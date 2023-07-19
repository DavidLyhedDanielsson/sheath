using System.Diagnostics;
using ConsoleApp1.Asset;
using ConsoleApp1.Models;
using Vortice.DXGI;

namespace ConsoleApp1.Graphics;

public static class ModelBuilder
{
    public static Model CreateModel(GraphicsState graphicsState, HeapState heapState, Dictionary<Texture, TextureID> textureIds, Mesh mesh, List<Material>[] submeshMaterials)
    {
        Debug.Assert(submeshMaterials.Length == mesh.Submeshes.Length);

        VIBufferView viBufferView = LinearResourceBuilder.CreateVertexIndexBuffer(graphicsState, heapState, mesh);

        int indexOffset = 0;

        Model.Submesh[] submeshes = new Model.Submesh[mesh.Submeshes.Length];
        for (int i = 0; i < mesh.Submeshes.Length; ++i)
        {
            Material material = submeshMaterials[i][0];

            Debug.Assert(textureIds.TryGetValue(material.Albedo, out TextureID? albedoTextureId));

            submeshes[i] = new Model.Submesh
            {
                VIBufferView = new VIBufferView
                {
                    VertexBuffer = viBufferView.VertexBuffer,
                    IndexBuffer = viBufferView.IndexBuffer,
                    IndexStart = viBufferView.IndexStart + indexOffset,
                    IndexCount = mesh.Submeshes[i].Indices.Length,
                    IndexBufferTotalCount = viBufferView.IndexBufferTotalCount,
                },
                Surface = new Surface
                {
                    // TODO
                    ID = 0,
                    AlbedoTexture = albedoTextureId!,
                    PSO = null,
                }
            };

            indexOffset += mesh.Submeshes[i].Indices.Length;
        }

        return new Model
        {
            Submeshes = submeshes,
        };
    }
}