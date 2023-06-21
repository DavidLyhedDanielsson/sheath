namespace ConsoleApp1;

using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.Direct3D12.Debug;
using Vortice.DXGI;
using FluentResults;

public class DirectX
{
    public ID3D12Device device;
    public ID3D12CommandQueue commandQueue;
    public ID3D12CommandAllocator commandAllocator;
    public ID3D12GraphicsCommandList commandList;
    public ID3D12RootSignature rootSignature;
    public ID3D12DescriptorHeap descriptorHeap;
    public ID3D12Resource[] renderTargets;
    public ID3D12Resource depthStencilView;

    public IDXGISwapChain swapChain;

    ID3D12Fence fence;
    AutoResetEvent fenceEvent;

    public ulong frameCount;

    public int rtvDescriptorSize;

    private static void DebugCallback(MessageCategory category, MessageSeverity severity, MessageId id, string description)
    {
        Console.WriteLine(description);
    }

    public static Result<DirectX> Create(Settings settings, IntPtr hRef)
    {
        DirectX dx = new DirectX();

        dx.renderTargets = new ID3D12Resource[settings.Graphics.BackBufferCount];
        dx.frameCount = 0;

        if (!D3D12.IsSupported(FeatureLevel.Level_12_1))
            return Result.Fail("Feature level 12.1 is not supported");

        if (D3D12.D3D12GetDebugInterface(out ID3D12Debug6? debug).Success)
        {
            debug!.EnableDebugLayer();
            debug!.SetEnableGPUBasedValidation(true);
            debug!.Dispose();
        }

        IDXGIFactory6 factory = DXGI.CreateDXGIFactory2<IDXGIFactory6>(true);
        factory.EnumAdapterByGpuPreference(0, GpuPreference.HighPerformance, out IDXGIAdapter? adapter);
        if (adapter == null)
            return Result.Fail("Couldn't find an adapter");

        D3D12.D3D12CreateDevice(adapter, FeatureLevel.Level_12_1, out dx.device);
        if (dx.device == null)
            return Result.Fail("Couldn't create device");

        ID3D12InfoQueue1 infoQueue = dx.device.QueryInterface<ID3D12InfoQueue1>();
        if (infoQueue != null)
            infoQueue!.RegisterMessageCallback(DebugCallback);

        dx.commandQueue = dx.device.CreateCommandQueue(new CommandQueueDescription(CommandListType.Direct, CommandQueuePriority.Normal));
        dx.commandAllocator = dx.device.CreateCommandAllocator(CommandListType.Direct);
        dx.commandList = dx.device.CreateCommandList<ID3D12GraphicsCommandList>(CommandListType.Direct, dx.commandAllocator);
        dx.commandList.Close();

        dx.swapChain = factory.CreateSwapChainForHwnd(dx.commandQueue, hRef, new SwapChainDescription1
        {
            Width = settings.Window.Width,
            Height = settings.Window.Height,
            Format = settings.Graphics.BackBufferFormat,
            Stereo = false,
            SampleDescription = new SampleDescription
            {
                Count = 1,
                Quality = 0,
            },
            BufferUsage = Usage.RenderTargetOutput,
            BufferCount = settings.Graphics.BackBufferCount,
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.FlipDiscard,
            AlphaMode = AlphaMode.Unspecified,
            Flags = SwapChainFlags.None
        });

        dx.rtvDescriptorSize = dx.device.GetDescriptorHandleIncrementSize(DescriptorHeapType.RenderTargetView);
        dx.descriptorHeap = dx.device.CreateDescriptorHeap(new DescriptorHeapDescription
        {
            Type = DescriptorHeapType.RenderTargetView,
            DescriptorCount = settings.Graphics.BackBufferCount,
            Flags = DescriptorHeapFlags.None,
            NodeMask = 0,
        });

        for (int i = 0; i < settings.Graphics.BackBufferCount; ++i)
        {
            dx.renderTargets[i] = dx.swapChain.GetBuffer<ID3D12Resource>(i);
            dx.device.CreateRenderTargetView(dx.renderTargets[i], null, dx.descriptorHeap.GetCPUDescriptorHandleForHeapStart() + i * dx.rtvDescriptorSize);
        }

        dx.device.CreateRootSignature(new VersionedRootSignatureDescription
        (
            new RootSignatureDescription1
            {
                Flags = RootSignatureFlags.None,
                Parameters = new RootParameter1[] { },
                StaticSamplers = new StaticSamplerDescription[] { },
            }
        ), out dx.rootSignature);

        dx.fence = dx.device.CreateFence();
        dx.fenceEvent = new AutoResetEvent(false);

        return Result.Ok(dx);
    }

    public void EndFrame()
    {
        commandQueue.Signal(fence, ++frameCount);
        fence.SetEventOnCompletion(frameCount, fenceEvent);
        fenceEvent.WaitOne();
    }
}