using FluentResults;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.Direct3D12.Debug;
using Vortice.DXGI;

namespace Application.Graphics;

public class GraphicsState : IDisposable
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public ID3D12Device Device { get; private set; }
    public ID3D12CommandQueue CommandQueue { get; private set; }
    public ID3D12CommandAllocator CommandAllocator { get; private set; }
    public ID3D12GraphicsCommandList CommandList { get; private set; }
    public ID3D12RootSignature RootSignature { get; private set; }
    public ID3D12DescriptorHeap RtvDescriptorHeap { get; private set; }
    public ID3D12DescriptorHeap DsvDescriptorHeap { get; private set; }
    public ID3D12Resource[] RenderTargets { get; private set; }
    public ID3D12Resource DepthBuffer { get; private set; }
    public IDXGISwapChain SwapChain { get; private set; }

    private ID3D12Fence _fence;
    private AutoResetEvent _fenceEvent;

    private ID3D12Fence _frameFence;
    private AutoResetEvent _frameFenceEvent;

    public ulong fenceCount;
    public ulong frameCount = 0;

    public int RtvDescriptorSize { get; private set; }
    public int DsvDescriptorSize { get; private set; }

    public List<PSO> livePsos = new();

    public void Dispose()
    {
        WaitUntilIdle();

        CommandQueue.Dispose();
        CommandAllocator.Dispose();
        CommandList.Dispose();
        RootSignature.Dispose();
        RtvDescriptorHeap.Dispose();
        DsvDescriptorHeap.Dispose();
        foreach (var renderTarget in RenderTargets)
            renderTarget.Dispose();
        DepthBuffer.Dispose();
        SwapChain.Dispose();

        _fence.Dispose();
        _fenceEvent.Dispose();
        _frameFence.Dispose();
        _frameFenceEvent.Dispose();

        foreach (var pso in livePsos)
            pso.Dispose();
        livePsos.Clear();

        var debugDevice = Device.QueryInterface<ID3D12DebugDevice>();
        Device.Dispose();

        if (debugDevice != null)
        {
            debugDevice!.ReportLiveDeviceObjects(ReportLiveDeviceObjectFlags.Detail | ReportLiveDeviceObjectFlags.IgnoreInternal);
            debugDevice!.Dispose();
        }
    }


#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    private static void DebugCallback(MessageCategory category, MessageSeverity severity, MessageId id, string description)
    {
        Console.WriteLine(description);
    }

    public static Result<GraphicsState> Create(Settings settings, IntPtr hRef)
    {
        GraphicsState state = new GraphicsState();

        state.RenderTargets = new ID3D12Resource[settings.Graphics.BackBufferCount];

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

        {

            D3D12.D3D12CreateDevice(adapter, FeatureLevel.Level_12_1, out ID3D12Device? device);
            if (device == null)
                return Result.Fail("Couldn't create device");
            state.Device = device;
            state.Device.Name = "Device";
        }

        ID3D12InfoQueue1 infoQueue = state.Device.QueryInterface<ID3D12InfoQueue1>();
        if (infoQueue != null)
        {
            infoQueue!.RegisterMessageCallback(DebugCallback);
            infoQueue.Dispose();
        }

        state.CommandQueue = state.Device.CreateCommandQueue(new CommandQueueDescription(CommandListType.Direct, CommandQueuePriority.Normal));
        state.CommandQueue.Name = "CommandQueue";
        state.CommandAllocator = state.Device.CreateCommandAllocator(CommandListType.Direct);
        state.CommandAllocator.Name = "CommandAllocator";
        state.CommandList = state.Device.CreateCommandList<ID3D12GraphicsCommandList>(CommandListType.Direct, state.CommandAllocator);
        state.CommandList.Name = "CommandList";

        state.SwapChain = factory.CreateSwapChainForHwnd(state.CommandQueue, hRef, new SwapChainDescription1
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
        state.SwapChain.DebugName = "SwapChain";

        state.RtvDescriptorSize = state.Device.GetDescriptorHandleIncrementSize(DescriptorHeapType.RenderTargetView);
        state.RtvDescriptorHeap = state.Device.CreateDescriptorHeap(new(DescriptorHeapType.RenderTargetView, settings.Graphics.BackBufferCount, DescriptorHeapFlags.None));
        state.RtvDescriptorHeap.Name = "RtvDescriptorHeap";

        for (int i = 0; i < settings.Graphics.BackBufferCount; ++i)
        {
            state.RenderTargets[i] = state.SwapChain.GetBuffer<ID3D12Resource>(i);
            state.RenderTargets[i].Name = $"RenderTarget{i}";
            state.Device.CreateRenderTargetView(state.RenderTargets[i], null, state.RtvDescriptorHeap.GetCPUDescriptorHandleForHeapStart() + i * state.RtvDescriptorSize);
        }

        state.DsvDescriptorSize = state.Device.GetDescriptorHandleIncrementSize(DescriptorHeapType.DepthStencilView);
        state.DsvDescriptorHeap = state.Device.CreateDescriptorHeap(new(DescriptorHeapType.DepthStencilView, 1, DescriptorHeapFlags.None));
        state.DsvDescriptorHeap.Name = "DsvDescriptorHeap";

        state.DepthBuffer = state.Device.CreateCommittedResource(
            HeapType.Default,
            ResourceDescription.Texture2D(
                settings.Graphics.DepthStencilFormat,
                (uint)settings.Window.Width,
                (uint)settings.Window.Height,
                1,
                1,
                1,
                0,
                ResourceFlags.AllowDepthStencil),
            ResourceStates.DepthWrite,
            new ClearValue
            {
                Format = settings.Graphics.DepthStencilFormat,
                DepthStencil = new DepthStencilValue
                {
                    Depth = settings.Graphics.DepthClearValue,
                    Stencil = 0
                }
            });
        state.DepthBuffer.Name = "DepthBuffer";
        state.Device.CreateDepthStencilView(state.DepthBuffer, new DepthStencilViewDescription
        {
            Format = settings.Graphics.DepthStencilFormat,
            ViewDimension = DepthStencilViewDimension.Texture2D,
            Flags = DepthStencilViewFlags.None,
            Texture2D = new Texture2DDepthStencilView
            {
                MipSlice = 0
            }
        }, state.DsvDescriptorHeap.GetCPUDescriptorHandleForHeapStart());

        {
            state.Device.CreateRootSignature(new VersionedRootSignatureDescription
            (
                new RootSignatureDescription1
                {
                    Flags = RootSignatureFlags.AllowInputAssemblerInputLayout,
                    Parameters = new[] {
                    new RootParameter1(RootParameterType.ConstantBufferView, new RootDescriptor1(HeapConfig.BaseRegister.modelData, HeapConfig.RegisterSpace.modelData, RootDescriptorFlags.None), ShaderVisibility.All),
                    new RootParameter1(
                        new RootDescriptorTable1(
                            new[] {
                                new DescriptorRange1(DescriptorRangeType.ConstantBufferView, -1, HeapConfig.BaseRegister.cbvs, HeapConfig.RegisterSpace.cbvs, HeapConfig.DescriptorOffsetFromStart.cbvs),
                                new DescriptorRange1(DescriptorRangeType.ShaderResourceView, -1, HeapConfig.BaseRegister.textures, HeapConfig.RegisterSpace.textures, HeapConfig.DescriptorOffsetFromStart.textures),
                                new DescriptorRange1(DescriptorRangeType.ShaderResourceView, -1, HeapConfig.BaseRegister.vertexBuffers, HeapConfig.RegisterSpace.vertexBuffers, HeapConfig.DescriptorOffsetFromStart.vertexBuffers),
                                new DescriptorRange1(DescriptorRangeType.ShaderResourceView, -1, HeapConfig.BaseRegister.surfaces, HeapConfig.RegisterSpace.surfaces, HeapConfig.DescriptorOffsetFromStart.surfaces),
                                new DescriptorRange1(DescriptorRangeType.ShaderResourceView, -1, HeapConfig.BaseRegister.instanceDatas, HeapConfig.RegisterSpace.instanceDatas, HeapConfig.DescriptorOffsetFromStart.instanceDatas),
                            }
                        )
                    , ShaderVisibility.All)
                    },
                    StaticSamplers = new StaticSamplerDescription[] {
                    new StaticSamplerDescription(
                        Filter.MinMagMipLinear
                        , TextureAddressMode.Mirror
                        , TextureAddressMode.Mirror
                        , TextureAddressMode.Mirror
                        , 0.0f
                        , 1
                        , ComparisonFunction.Never
                        , StaticBorderColor.OpaqueWhite
                        , 0.0f
                        , D3D12.Float32Max
                        , HeapConfig.BaseRegister.staticSamplers
                        , HeapConfig.RegisterSpace.staticSamplers
                        , ShaderVisibility.Pixel
                    )
                    },
                }
            ), out ID3D12RootSignature? rootSignature);
            if (rootSignature == null)
                return Result.Fail("Couldn't create root signature");
            state.RootSignature = rootSignature;
            state.RootSignature.Name = "RootSignature";
        }

        state._frameFence = state.Device.CreateFence();
        state._frameFence.Name = "FrameFence";
        state._frameFenceEvent = new AutoResetEvent(false);

        state._fence = state.Device.CreateFence();
        state._fence.Name = "Fence";
        state._fenceEvent = new AutoResetEvent(false);

        state.livePsos = new List<PSO>();

        factory.Dispose();
        return Result.Ok(state);
    }

    public void WaitUntilIdle()
    {
        CommandQueue.Signal(_fence, ++fenceCount);
        _fence.SetEventOnCompletion(fenceCount, _fenceEvent);
        _fenceEvent.WaitOne();
    }

    public void EndFrameAndWait()
    {
        CommandQueue.Signal(_frameFence, ++frameCount);
        _frameFence.SetEventOnCompletion(frameCount, _frameFenceEvent);
        _frameFenceEvent.WaitOne();
    }
}