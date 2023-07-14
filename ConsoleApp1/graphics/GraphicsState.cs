using FluentResults;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.Direct3D12.Debug;
using Vortice.DXGI;

namespace ConsoleApp1.Graphics;

public class GraphicsState
{
    public ID3D12Device device;
    public ID3D12CommandQueue commandQueue;
    public ID3D12CommandAllocator commandAllocator;
    public ID3D12GraphicsCommandList commandList;
    public ID3D12RootSignature rootSignature;
    public ID3D12DescriptorHeap rtvDescriptorHeap;
    public ID3D12DescriptorHeap cbvUavSrvDescriptorHeap;
    public ID3D12Resource[] renderTargets;
    public ID3D12Resource depthStencilView;
    public ID3D12Resource instanceBuffer;

    public IDXGISwapChain swapChain;

    ID3D12Fence fence;
    AutoResetEvent fenceEvent;

    ID3D12Fence frameFence;
    AutoResetEvent frameFenceEvent;

    public ulong fenceCount;
    public ulong frameCount;

    public int rtvDescriptorSize;
    public int cbvUavSrvDescriptorSize;

    private static void DebugCallback(MessageCategory category, MessageSeverity severity, MessageId id, string description)
    {
        Console.WriteLine(description);
    }

    public static Result<GraphicsState> Create(Settings settings, IntPtr hRef)
    {
        GraphicsState state = new GraphicsState();

        state.renderTargets = new ID3D12Resource[settings.Graphics.BackBufferCount];
        state.frameCount = 0;

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

        D3D12.D3D12CreateDevice(adapter, FeatureLevel.Level_12_1, out state.device);
        if (state.device == null)
            return Result.Fail("Couldn't create device");

        ID3D12InfoQueue1 infoQueue = state.device.QueryInterface<ID3D12InfoQueue1>();
        if (infoQueue != null)
            infoQueue!.RegisterMessageCallback(DebugCallback);

        state.commandQueue = state.device.CreateCommandQueue(new CommandQueueDescription(CommandListType.Direct, CommandQueuePriority.Normal));
        state.commandAllocator = state.device.CreateCommandAllocator(CommandListType.Direct);
        state.commandList = state.device.CreateCommandList<ID3D12GraphicsCommandList>(CommandListType.Direct, state.commandAllocator);

        state.swapChain = factory.CreateSwapChainForHwnd(state.commandQueue, hRef, new SwapChainDescription1
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

        state.rtvDescriptorSize = state.device.GetDescriptorHandleIncrementSize(DescriptorHeapType.RenderTargetView);
        state.rtvDescriptorHeap = state.device.CreateDescriptorHeap(new(DescriptorHeapType.RenderTargetView, settings.Graphics.BackBufferCount, DescriptorHeapFlags.None));

        state.cbvUavSrvDescriptorSize = state.device.GetDescriptorHandleIncrementSize(DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView);
        // 1024 CBVs, 1024 texture SRVs, 1024 buffer SRVs
        state.cbvUavSrvDescriptorHeap = state.device.CreateDescriptorHeap(new(DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView, 1024 * 3, DescriptorHeapFlags.ShaderVisible));

        for (int i = 0; i < settings.Graphics.BackBufferCount; ++i)
        {
            state.renderTargets[i] = state.swapChain.GetBuffer<ID3D12Resource>(i);
            state.device.CreateRenderTargetView(state.renderTargets[i], null, state.rtvDescriptorHeap.GetCPUDescriptorHandleForHeapStart() + i * state.rtvDescriptorSize);
        }

        state.device.CreateRootSignature(new VersionedRootSignatureDescription
        (
            new RootSignatureDescription1
            {
                Flags = RootSignatureFlags.AllowInputAssemblerInputLayout,
                Parameters = new[] {
                    new RootParameter1(RootParameterType.ConstantBufferView, new RootDescriptor1(0, 3, RootDescriptorFlags.None), ShaderVisibility.All),
                    new RootParameter1(
                        new RootDescriptorTable1(
                            new[] {
                                new DescriptorRange1(DescriptorRangeType.ConstantBufferView, -1, 0, 0, 0),
                                new DescriptorRange1(DescriptorRangeType.ShaderResourceView, -1, 0, 1, 1024), // Textures
                                new DescriptorRange1(DescriptorRangeType.ShaderResourceView, -1, 0, 2, 2048), // Vertex buffers and data buffers
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
                        , 0
                        , 2
                        , ShaderVisibility.Pixel
                    )
                },
            }
        ), out state.rootSignature);

        state.frameFence = state.device.CreateFence();
        state.frameFenceEvent = new AutoResetEvent(false);

        state.fence = state.device.CreateFence();
        state.fenceEvent = new AutoResetEvent(false);
        
        state.instanceBuffer = state.device.CreateCommittedResource(
            HeapType.Upload,
            HeapFlags.None,
            ResourceDescription.Buffer(64 * 1024 * 1024),
            ResourceStates.Common);
        unsafe
        {
            int[] initialData = new int[1024];
            for (int i = 0; i < 1024; ++i)
                initialData[i] = 0;
            
            byte* data;
            state.instanceBuffer.Map(0, (void**)&data);
            fixed (void* source = &initialData[0])
            {
                for (int i = 0; i < 64 * 1024 * 1024; i += 1024 * 4)
                    Buffer.MemoryCopy(source, data + i , 64 * 1024 * 1024, 1024 * 4);
            }
            state.instanceBuffer.Unmap(0);
        }
        state.commandList.ResourceBarrierTransition(state.instanceBuffer, ResourceStates.Common, ResourceStates.VertexAndConstantBuffer);

        return Result.Ok(state);
    }

    public void WaitUntilIdle()
    {
        commandQueue.Signal(fence, ++fenceCount);
        fence.SetEventOnCompletion(fenceCount, fenceEvent);
        fenceEvent.WaitOne();
    }

    public void EndFrameAndWait()
    {
        commandQueue.Signal(frameFence, ++frameCount);
        frameFence.SetEventOnCompletion(frameCount, frameFenceEvent);
        frameFenceEvent.WaitOne();
    }
}