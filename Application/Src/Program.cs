global using Silk.NET.Maths;

using Application.Asset;
using Application.Graphics;
using static SDL2.SDL;
using Vortice.Direct3D12;
using Application.Models;
using System.Diagnostics;
using ImGuiNET;
using Arch.Core;
using System.Runtime.InteropServices;
using static System.Runtime.InteropServices.Marshal;
using Vortice.DXGI;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;
using System.Numerics;
using Application.Graphics.shader;

namespace Application
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
            AssetLoader.Import(assetCatalogue, "Asset/Prop/Phone.gltf");

            SDL_SysWMinfo wmInfo = new();
            SDL_VERSION(out wmInfo.version);
            SDL_GetWindowWMInfo(sdlWindow, ref wmInfo);

            var hRef = wmInfo.info.win.window;

            var graphicsStateResult = GraphicsState.Create(settings, hRef).LogIfFailed();
            if (graphicsStateResult.IsFailed)
                return -2;
            GraphicsState graphicsState = graphicsStateResult.Value;

            ImGui.CreateContext();
            ImGui.StyleColorsDark();
            ImGuiBackend.Renderer imGuiRenderer = new();
            var imGuiDescHeap = graphicsState.device.CreateDescriptorHeap(new(DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView, 1, DescriptorHeapFlags.ShaderVisible));
            imGuiRenderer.ImGui_ImplDX12_Init(graphicsState.device, settings.Graphics.BackBufferCount, settings.Graphics.BackBufferFormat, imGuiDescHeap.GetCPUDescriptorHandleForHeapStart(), imGuiDescHeap.GetGPUDescriptorHandleForHeapStart());

            ImGuiBackend.SdlBackend imGuiSdlBackend = new(sdlWindow);

            Showroom showroom = new();

            var heapResult = LinearResourceBuilder.CreateHeapState(graphicsState).LogIfFailed();
            if (heapResult.IsFailed)
                return -3;
            var heapState = heapResult.Value;

            // TODO: these should live somewhere
            Dictionary<string, Texture> textureNames = new();
            Dictionary<string, Surface> surfaceNames = new();
            Dictionary<string, Mesh> meshNames = new();

            // Create bulb stuff
            {
                Texture texture = LinearResourceBuilder.CreateTexture(graphicsState, heapState, AssetLoader.CreateTexture("Asset/", "Bulb.png").LogIfFailed().Value);
                textureNames.Add("Bulb.png", texture);

                Surface surface = LinearResourceBuilder.CreateBillboardSurface(settings, graphicsState, heapState, texture);
                surfaceNames.Add("Bulb", surface);
            }

            Surface bulbSurface = surfaceNames["Bulb"];

            assetCatalogue.ForEachTextureData(textureData =>
            {
                Texture texture = LinearResourceBuilder.CreateTexture(graphicsState, heapState, textureData);
                textureNames.Add(textureData.FilePath, texture);
            });

            assetCatalogue.ForEachMaterial(material =>
            {
                Surface surface = LinearResourceBuilder.CreateSurface(settings, graphicsState, heapState, material, textureNames);
                surfaceNames.Add(material.Name, surface);
            });

            assetCatalogue.ForEachVertexData(vertexData =>
            {
                Mesh mesh = LinearResourceBuilder.CreateMesh(graphicsState, heapState, vertexData);
                meshNames.Add(vertexData.Name, mesh);
            });

            //Mesh terrainMesh = LinearResourceBuilder.CreateMesh(graphicsState, heapState, assetCatalogue.GetVertexData("Map/terrain.r16")!);
            //Surface terrainSurface = LinearResourceBuilder.CreateTerrainSurface(settings, graphicsState, heapState);

            //Scene scene = new();

            assetCatalogue.ForEachDefaultMaterial((string vertexDataId, string[] materials) =>
            {
                Surface[] surfaces = new Surface[materials.Length];
                for (int i = 0; i < materials.Length; ++i)
                {
                    surfaceNames.TryGetValue(materials[i], out surfaces[i]);
                    Debug.Assert(surfaces[i] != null);
                }

                meshNames.TryGetValue(vertexDataId, out Mesh? mesh);
                Debug.Assert(mesh != null);

                Model model = ModelBuilder.CreateModel(mesh, surfaces);

                showroom.AddShowcase(vertexDataId, model);
            });

            World world = World.Create();

            Model phoneModel = showroom.GetShowcase("SM_Phone_01a").Model;
            world.Create(new ECS.Component.Position(0.0f, 0.0f, 0.0f), new ECS.Component.Renderable()
            {
                VIBufferViews = phoneModel.Submeshes.Select(m => m.VIBufferView).ToArray(),
                Surfaces = phoneModel.Submeshes.Select(m => m.Surface).ToArray(),
            });
            world.Create(new ECS.Component.Position(0.3f, 0.3f, 0.3f), new ECS.Component.Renderable()
            {
                VIBufferViews = phoneModel.Submeshes.Select(m => m.VIBufferView).ToArray(),
                Surfaces = phoneModel.Submeshes.Select(m => m.Surface).ToArray(),
            });
            world.Create(new ECS.Component.Position(-0.3f, -0.3f, -0.3f), new ECS.Component.Renderable()
            {
                VIBufferViews = phoneModel.Submeshes.Select(m => m.VIBufferView).ToArray(),
                Surfaces = phoneModel.Submeshes.Select(m => m.Surface).ToArray(),
            });

            //scene.AddInstance(.Model, new InstanceData { transform = Matrix4X4<float>.Identity });
            //var bounds = scene.GetBounds();

            ID3D12Resource cameraBuffer = graphicsState.device.CreateCommittedResource(HeapType.Upload,
                ResourceDescription.Buffer(1024), ResourceStates.AllShaderResource);
            graphicsState.device.CreateConstantBufferView(new ConstantBufferViewDescription(cameraBuffer, 1024),
                heapState.cbvUavSrvDescriptorHeap.Segments[HeapConfig.Segments.cbvs].NextCpuHandle());

            // This should be moved into a common heap and uploaded through upload heap
            ID3D12Resource perFrameBuffer = graphicsState.device.CreateCommittedResource(HeapType.Upload,
                ResourceDescription.Buffer(1024), ResourceStates.AllShaderResource);
            graphicsState.device.CreateConstantBufferView(new ConstantBufferViewDescription(perFrameBuffer, 1024),
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

            Vector3D<float> cameraPosition = new(0.0f, 0.0f, -1.0f);
            Vector3D<float> lightPosition = new(0.0f, 0.0f, -1.0f);

            // bool spinCamera = false;
            //var center = bounds.Center;
            //var radius = 0.5f;
            Matrix4X4<float> viewMatrix = Matrix4X4.CreateLookAt(
                cameraPosition,
                new Vector3D<float>(0.0f, 0.1f, 0.0f),
                Vector3D<float>.UnitY
            );

            // Reload shaders
            List<RenamedEventArgs> reloadFiles = new();

            // VS saves a file to a tmp, renames old file to tmp, then renames the first tmp to the file name
            using var vsWatcher = new FileSystemWatcher(Utils.ShaderRootPath + "/vs");
            vsWatcher.NotifyFilter = NotifyFilters.FileName;
            vsWatcher.Filter = "*.hlsl";
            vsWatcher.Renamed += (object sender, RenamedEventArgs args) =>
            {
                if (args.Name.EndsWith(".TMP"))
                    return;

                reloadFiles.Add(args);
            };
            vsWatcher.EnableRaisingEvents = true;

            using var psWatcher = new FileSystemWatcher(Utils.ShaderRootPath + "/ps");
            psWatcher.NotifyFilter = NotifyFilters.FileName;
            psWatcher.Filter = "*.hlsl";
            psWatcher.Renamed += (object sender, RenamedEventArgs args) =>
            {
                if (args.Name.EndsWith(".TMP"))
                    return;

                reloadFiles.Add(args);
            };
            psWatcher.EnableRaisingEvents = true;

            var running = true;
            while (running)
            {
                // Reload shaders
                for (int i = reloadFiles.Count - 1; i >= 0; --i)
                {
                    RenamedEventArgs reloadArgs = reloadFiles[i];

                    try
                    {
                        if (Regex.IsMatch(reloadArgs.FullPath, @"[/\\]ps[/\\]"))
                            LinearResourceBuilder.RecreatePsos(settings, graphicsState, null, reloadArgs.Name);
                        else if (Regex.IsMatch(reloadArgs.FullPath, @"[/\\]vs[/\\]"))
                            LinearResourceBuilder.RecreatePsos(settings, graphicsState, reloadArgs.Name, null);

                        reloadFiles.RemoveAt(i);
                    }
                    catch (System.IO.IOException ex)
                    {
                        // :( Try again
                        Console.WriteLine(ex.ToString());
                    }
                }

                while (SDL_PollEvent(out SDL_Event ev) != 0)
                {
                    imGuiSdlBackend.ImGui_ImplSDL2_ProcessEvent(ev);
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

                float lightRadius = 10.0f;
                /*lightPosition = new Vector3D<float>(
                            MathF.Cos((float)uptime.Elapsed.TotalSeconds) * lightRadius,
                            0.1f,
                            MathF.Sin((float)uptime.Elapsed.TotalSeconds) * lightRadius
                        );*/
                lightPosition = new Vector3D<float>(MathF.Cos((float)uptime.Elapsed.TotalSeconds), 0.0f, 1.0f);

                viewMatrix = Matrix4X4.CreateLookAt(
                    cameraPosition,
                    new Vector3D<float>(0.0f, 0.1f, 0.0f),
                    Vector3D<float>.UnitY
                );

                /*if (spinCamera)
                {
                    viewMatrix = Matrix4X4.CreateLookAt(
                        new Vector3D<float>(
                            MathF.Cos((float)uptime.Elapsed.TotalSeconds) * radius,
                            0.2f,
                            MathF.Sin((float)uptime.Elapsed.TotalSeconds) * radius
                        ),
                        new Vector3D<float>(0.0f, 0.1f, 0.0f),
                        Vector3D<float>.UnitY
                    );
                }*/
                Matrix4X4<float> projMatrix = Matrix4X4.CreatePerspectiveFieldOfView(59.0f * (MathF.PI / 180.0f),
                    settings.Window.Width / (float)settings.Window.Height, 500.0f, 0.001f);
                Matrix4X4<float> viewProjMatrix = Matrix4X4.Transpose(viewMatrix * projMatrix);
                unsafe
                {
                    byte* data;
                    cameraBuffer.Map(0, (void**)&data);
                    Buffer.MemoryCopy(&viewProjMatrix, data, 1024, 4 * 4 * 4);
                    cameraBuffer.Unmap(0);
                }

                unsafe
                {
                    FrameData frameData = new FrameData
                    {
                        CameraPosition = cameraPosition,
                        LightPosition = lightPosition,
                    };

                    byte* data;
                    perFrameBuffer.Map(0, (void**)&data);
                    Buffer.MemoryCopy(&frameData, data, 1024, SizeOf<FrameData>());
                    perFrameBuffer.Unmap(0);
                }

                var commandList = graphicsState.commandList;
                graphicsState.commandAllocator.Reset();
                commandList.Reset(graphicsState.commandAllocator);

                var frameIndex = (int)(graphicsState.frameCount % 3);

                commandList.ResourceBarrier(
                    new ResourceBarrier[] {
                        ResourceBarrier.BarrierTransition(graphicsState.renderTargets[frameIndex], ResourceStates.Common, ResourceStates.RenderTarget),
                    }
                );

                commandList.ClearRenderTargetView(
                    graphicsState.rtvDescriptorHeap.GetCPUDescriptorHandleForHeapStart() +
                    frameIndex * graphicsState.rtvDescriptorSize, Vortice.Mathematics.Colors.LemonChiffon);
                commandList.ClearDepthStencilView(
                    graphicsState.dsvDescriptorHeap.GetCPUDescriptorHandleForHeapStart(),
                    ClearFlags.Depth | ClearFlags.Stencil,
                    settings.Graphics.DepthClearValue,
                    0
                );
                commandList.OMSetRenderTargets(
                    graphicsState.rtvDescriptorHeap.GetCPUDescriptorHandleForHeapStart() + frameIndex * graphicsState.rtvDescriptorSize,
                    graphicsState.dsvDescriptorHeap.GetCPUDescriptorHandleForHeapStart()
                );

                commandList.RSSetViewport(0.0f, 0.0f, settings.Window.Width, settings.Window.Height);
                commandList.RSSetScissorRect(settings.Window.Width, settings.Window.Height);
                commandList.IASetPrimitiveTopology(Vortice.Direct3D.PrimitiveTopology.TriangleList);
                commandList.SetGraphicsRootSignature(graphicsState.rootSignature);
                commandList.SetDescriptorHeaps(heapState.cbvUavSrvDescriptorHeap.ID3D12DescriptorHeap);
                commandList.SetGraphicsRootDescriptorTable(1, heapState.cbvUavSrvDescriptorHeap.ID3D12DescriptorHeap.GetGPUDescriptorHandleForHeapStart());

                //Dictionary<PSO, List<InstanceData>> instanceDatas = new();
                Dictionary<PSO, Dictionary<VIBufferView, (List<Surface>, List<InstanceData>)>> renderPsos = new();

                world.Query(new QueryDescription().WithAll<ECS.Component.Renderable, ECS.Component.Position>(), (ref ECS.Component.Renderable renderable, ref ECS.Component.Position position) =>
                {
                    for (int i = 0; i < renderable.Surfaces.Length; ++i)
                    {
                        Surface surface = renderable.Surfaces[i];

                        if (!renderPsos.ContainsKey(renderable.Surfaces[i].PSO))
                            renderPsos.Add(surface.PSO, new());

                        var psoObjects = renderPsos[surface.PSO];

                        if (!psoObjects.ContainsKey(renderable.VIBufferViews[i]))
                            psoObjects.Add(renderable.VIBufferViews[i], (new(), new()));

                        (List<Surface> surfaces, List<InstanceData> instanceDatas) = psoObjects[renderable.VIBufferViews[i]];

                        surfaces.Add(surface);
                        instanceDatas.Add(new InstanceData
                        {
                            transform = Matrix4X4.CreateTranslation(position.X, position.Y, position.Z)
                        });
                    }
                });

                int instanceDataOffset = 0;
                int drawOffset = 0;

                foreach (var psoPair in renderPsos)
                {
                    PSO pso = psoPair.Key;
                    Dictionary<VIBufferView, (List<Surface>, List<InstanceData>)> drawData = psoPair.Value;

                    commandList.SetPipelineState(pso.ID3D12PipelineState);

                    foreach (var (bufferView, (surfaces, instanceDatas)) in drawData)
                    {
                        ReadOnlySpan<InstanceData> dataSpan = CollectionsMarshal.AsSpan(instanceDatas);
                        heapState.instanceDataBuffer.SetData(dataSpan, instanceDataOffset * SizeOf<InstanceData>());

                        unsafe
                        {
                            byte* data;
                            heapState.perDrawBuffer.Map(0, (void**)&data);

                            for (int i = 0; i < surfaces.Count; ++i)
                            {
                                Debug.Assert(SizeOf<ModelData>() <= 256);
                                ModelData modelData = new()
                                {
                                    AlbedoTextureId = surfaces[i].AlbedoTexture.ID,
                                    NormalTextureId = surfaces[i].NormalTexture.ID,
                                    OrmTextureId = surfaces[i].ORMTexture.ID,
                                    VertexBufferId = bufferView.VertexBufferId,
                                    InstanceStartOffset = instanceDataOffset,
                                };
                                Buffer.MemoryCopy(&modelData, data + drawOffset * 256, 256, SizeOf<ModelData>());

                            }
                            heapState.perDrawBuffer.Unmap(0);
                        }

                        instanceDataOffset += instanceDatas.Count;

                        graphicsState.commandList.IASetIndexBuffer(new IndexBufferView(bufferView.IndexBuffer.GPUVirtualAddress, bufferView.IndexBufferTotalCount * SizeOf(typeof(uint)), Format.R32_UInt));
                        graphicsState.commandList.SetGraphicsRootConstantBufferView(0, heapState.perDrawBuffer.GPUVirtualAddress + (ulong)(drawOffset * 256));
                        graphicsState.commandList.DrawIndexedInstanced(bufferView.IndexCount, instanceDatas.Count, bufferView.IndexStart, 0, 0);

                        drawOffset++;
                    }
                }

                //scene.Render(graphicsState, heapState.instanceDataBuffer, heapState.perDrawBuffer);

                commandList.RSSetViewport(0.0f, 0.0f, settings.Window.Width, settings.Window.Height);
                commandList.RSSetScissorRect(settings.Window.Width, settings.Window.Height);
                commandList.IASetPrimitiveTopology(Vortice.Direct3D.PrimitiveTopology.TriangleList);
                commandList.SetGraphicsRootSignature(graphicsState.rootSignature);
                commandList.SetDescriptorHeaps(heapState.cbvUavSrvDescriptorHeap.ID3D12DescriptorHeap);
                commandList.SetGraphicsRootDescriptorTable(1, heapState.cbvUavSrvDescriptorHeap.ID3D12DescriptorHeap.GetGPUDescriptorHandleForHeapStart());
                graphicsState.commandList.SetPipelineState(bulbSurface.PSO.ID3D12PipelineState);
                unsafe
                {
                    byte* data;
                    heapState.perDrawBuffer.Map(0, (void**)&data);
                    data += drawOffset * 256;

                    Debug.Assert(SizeOf<ModelData>() <= 256);
                    ModelData modelData = new()
                    {
                        AlbedoTextureId = bulbSurface.AlbedoTexture.ID,
                        NormalTextureId = -1,
                        OrmTextureId = -1,
                        VertexBufferId = -1,
                        InstanceStartOffset = -1
                    };
                    Buffer.MemoryCopy(&modelData, data, 256, SizeOf<ModelData>());

                    heapState.perDrawBuffer.Unmap(0);
                }
                graphicsState.commandList.SetGraphicsRootConstantBufferView(0, heapState.perDrawBuffer.GPUVirtualAddress + (ulong)(drawOffset * 256));
                graphicsState.commandList.DrawInstanced(6, 1, 0, 0);
                drawOffset++;


                imGuiRenderer.ImGui_ImplDX12_NewFrame();
                imGuiSdlBackend.ImGui_ImplSDL2_NewFrame();
                ImGui.NewFrame();

                ImGui.Begin("Window");
                unsafe
                {
                    ImGui.DragFloat3("Light position", ref *(Vector3*)&lightPosition);
                    ImGui.DragFloat3("Camera position", ref *(Vector3*)&cameraPosition, 0.001f);
                }
                //ImGui.Checkbox("Spin camera", ref spinCamera);
                ImGui.End();


                ImGui.Render();
                commandList.SetDescriptorHeaps(imGuiDescHeap);
                imGuiRenderer.ImGui_ImplDX12_RenderDrawData(ImGui.GetDrawData(), graphicsState.commandList);

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