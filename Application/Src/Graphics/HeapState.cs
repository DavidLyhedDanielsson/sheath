using System.Diagnostics;
using System.Dynamic;
using SharpGen.Runtime;
using Vortice.Direct3D12;

namespace Application.Graphics;

public class Heap : IDisposable
{
    public required ID3D12Heap ID3D12Heap { get; init; }
    public required ulong Size { get; init; }
    public required ulong Used { get; set; }
    public ulong PaddedSpace { get; set; }

    public static Heap New(ID3D12Heap heap, ulong size)
    {
        return new Heap { ID3D12Heap = heap, Size = size, Used = 0, PaddedSpace = 0 };
    }

    public void Dispose()
    {
        ID3D12Heap.Dispose();
    }

    public ID3D12Resource AppendBuffer(
        ID3D12Device device
        , ulong size
        , ResourceStates initialState = ResourceStates.CopyDest
    )
    {
        ID3D12Resource resource = device.CreatePlacedResource<ID3D12Resource>(
            ID3D12Heap
            , Used
            , ResourceDescription.Buffer(size)
            , initialState
        );
        resource.Name = "Heap.AppendBuffer";

        ulong alignment = D3D12.DefaultResourcePlacementAlignment;
        ulong alignedSize = (size + alignment - 1) / alignment * alignment;

        PaddedSpace += alignedSize - size;
        Used += alignedSize;

        return resource;
    }

    public ID3D12Resource AppendBuffer(
        ID3D12Device device
        , int size
        , ResourceStates initialState = ResourceStates.CopyDest
    )
    {
        Debug.Assert(size > 0);
        return AppendBuffer(device, (ulong)size, initialState);
    }

    public ID3D12Resource AppendTexture2D(
        ID3D12Device device
        , ResourceDescription resourceDescription
        , ResourceStates initialState = ResourceStates.CopyDest
    )
    {
        ResourceAllocationInfo allocationInfo = device.GetResourceAllocationInfo(
            new ResourceDescription[] { resourceDescription }
        );
        ID3D12Resource resource = device.CreatePlacedResource<ID3D12Resource>(
            ID3D12Heap
            , Used
            , resourceDescription
            , initialState
        );

        Used += allocationInfo.SizeInBytes;

        return resource;
    }
}

public class DescriptorHeapSegment
{
    public required int Size { get; init; }
    public required CpuDescriptorHandle BaseHandle { get; init; }
    public required int HandleSize { get; init; }
    public int Used { get; set; }

    public CpuDescriptorHandle NextCpuHandle()
    {
        Debug.Assert(Used < Size);
        return BaseHandle.Offset(Used++, HandleSize);
    }
}

public class DescriptorHeap : IDisposable
{
    public required ID3D12DescriptorHeap ID3D12DescriptorHeap { get; init; }
    public required int Size { get; init; }
    public required DescriptorHeapSegment[] Segments { get; init; }

    public class Builder
    {
        private ID3D12DescriptorHeap _heap;
        private int _handleSize;
        private List<int> _segments;

        public Builder(ID3D12DescriptorHeap heap, int handleSize)
        {
            _heap = heap;
            _handleSize = handleSize;
            _segments = new(8);
        }

        public Builder WithSegment(int size)
        {
            _segments.Add(size);
            return this;
        }

        public DescriptorHeap Build()
        {
            return new DescriptorHeap
            {
                ID3D12DescriptorHeap = _heap,
                Size = _segments.Sum(),
                Segments = _segments.Select((segment, i) => new DescriptorHeapSegment
                {
                    Size = segment,
                    BaseHandle = _heap.GetCPUDescriptorHandleForHeapStart().Offset(_segments.Take(i).Sum(), _handleSize),
                    HandleSize = _handleSize,
                    Used = 0,
                }).ToArray()
            };
        }
    }

    public void Dispose()
    {
        ID3D12DescriptorHeap.Dispose();
    }
}

public class HeapState : IDisposable
{
    public required LinearUploader UploadBuffer { get; init; } // TODO: Not a fan of this name
    public required Heap UploadHeap { get; init; }
    public required Heap VertexHeap { get; init; }
    public required Heap IndexHeap { get; init; }
    public required Heap TextureHeap { get; init; }
    public required DescriptorHeap CbvUavSrvDescriptorHeap { get; init; }
    public required DescriptorHeap RtvDescriptorHeap { get; init; }

    public required Heap InstanceDataHeap { get; init; }
    public required ID3D12Resource InstanceDataBuffer { get; init; }

    public required Heap PerDrawConstantBufferHeap { get; init; }
    public required ID3D12Resource PerDrawBuffer { get; init; }

    public int SurfaceCounter { get; set; }

    public HashSet<IDisposable> disposables { get; } = new();

    public void Dispose()
    {
        UploadBuffer.Dispose();
        UploadHeap.Dispose();
        VertexHeap.Dispose();
        IndexHeap.Dispose();
        TextureHeap.Dispose();
        CbvUavSrvDescriptorHeap.Dispose();
        RtvDescriptorHeap.Dispose();
        InstanceDataHeap.Dispose();
        InstanceDataBuffer.Dispose();
        PerDrawConstantBufferHeap.Dispose();
        PerDrawBuffer.Dispose();

        foreach (var disposable in disposables)
            disposable.Dispose();
    }

    public T Track<T>(T disposable) where T : IDisposable
    {
        disposables.Add(disposable);
        return disposable;
    }

    public T Track<T>(T disposable, string name) where T : ID3D12Object
    {
        disposable.Name = name;
        return Track(disposable);
    }
}