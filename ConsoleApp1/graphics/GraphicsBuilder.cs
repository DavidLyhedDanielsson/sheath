using ConsoleApp1.Asset;
using Vortice.Direct3D12;
using Vortice.DXGI;
using static System.Runtime.InteropServices.Marshal;

namespace ConsoleApp1.Graphics;

public static class GraphicsBuilder
{
    public class VertexIndexBuilder
    {
        public required GraphicsState GraphicsState { get; init; }
        public required PSOConfig PsoConfig { get; init; }
        private List<Mesh> _meshes = new();

        public VertexIndexBuilder AddMesh(Mesh mesh)
        {
            _meshes.Add(mesh);
            return this;
        }

        public List<VIBufferView> Build()
        {
            var totalVertexCount = _meshes.Sum(mesh => mesh.Vertices.Length);
            var totalIndexCount = _meshes.Sum(mesh => mesh.Submeshes.Sum(submesh => submesh.Indices.Length));

            int vertexDataSize = SizeOf(typeof(Vertex)) * totalVertexCount;
            // uint = 32
            int indexDataSize = 32 * totalIndexCount;

            var device = GraphicsState.device;

            ID3D12Resource uploadVertexBuffer = device.CreateCommittedResource(
                HeapType.Upload,
                HeapFlags.None,
                ResourceDescription.Buffer(vertexDataSize),
                ResourceStates.CopySource);
            ID3D12Resource uploadIndexBuffer = device.CreateCommittedResource(
                HeapType.Upload,
                HeapFlags.None,
                ResourceDescription.Buffer(indexDataSize),
                ResourceStates.CopySource);

            ID3D12Resource vertexBuffer = device.CreateCommittedResource(
                HeapType.Default,
                HeapFlags.None,
                ResourceDescription.Buffer(vertexDataSize),
                ResourceStates.CopyDest);
            ID3D12Resource indexBuffer = device.CreateCommittedResource(
                HeapType.Default,
                HeapFlags.None,
                ResourceDescription.Buffer(indexDataSize),
                ResourceStates.CopyDest);

            List<VIBufferView> bufferViews = new(_meshes.Count);

            int vertexCountOffset = 0;
            int indexByteOffset = 0;
            int indexCountOffset = 0;
            foreach (Mesh mesh in _meshes)
            {
                var meshIndexCount = mesh.Submeshes.Sum(submesh => submesh.Indices.Length);
                var indices = new int[meshIndexCount];
                for (int submeshI = 0, indexI = 0; submeshI < mesh.Submeshes.Length; ++submeshI)
                {
                    for (int i = 0; i < mesh.Submeshes[submeshI].Indices.Length; ++i)
                        indices[indexI++] = mesh.Submeshes[submeshI].Indices[i] + indexCountOffset;
                }

                bufferViews.Add(new VIBufferView
                {
                    VertexBuffer = vertexBuffer,
                    IndexBuffer = indexBuffer,
                    IndexBufferOffset = indexCountOffset,
                });

                uploadVertexBuffer.SetData(indices, indexByteOffset);

                indexByteOffset += SizeOf(typeof(int)) * meshIndexCount;
                indexCountOffset += meshIndexCount;

                uploadVertexBuffer.SetData(mesh.Vertices.ToArray(), vertexCountOffset);
                vertexCountOffset += SizeOf(typeof(Vertex)) * mesh.Vertices.Length;
            }

            GraphicsState.commandList.CopyResource(vertexBuffer, uploadVertexBuffer);
            GraphicsState.commandList.CopyResource(indexBuffer, uploadIndexBuffer);

            return bufferViews;
        }
    }

    public class TextureBuilder
    {
        public required GraphicsState GraphicsState { get; init; }
        private List<Texture> _textures = new();

        public TextureBuilder AddTexture(Texture texture)
        {
            _textures.Add(texture);
            return this;
        }

        public void Build()
        {
            var device = GraphicsState.device;
            int totalByteSize = _textures.Sum(texture => texture.Width * texture.Height * 4);

            List<ResourceDescription> resourceDescriptions = new(_textures.Count);
            foreach (Texture texture in _textures)
            {
                resourceDescriptions.Add(
                    ResourceDescription.Texture2D(
                        Format.R8G8B8A8_UNorm
                        , (uint)texture.Width
                        , (uint)texture.Height
                ));
            }

            ResourceAllocationInfo allocationInfo = device.GetResourceAllocationInfo(resourceDescriptions.ToArray());

            ID3D12Resource uploadBuffer = device.CreateCommittedResource(HeapType.Upload, ResourceDescription.Buffer(totalByteSize), ResourceStates.CopySource);
            ID3D12Heap heap = device.CreateHeap<ID3D12Heap>(new HeapDescription(allocationInfo.SizeInBytes, HeapType.Default));

            unsafe
            {
                byte* uploadBufferData;
                uploadBuffer.Map(0, (void**)&uploadBufferData);
                int uploadBufferOffset = 0;

                List<ID3D12Resource> resources = new(_textures.Count);
                int heapOffset = 0;

                // TODO: Wrap desc heap in class and remove this
                // CBV = 0-1023, SRV = 1024-2047
                int textureI = 1024;
                foreach (Texture texture in _textures)
                {
                    // TODO: Use GetCopyableFootprints?
                    int pitchWidth = texture.Width * 4;
                    if (pitchWidth % D3D12.TextureDataPitchAlignment != 0)
                        throw new NotImplementedException("Not yet :(");

                    var resource = device.CreatePlacedResource<ID3D12Resource>(
                        heap
                        , (ulong)heapOffset
                        , ResourceDescription.Texture2D(Format.R8G8B8A8_UNorm, (uint)texture.Width, (uint)texture.Height, 1, 1)
                        , ResourceStates.CopyDest);

                    int byteSize = texture.Width * texture.Height * 4;

                    resources.Add(resource);

                    int alignedByteSize = (int)(MathF.Round(byteSize / (float)allocationInfo.Alignment) * allocationInfo.Alignment);
                    heapOffset += alignedByteSize;

                    fixed (byte* source = &texture.Texels[0])
                    {
                        Buffer.MemoryCopy(source, uploadBufferData + uploadBufferOffset, totalByteSize, byteSize);
                    }
                    GraphicsState.commandList.CopyTextureRegion(
                        new TextureCopyLocation(resource)
                        , 0
                        , 0
                        , 0
                        , new TextureCopyLocation(
                            uploadBuffer
                            , new PlacedSubresourceFootPrint()
                            {
                                Offset = (ulong)uploadBufferOffset,
                                Footprint = new SubresourceFootPrint(Format.R8G8B8A8_UNorm, texture.Width, texture.Height, 1, texture.Width * 4)
                            }
                        ));
                    uploadBufferOffset += byteSize;

                    device.CreateShaderResourceView(resource, new ShaderResourceViewDescription()
                    {
                        Format = Format.R8G8B8A8_UNorm,
                        ViewDimension = ShaderResourceViewDimension.Texture2D,
                        Shader4ComponentMapping = ShaderComponentMapping.Default,
                        Texture2D = new Texture2DShaderResourceView()
                        {
                            MostDetailedMip = 0,
                            MipLevels = 1,
                            PlaneSlice = 0,
                            ResourceMinLODClamp = 0.0f,
                        }
                    }, GraphicsState.cbvUavSrvDescriptorHeap.GetCPUDescriptorHandleForHeapStart() + textureI * GraphicsState.cbvUavSrvDescriptorSize);
                    textureI++;
                }


                GraphicsState.commandList.ResourceBarrier(
                    resources.Select(resource => ResourceBarrier.BarrierTransition(
                        resource
                        , ResourceStates.CopyDest
                        , ResourceStates.PixelShaderResource
                    )).ToArray()
                );

                uploadBuffer.Unmap(0);
            }
        }
    }

    public class SurfaceBuilder
    {
        public required GraphicsState GraphicsState { get; init; }
        private List<Material> _materials = new();

        public SurfaceBuilder AddMaterial(Material material)
        {
            _materials.Add(material);
            return this;
        }

        public void Build()
        {

        }
    }

    public static VertexIndexBuilder CreateVertexIndexBuffers(GraphicsState state, PSOConfig psoConfig)
    {
        return new VertexIndexBuilder() { GraphicsState = state, PsoConfig = psoConfig };
    }

    public static TextureBuilder CreateTextures(GraphicsState state)
    {
        return new TextureBuilder() { GraphicsState = state };
    }

    public static SurfaceBuilder CreateSurfaces(GraphicsState state)
    {
        return new SurfaceBuilder() { GraphicsState = state };
    }
}