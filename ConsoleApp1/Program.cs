using ConsoleApp1.Asset;
using ConsoleApp1.Graphics;
using static SDL2.SDL;
using Vortice.Direct3D12;
using ConsoleApp1.Models;
using System.Diagnostics;
using System.Numerics;
using static System.Runtime.InteropServices.Marshal;

namespace ConsoleApp1
{
    internal class Program
    {
        public class MyConsoleLogger : FluentResults.IResultLogger
        {
            public void Log(string context, string content, FluentResults.ResultBase result,
                Microsoft.Extensions.Logging.LogLevel logLevel)
            {
                Console.WriteLine("Result: {0} {1} <{2}>", result.Reasons.Select(reason => reason.Message), content,
                    context);
            }

            public void Log<TContext>(string content, FluentResults.ResultBase result,
                Microsoft.Extensions.Logging.LogLevel logLevel)
            {
                Console.WriteLine("Result: {0} {1} <{2}>", result.Reasons.Select(reason => reason.Message), content,
                    typeof(TContext).FullName);
            }
        };

        static int Main(string[] args)
        {
            var logger = new MyConsoleLogger();
            FluentResults.Result.Setup(cfg => { cfg.Logger = logger; });

            Settings settings = Settings.Load() ?? new Settings();

            if (SDL_Init(SDL_INIT_VIDEO) < 0)
                return -1;

            var sdlWindow = SDL_CreateWindow(
                "Test window"
                , settings.Window.PositionX
                , settings.Window.PositionY
                , settings.Window.Width
                , settings.Window.Height
                , 0);

            AssetCatalogue assetCatalogue = new();
            AssetLoader.Import(assetCatalogue, "Map/Main.gltf");

            SDL_SysWMinfo wmInfo = new();
            SDL_VERSION(out wmInfo.version);
            SDL_GetWindowWMInfo(sdlWindow, ref wmInfo);

            var hRef = wmInfo.info.win.window;

            var graphicsStateResult = GraphicsState.Create(settings, hRef).LogIfFailed();
            if (graphicsStateResult.IsFailed)
                return -2;
            GraphicsState graphicsState = graphicsStateResult.Value;

            var psoResult = PSOConfig.Create(settings, graphicsState.device, graphicsState.rootSignature).LogIfFailed();
            if (psoResult.IsFailed)
                return -3;
            PSOConfig psoConfig = psoResult.Value;

            var loadCycle = new string[]
            {
                "MapleTree_1",
                "MapleTree_2",
                "MapleTree_3",
                "PineTree_1",
                "PineTree_2",
                "PineTree_3",
                "PineTree_5",
                "SM_House_01_C_2",
                "SM_House_06_A",
                "SM_Long_House_01_A",
                "SM_Small_House_02_C",
            };
            var modelCycle = new string[]
            {
                "MapleTree_1",
                "MapleTree_2",
                "MapleTree_3",
                "PineTree_1",
                "PineTree_2",
                "PineTree_3",
                "PineTree_5",
                "SM_House_01_C_2",
                "SM_House_06_A",
                "SM_Long_House_01_A",
                "SM_Small_House_02_C",
            };

            Showroom showroom = new();

            var heapResult = LinearResourceBuilder.CreateHeapState(graphicsState).LogIfFailed();
            if (heapResult.IsFailed)
                return -3;
            var heapState = heapResult.Value;

            assetCatalogue.ForEachMaterial(material =>
            {
                LinearResourceBuilder.CreateTexture(graphicsState, heapState, material.Albedo);
            });

            assetCatalogue.ForEachDefaultMaterial((Mesh mesh, string[] materials) =>
            {
                if (!loadCycle.Contains(mesh.Name))
                    return;

                List<Material>[] mats = new List<Material>[materials.Length];
                for (int i = 0; i < materials.Length; ++i)
                {
                    mats[i] = new();

                    Material? material = assetCatalogue.GetMaterial(materials[i]);
                    Debug.Assert(material != null);
                    mats[i].Add(material);
                }

                Model model = ModelBuilder.CreateModel(graphicsState, heapState, assetCatalogue, mesh, mats);

                showroom.AddShowcase(mesh.Name, model);
            });

            ID3D12Resource cameraBuffer = graphicsState.device.CreateCommittedResource(HeapType.Upload,
                ResourceDescription.Buffer(1024), ResourceStates.AllShaderResource);
            graphicsState.device.CreateConstantBufferView(new ConstantBufferViewDescription(cameraBuffer, 1024),
                heapState.cbvUavSrvDescriptorHeap.Segments[HeapConfig.Segments.cbvs].NextCpuHandle());

            graphicsState.commandList.Close();
            graphicsState.commandQueue.ExecuteCommandList(graphicsState.commandList);
            graphicsState.WaitUntilIdle();

            heapState.uploadBuffer.UploadsDone();
            while (heapState.uploadBuffer.HasMoreUploads())
            {
                graphicsState.commandAllocator.Reset();
                graphicsState.commandList.Reset(graphicsState.commandAllocator);
                heapState.uploadBuffer.QueueRemainingUploads(graphicsState.commandList);
                graphicsState.commandList.Close();
                graphicsState.commandQueue.ExecuteCommandList(graphicsState.commandList);
                graphicsState.WaitUntilIdle();
                heapState.uploadBuffer.UploadsDone();
            }

            Stopwatch uptime = Stopwatch.StartNew();

            var running = true;
            while (running)
            {
                while (SDL_PollEvent(out SDL_Event ev) != 0)
                {
                    switch (ev.type)
                    {
                        case SDL_EventType.SDL_QUIT:
                            running = false;
                            break;
                        case SDL_EventType.SDL_KEYDOWN:
                            if (ev.key.keysym.scancode == SDL_Scancode.SDL_SCANCODE_ESCAPE)
                                running = false;
                            break;
                    }
                }

                Matrix4x4 viewMatrix = Matrix4x4.CreateLookAt(
                    new Vector3(MathF.Cos((float)uptime.Elapsed.TotalSeconds) * 30.0f, 30.0f,
                        MathF.Sin((float)uptime.Elapsed.TotalSeconds) * 30.0f), Vector3.Zero, Vector3.UnitY);
                Matrix4x4 projMatrix = Matrix4x4.CreatePerspectiveFieldOfView(59.0f * (MathF.PI / 180.0f),
                    settings.Window.Width / (float)settings.Window.Height, 1, 50);
                Matrix4x4 viewProjMatrix = Matrix4x4.Transpose(viewMatrix * projMatrix);
                unsafe
                {
                    byte* data;
                    cameraBuffer.Map(0, (void**)&data);
                    Buffer.MemoryCopy(&viewProjMatrix.M11, data, 1024, 4 * 4 * 4);
                    cameraBuffer.Unmap(0);
                }

                var commandList = graphicsState.commandList;
                graphicsState.commandAllocator.Reset();
                commandList.Reset(graphicsState.commandAllocator, psoConfig.NdcTriangle);

                var frameIndex = (int)(graphicsState.frameCount % 3);

                commandList.ResourceBarrier(new ResourceBarrier(new ResourceTransitionBarrier(
                    graphicsState.renderTargets[frameIndex], ResourceStates.Common, ResourceStates.RenderTarget)));

                commandList.ClearRenderTargetView(
                    graphicsState.rtvDescriptorHeap.GetCPUDescriptorHandleForHeapStart() +
                    frameIndex * graphicsState.rtvDescriptorSize, Vortice.Mathematics.Colors.LemonChiffon);
                commandList.OMSetRenderTargets(graphicsState.rtvDescriptorHeap.GetCPUDescriptorHandleForHeapStart() +
                                               frameIndex * graphicsState.rtvDescriptorSize);
                commandList.RSSetViewport(0.0f, 0.0f, settings.Window.Width, settings.Window.Height);
                commandList.RSSetScissorRect(settings.Window.Width, settings.Window.Height);
                commandList.IASetPrimitiveTopology(Vortice.Direct3D.PrimitiveTopology.TriangleList);
                commandList.SetGraphicsRootSignature(graphicsState.rootSignature);
                commandList.SetDescriptorHeaps(heapState.cbvUavSrvDescriptorHeap.ID3D12DescriptorHeap);
                commandList.SetGraphicsRootDescriptorTable(1, heapState.cbvUavSrvDescriptorHeap.ID3D12DescriptorHeap.GetGPUDescriptorHandleForHeapStart());

                int modelIndex = (uptime.Elapsed.Seconds / 3) % modelCycle.Length;
                Model model = showroom.GetShowcase(modelCycle[modelIndex])!.Model;
                unsafe
                {
                    byte* data;
                    graphicsState.instanceBuffer.Map(0, (void**)&data);
                    Buffer.MemoryCopy(&modelIndex, data, 4, 4);
                    graphicsState.instanceBuffer.Unmap(0);
                }

                for (int i = 0; i < model.Submeshes.Length; ++i)
                {
                    var submesh = model.Submeshes[i];

                    commandList.SetGraphicsRootConstantBufferView(0, graphicsState.instanceBuffer.GPUVirtualAddress);
                    commandList.IASetIndexBuffer(new IndexBufferView(submesh.VIBufferView.IndexBuffer.GPUVirtualAddress, submesh.VIBufferView.IndexBufferTotalCount * SizeOf(typeof(uint)), Vortice.DXGI.Format.R32_UInt));
                    commandList.DrawIndexedInstanced(submesh.VIBufferView.IndexCount, 1, submesh.VIBufferView.IndexStart, 0, 0);
                }

                commandList.ResourceBarrier(new ResourceBarrier(new ResourceTransitionBarrier(
                    graphicsState.renderTargets[frameIndex], ResourceStates.RenderTarget, ResourceStates.Common)));

                commandList.Close();
                graphicsState.commandQueue.ExecuteCommandList(commandList);

                graphicsState.swapChain.Present(1);

                graphicsState.EndFrameAndWait();
            }

            {
                SDL_GetWindowPosition(sdlWindow, out int x, out int y);
                settings.Window.PositionX = x;
                settings.Window.PositionY = y;
                SDL_GetWindowSize(sdlWindow, out int sizeX, out int sizeY);
                settings.Window.Width = sizeX;
                settings.Window.Height = sizeY;
                settings.Save();
            }

            SDL_Quit();

            return 0;
        }
    }
}