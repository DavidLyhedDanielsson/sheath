using ConsoleApp1.Asset;
using ConsoleApp1.Models;
using FluentResults;
using Vortice.Direct3D12;
using Vortice.DXGI;
using static System.Runtime.InteropServices.Marshal;

namespace ConsoleApp1.Graphics;

public interface IResourceBuilder
{
    public static abstract VIBufferView CreateVertexIndexBuffer(GraphicsState graphicsState, HeapState heapState, Mesh mesh);
    public static abstract TextureID CreateTexture(GraphicsState graphicsState, HeapState heapState, Texture texture);
    public static abstract int CreateSurface(HeapState heapState, Surface surface);
    public static abstract Result<HeapState> CreateHeapState(GraphicsState graphicsState);
}

public class LinearResourceBuilder : IResourceBuilder
{
    /*public const int descriptorHeapCBV = 0;
    public const int descriptorHeapTexture = 1;
    public const int descriptorHeapVertex = 2;
    public const int descriptorHeapSurface = 3;*/

    public static VIBufferView CreateVertexIndexBuffer(
        GraphicsState graphicsState
        , HeapState heapState
        , Mesh mesh)
    {
        ulong totalVertexCount = (ulong)mesh.Vertices.Length;
        ulong totalIndexCount = (ulong)mesh.Submeshes.Sum(submesh => submesh.Indices.Length);

        ulong vertexByteSize = (ulong)SizeOf(typeof(Vertex));
        ulong verticesByteSize = vertexByteSize * totalVertexCount;

        ulong indexByteSize = (ulong)SizeOf(typeof(uint));
        ulong indicesByteSize = indexByteSize * totalIndexCount;

        var device = graphicsState.device;

        ID3D12Resource vertexBuffer = heapState.vertexHeap.AppendBuffer(device, verticesByteSize);
        device.CreateShaderResourceView(vertexBuffer, new ShaderResourceViewDescription
        {
            Format = Format.Unknown,
            ViewDimension = ShaderResourceViewDimension.Buffer,
            Shader4ComponentMapping = ShaderComponentMapping.Default,
            Buffer = new BufferShaderResourceView
            {
                FirstElement = 0,
                Flags = BufferShaderResourceViewFlags.None,
                NumElements = checked((int)totalVertexCount),
                StructureByteStride = checked((int)vertexByteSize),
            },
        }, heapState.cbvUavSrvDescriptorHeap.Segments[HeapConfig.Segments.vertexBuffers].NextCpuHandle()
        );

        ID3D12Resource indexBuffer = heapState.indexHeap.AppendBuffer(device, indicesByteSize);

        unsafe
        {
            fixed (void* source = &mesh.Vertices[0])
            {
                heapState.uploadBuffer.QueueBufferUpload(graphicsState.commandList, vertexBuffer, 0, source, verticesByteSize);
            }

            ulong offset = 0;
            foreach (var submesh in mesh.Submeshes)
            {
                ulong submeshIndicesByteSize = (ulong)submesh.Indices.Length * indexByteSize;
                fixed (void* source = &submesh.Indices[0])
                {
                    heapState.uploadBuffer.QueueBufferUpload(graphicsState.commandList, indexBuffer, offset, source, submeshIndicesByteSize);
                }
                offset += submeshIndicesByteSize;
            }
        }

        // TODO: Can't be here if the data is queued for copying
        graphicsState.commandList.ResourceBarrierTransition(
            vertexBuffer
            , ResourceStates.CopyDest
            , ResourceStates.AllShaderResource
        );
        graphicsState.commandList.ResourceBarrierTransition(
            indexBuffer
            , ResourceStates.CopyDest
            , ResourceStates.IndexBuffer
        );

        return new VIBufferView
        {
            VertexBuffer = vertexBuffer,
            IndexBuffer = indexBuffer,
            IndexStart = 0,
            IndexCount = checked((int)totalIndexCount),
            IndexBufferTotalCount = checked((int)totalIndexCount),
        };
    }

    public static TextureID CreateTexture(GraphicsState graphicsState, HeapState heapState, Texture texture)
    {
        ID3D12Resource resource = heapState.textureHeap.AppendTexture2D(
            graphicsState.device
            , ResourceDescription.Texture2D(Format.R8G8B8A8_UNorm, (uint)texture.Width, (uint)texture.Height, 1, 1)
        );

        int textureId = heapState.cbvUavSrvDescriptorHeap.Segments[HeapConfig.Segments.textures].Used;
        graphicsState.device.CreateShaderResourceView(resource,
            new ShaderResourceViewDescription
            {
                Format = Format.R8G8B8A8_UNorm,
                ViewDimension = ShaderResourceViewDimension.Texture2D,
                Shader4ComponentMapping = ShaderComponentMapping.Default,
                Texture2D = new Texture2DShaderResourceView
                {
                    MostDetailedMip = 0,
                    MipLevels = 1,
                    PlaneSlice = 0,
                    ResourceMinLODClamp = 0.0f,
                },
            },
            heapState.cbvUavSrvDescriptorHeap.Segments[HeapConfig.Segments.textures].NextCpuHandle()
        );

        unsafe
        {
            heapState.uploadBuffer.QueueTextureUpload(graphicsState.commandList, resource, texture);
        }

        // TODO: Can't be here if the data is queued for copying
        graphicsState.commandList.ResourceBarrierTransition(
            resource
            , ResourceStates.CopyDest
            , ResourceStates.AllShaderResource
        );

        return new TextureID { ID = textureId };
    }

    public static int CreateSurface(HeapState heapState, Surface surface)
    {
        throw new NotImplementedException();
    }

    public static Result<HeapState> CreateHeapState(GraphicsState graphicsState)
    {
        ID3D12DescriptorHeap id3d12DescriptorHeap = graphicsState.device.CreateDescriptorHeap(
            new DescriptorHeapDescription(
                DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView
                , HeapConfig.ArraySize.total
                , DescriptorHeapFlags.ShaderVisible
            )
        );

        int handleSize = graphicsState.device.GetDescriptorHandleIncrementSize(DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView);
        var descriptorHeap = new DescriptorHeap.Builder(id3d12DescriptorHeap, handleSize)
            .WithSegment(HeapConfig.ArraySize.cbvs)
            .WithSegment(HeapConfig.ArraySize.textures)
            .WithSegment(HeapConfig.ArraySize.vertexBuffers)
            .WithSegment(HeapConfig.ArraySize.surfaces)
            .Build();

        const int uploadHeapSize = 256 * 1024 * 1024;
        ID3D12Heap uploadHeap =
            graphicsState.device.CreateHeap<ID3D12Heap>(
                new HeapDescription(uploadHeapSize, HeapType.Upload)
            );
        ID3D12Resource uploadBuffer = graphicsState.device.CreatePlacedResource<ID3D12Resource>(
            uploadHeap
            , 0
            , ResourceDescription.Buffer(uploadHeapSize)
            , ResourceStates.Common
        );

        const ulong vertexHeapSize = 1024 * 1024 * 1024;
        Heap vertexHeap = Heap.New(
            graphicsState.device.CreateHeap<ID3D12Heap>(
                new HeapDescription(vertexHeapSize, HeapType.Default)
            )
            , vertexHeapSize
        );

        const ulong indexHeapSize = 512 * 1024 * 1024;
        Heap indexHeap = Heap.New(
            graphicsState.device.CreateHeap<ID3D12Heap>(
                new HeapDescription(indexHeapSize, HeapType.Default)
            )
            , indexHeapSize
        );

        const ulong textureHeapSize = 2048L * 1024 * 1024;
        Heap textureHeap = Heap.New(
            graphicsState.device.CreateHeap<ID3D12Heap>(
                new HeapDescription(textureHeapSize, HeapType.Default)
            )
            , textureHeapSize
        );

        const ulong constantBufferHeapSize = 64 * 1024 * 1024;
        Heap constantBufferHeap = Heap.New(
            graphicsState.device.CreateHeap<ID3D12Heap>(
                new HeapDescription(constantBufferHeapSize, HeapType.Default)
            )
            , constantBufferHeapSize
        );

        return new HeapState
        {
            uploadBuffer = new LinearUploader(uploadBuffer, uploadHeapSize),
            uploadHeap = Heap.New(uploadHeap, uploadHeapSize),
            vertexHeap = vertexHeap,
            indexHeap = indexHeap,
            textureHeap = textureHeap,
            constantBufferHeap = constantBufferHeap,
            cbvUavSrvDescriptorHeap = descriptorHeap,
        };
    }
}