using System.Diagnostics;
using Assimp = Silk.NET.Assimp;

namespace Application.Asset
{
    public class MeshLoader
    {
        public class MeshWithMaterial
        {
            public required VertexData Mesh { get; init; }
            public required string[] SubmeshMaterials { get; init; }
        }

        public static unsafe MeshWithMaterial CreateMeshWithMaterial(Assimp.Assimp assimp, List<Assimp.Mesh> meshes, Assimp.Material** materials)
        {
            var submeshMaterials = new string[meshes.Count];
            var submeshes = new Submesh[meshes.Count];

            var vertexCount = meshes.Sum(mesh => mesh.MNumVertices);
            var vertices = new Vertex[vertexCount];

            // All submesh vertices and indices will be placed in a single array respectively
            int vertexOffset = 0;
            uint indexOffset = 0;

            for (int meshI = 0; meshI < meshes.Count; ++meshI)
            {
                var mesh = meshes[meshI];
                for (int i = 0; i < mesh.MNumVertices; ++i, ++vertexOffset)
                {
                    unsafe
                    {
                        vertices[vertexOffset] = new Vertex()
                        {
                            Position = mesh.MVertices[i].ToGeneric(),
                            Normal = mesh.MNormals[i].ToGeneric(),
                            Tangent = mesh.MTangents[i].ToGeneric(),
                            TextureCoordinates = new(mesh.MTextureCoords[0][i].X, mesh.MTextureCoords[0][i].Y),
                        };
                    }
                }

                uint[] indices = new uint[mesh.MNumFaces * 3];
                for (int i = 0, counter = 0; i < mesh.MNumFaces; i++)
                {
                    unsafe
                    {
                        Debug.Assert(mesh.MFaces[i].MNumIndices == 3);
                        indices[counter++] = mesh.MFaces[i].MIndices[0] + indexOffset;
                        indices[counter++] = mesh.MFaces[i].MIndices[1] + indexOffset;
                        indices[counter++] = mesh.MFaces[i].MIndices[2] + indexOffset;
                    }
                }
                indexOffset += (uint)mesh.MNumFaces * 3;

                submeshes[meshI] = new Submesh()
                {
                    Indices = indices,
                };
                unsafe
                {
                    Assimp.AssimpString materialName;
                    assimp.GetMaterialString(materials[mesh.MMaterialIndex], Assimp.Assimp.MaterialNameBase, 0, 0, &materialName);
                    submeshMaterials[meshI] = materialName.AsString;
                }
            }

            return new MeshWithMaterial()
            {
                Mesh = new VertexData()
                {
                    Name = meshes[0].MName.AsString.Split('-')[0],
                    Vertices = vertices,
                    Submeshes = submeshes,
                },
                SubmeshMaterials = submeshMaterials,
            };
        }
    }
}
