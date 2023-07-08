using ConsoleApp1.Asset;
using ConsoleApp1.Graphics;
using static SDL2.SDL;
using Vortice.Direct3D12;

namespace ConsoleApp1
{
    internal class Program
    {
        public class MyConsoleLogger : FluentResults.IResultLogger
        {
            public void Log(string context, string content, FluentResults.ResultBase result, Microsoft.Extensions.Logging.LogLevel logLevel)
            {
                Console.WriteLine("Result: {0} {1} <{2}>", result.Reasons.Select(reason => reason.Message), content, context);
            }

            public void Log<TContext>(string content, FluentResults.ResultBase result, Microsoft.Extensions.Logging.LogLevel logLevel)
            {
                Console.WriteLine("Result: {0} {1} <{2}>", result.Reasons.Select(reason => reason.Message), content, typeof(TContext).FullName);
            }
        };

        static int Main(string[] args)
        {
            var logger = new MyConsoleLogger();
            FluentResults.Result.Setup(cfg =>
            {
                cfg.Logger = logger;
            });

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

            GraphicsBuilder.VertexIndexBuilder viBuilder = GraphicsBuilder.CreateVertexIndexBuffers(graphicsState, psoConfig);
            assetCatalogue.ForEachMesh(mesh => viBuilder.AddMesh(mesh));
            viBuilder.Build();

            GraphicsBuilder.TextureBuilder textureBuilder = GraphicsBuilder.CreateTextures(graphicsState);
            GraphicsBuilder.SurfaceBuilder surfaceBuilder = GraphicsBuilder.CreateSurfaces(graphicsState);
            assetCatalogue.ForEachMaterial(material =>
            {
                Texture? texture = assetCatalogue.GetTexture(material.Diffuse.FilePath);

                if (texture != null)
                    textureBuilder.AddTexture(texture);

                surfaceBuilder.AddMaterial(material);
            });
            textureBuilder.Build();
            surfaceBuilder.Build();

            graphicsState.commandList.Close();
            graphicsState.commandQueue.ExecuteCommandList(graphicsState.commandList);
            graphicsState.WaitUntilIdle();

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

                var commandList = graphicsState.commandList;
                graphicsState.commandAllocator.Reset();
                commandList.Reset(graphicsState.commandAllocator, psoConfig.NdcTriangle);

                var frameIndex = (int)(graphicsState.frameCount % 3);

                commandList.ResourceBarrier(new ResourceBarrier(new ResourceTransitionBarrier(graphicsState.renderTargets[frameIndex], ResourceStates.Common, ResourceStates.RenderTarget)));

                commandList.ClearRenderTargetView(graphicsState.rtvDescriptorHeap.GetCPUDescriptorHandleForHeapStart() + frameIndex * graphicsState.rtvDescriptorSize, Vortice.Mathematics.Colors.LemonChiffon);
                commandList.OMSetRenderTargets(graphicsState.rtvDescriptorHeap.GetCPUDescriptorHandleForHeapStart() + frameIndex * graphicsState.rtvDescriptorSize);
                commandList.RSSetViewport(0.0f, 0.0f, settings.Window.Width, settings.Window.Height);
                commandList.RSSetScissorRect(settings.Window.Width, settings.Window.Height);
                commandList.IASetPrimitiveTopology(Vortice.Direct3D.PrimitiveTopology.TriangleList);
                commandList.SetGraphicsRootSignature(graphicsState.rootSignature);
                commandList.SetDescriptorHeaps(graphicsState.cbvUavSrvDescriptorHeap);
                commandList.SetGraphicsRootDescriptorTable(0, graphicsState.cbvUavSrvDescriptorHeap.GetGPUDescriptorHandleForHeapStart());

                commandList.DrawInstanced(6, 1, 0, 0);

                commandList.ResourceBarrier(new ResourceBarrier(new ResourceTransitionBarrier(graphicsState.renderTargets[frameIndex], ResourceStates.RenderTarget, ResourceStates.Common)));

                commandList.Close();
                graphicsState.commandQueue.ExecuteCommandList(commandList);

                graphicsState.swapChain.Present(1);

                graphicsState.EndFrame();
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
