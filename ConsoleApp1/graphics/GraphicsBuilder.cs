using ConsoleApp1.Asset;
using ConsoleApp1.Models;
using Vortice.Direct3D12;
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

        public List<Model> Build()
        {
            var vertexCount = _meshes.Sum(mesh => mesh.Vertices.Length);
            var totalIndexCount = _meshes.Sum(mesh => mesh.Submeshes.Sum(submesh => submesh.Indices.Length));

            int vertexDataSize = SizeOf(typeof(Vertex)) * vertexCount;
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

            List<Model> models = new(_meshes.Count);

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

                models.Add(new Model
                {
                    pipelineStateObject = PsoConfig.NdcTriangle,
                    vertexBuffer = vertexBuffer,
                    indexBuffer = indexBuffer,
                    indexOffset = indexCountOffset,
                });

                uploadVertexBuffer.SetData(indices, indexByteOffset);

                indexByteOffset += SizeOf(typeof(int)) * meshIndexCount;
                indexCountOffset += meshIndexCount;

                uploadVertexBuffer.SetData(mesh.Vertices.ToArray(), vertexCountOffset);
                vertexCountOffset += SizeOf(typeof(Vertex)) * mesh.Vertices.Length;
            }

            GraphicsState.commandList.CopyResource(vertexBuffer, uploadVertexBuffer);
            GraphicsState.commandList.CopyResource(indexBuffer, uploadIndexBuffer);

            return models;
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