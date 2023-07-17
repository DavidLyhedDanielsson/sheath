using System.Diagnostics;
using ConsoleApp1.Asset;
using ConsoleApp1.Models;

namespace ConsoleApp1.Graphics;

public static class ModelBuilder
{
    public static Model CreateModel(GraphicsState graphicsState, HeapState heapState, AssetCatalogue catalogue, Mesh mesh, List<Material>[] submeshMaterials)
    {
        Debug.Assert(submeshMaterials.Length == mesh.Submeshes.Length);

        VIBufferView viBufferView = LinearResourceBuilder.CreateVertexIndexBuffer(graphicsState, heapState, mesh);

        int indexOffset = 0;

        Model.Submesh[] submeshes = new Model.Submesh[mesh.Submeshes.Length];
        for (int i = 0; i < mesh.Submeshes.Length; ++i)
        {
            Material material = submeshMaterials[i][0];

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
                    AlbedoTexture = 0,
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