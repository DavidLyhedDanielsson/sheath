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

            var scene = new Scene();
            scene.Import();

            SDL_SysWMinfo wmInfo = new SDL_SysWMinfo();
            SDL_VERSION(out wmInfo.version);
            SDL_GetWindowWMInfo(sdlWindow, ref wmInfo);

            var hRef = wmInfo.info.win.window;

            var dxResult = DirectX.Create(settings, hRef).LogIfFailed();
            if (dxResult.IsFailed)
                return -2;
            var dx = dxResult.Value;

            var psoResult = PipelineStateObject.Create(settings, dx.device, dx.rootSignature).LogIfFailed();
            if (psoResult.IsFailed)
                return -3;
            var pipelineStateObject = psoResult.Value;

            scene.WaitUntilDoneLoading();

            var running = true;
            while (running)
            {
                SDL_Event ev;
                while (SDL_PollEvent(out ev) != 0)
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

                var commandList = dx.commandList;
                dx.commandAllocator.Reset();
                commandList.Reset(dx.commandAllocator, pipelineStateObject.NdcTriangle);

                var frameIndex = (int)(dx.frameCount % 3);

                commandList.ResourceBarrier(new ResourceBarrier(new ResourceTransitionBarrier(dx.renderTargets[frameIndex], ResourceStates.Common, ResourceStates.RenderTarget)));

                commandList.ClearRenderTargetView(dx.descriptorHeap.GetCPUDescriptorHandleForHeapStart() + frameIndex * dx.rtvDescriptorSize, Vortice.Mathematics.Colors.LemonChiffon);
                commandList.OMSetRenderTargets(dx.descriptorHeap.GetCPUDescriptorHandleForHeapStart() + frameIndex * dx.rtvDescriptorSize);
                commandList.RSSetViewport(0.0f, 0.0f, settings.Window.Width, settings.Window.Height);
                commandList.RSSetScissorRect(settings.Window.Width, settings.Window.Height);
                commandList.IASetPrimitiveTopology(Vortice.Direct3D.PrimitiveTopology.TriangleList);
                commandList.SetGraphicsRootSignature(dx.rootSignature);

                commandList.DrawInstanced(3, 1, 0, 0);

                commandList.ResourceBarrier(new ResourceBarrier(new ResourceTransitionBarrier(dx.renderTargets[frameIndex], ResourceStates.RenderTarget, ResourceStates.Common)));

                commandList.Close();
                dx.commandQueue.ExecuteCommandList(commandList);

                dx.swapChain.Present(1);

                dx.EndFrame();
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
