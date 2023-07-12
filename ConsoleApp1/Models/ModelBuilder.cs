using System.Diagnostics;
using ConsoleApp1.Asset;
using ConsoleApp1.Models;

namespace ConsoleApp1.Graphics;

public static class ModelBuilder
{
    // TODO: Move buffers into somewhere. Showroom? Catalogue? Graphics state?
    public static Model CreateModel(AssetCatalogue catalogue, List<GraphicsBuilder.MeshVIBuffer> buffers, Mesh mesh, List<Material>[] submeshMaterials)
    {
        Debug.Assert(submeshMaterials.Length == mesh.Submeshes.Length);

        VIBufferView? bufferView = buffers.Find(buffer => buffer.MeshName == mesh.Name)?.VIBufferView;
        Debug.Assert(bufferView != null);

        int indexOffset = 0;

        Model.Submesh[] submeshes = new Model.Submesh[mesh.Submeshes.Length];
        for (int i = 0; i < mesh.Submeshes.Length; ++i)
        {
            Material material = submeshMaterials[i][0];

            submeshes[i] = new Model.Submesh
            {
                VIBufferView = new VIBufferView
                {
                    VertexBuffer = bufferView.VertexBuffer,
                    IndexBuffer = bufferView.IndexBuffer,
                    IndexStart = bufferView.IndexStart + indexOffset,
                    IndexCount = mesh.Submeshes[i].Indices.Length,
                    IndexBufferTotalCount = bufferView.IndexBufferTotalCount,
                },
                Surface = new Surface
                {
                    AlbedoTexture = catalogue.GetTexture(material.Diffuse.FilePath)!,
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