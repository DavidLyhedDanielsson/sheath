using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Vortice.Direct3D12;

namespace ConsoleApp1.Graphics;

internal struct Heap
{
    public required ID3D12Heap ID3D12Heap { get; init; }
    public readonly required ulong Size { get; init; }
    public required ulong Used { get; set; }
    public ulong PaddedSpace { get; set; }

    public static Heap New(ID3D12Heap heap, ulong size)
    {
        return new Heap { ID3D12Heap = heap, Size = size, Used = 0, PaddedSpace = 0 };
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
}

internal struct DescriptorHeapSegment
{
    public readonly required int Size { get; init; }
    public readonly required CpuDescriptorHandle BaseHandle { get; init; }
    public readonly required int HandleSize { get; init; }
    public int Used { get; set; }

    public CpuDescriptorHandle NextCpuHandle()
    {
        return BaseHandle.Offset(Used++, HandleSize);
    }
}

internal struct DescriptorHeap
{
    public readonly ID3D12DescriptorHeap ID3D12DescriptorHeap { get; init; }
    public readonly int size { get; init; }
    public readonly DescriptorHeapSegment[] segments { get; init; }

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
                size = _segments.Sum(),
                segments = _segments.Select((segment, i) => new DescriptorHeapSegment
                {
                    Size = segment,
                    BaseHandle = _heap.GetCPUDescriptorHandleForHeapStart().Offset(_segments.Take(i).Sum(), _handleSize),
                    HandleSize = _handleSize,
                    Used = 0,
                }).ToArray()
            };
        }
    }
}

public class HeapState
{
    internal LinearUploader uploadBuffer; // TODO: Not a fan of this name
    internal Heap uploadHeap;
    internal Heap vertexHeap;
    internal Heap indexHeap;
    internal Heap textureHeap;
    internal Heap constantBufferHeap;
    internal DescriptorHeap cbvUavSrvDescriptorHeap;
    // TODO: Unused?
    internal List<ID3D12Resource> vertexBuffers;
    internal List<ID3D12Resource> indexBuffers;
    internal List<ID3D12Resource> textures;
    internal List<ID3D12Resource> surfaces;
    internal List<VIBufferView> viBufferViews;
}