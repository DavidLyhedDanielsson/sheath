using FluentResults;
using StbiSharp;

namespace ConsoleApp1.Asset;

using Assimp;
using Silk.NET.Maths;

public static class AssimpExtensions
{
    public static Vector2D<float> ToVector2(this Assimp.Vector2D vec)
    {
        return new Vector2D<float> { X = vec.X, Y = vec.Y };
    }
    public static Vector2D<float> ToVector2(this Assimp.Vector3D vec)
    {
        return new Vector2D<float> { X = vec.X, Y = vec.Y };
    }
    public static Vector3D<float> ToVector3(this Assimp.Vector3D vec)
    {
        return new Vector3D<float> { X = vec.X, Y = vec.Y, Z = vec.Z };
    }
}

public class AssetLoader
{
    private class MeshWithMaterial
    {
        public VertexData Mesh { get; init; }
        public string[] SubmeshMaterials { get; init; }
    }

    private static MeshWithMaterial CreateMesh(List<Assimp.Mesh> meshes, List<Assimp.Material> materials)
    {
        var submeshMaterials = new string[meshes.Count];
        var submeshes = new Submesh[meshes.Count];

        var vertexCount = meshes.Sum(mesh => mesh.VertexCount);
        var vertices = new Vertex[vertexCount];

        // All submesh vertices and indices will be placed in a single array respectively
        int vertexOffset = 0;
        uint indexOffset = 0;

        for (int meshI = 0; meshI < meshes.Count; ++meshI)
        {
            var mesh = meshes[meshI];
            for (int i = 0; i < mesh.VertexCount; ++i, ++vertexOffset)
            {
                vertices[vertexOffset] = new Vertex()
                {
                    Position = mesh.Vertices[i].ToVector3(),
                    Normal = mesh.Normals[i].ToVector3(),
                    TextureCoordinates = mesh.TextureCoordinateChannels[0][i].ToVector2(),
                };
            }

            uint[] indices = mesh.GetUnsignedIndices();
            for (var i = 0; i < indices.Length; i++)
                indices[i] += indexOffset;
            indexOffset += (uint)mesh.VertexCount;

            submeshes[meshI] = new Submesh()
            {
                Indices = indices,
            };
            submeshMaterials[meshI] = materials[mesh.MaterialIndex].Name;
        }

        return new MeshWithMaterial()
        {
            Mesh = new VertexData()
            {
                Name = meshes[0].Name.Split('-')[0],
                Vertices = vertices,
                Submeshes = submeshes,
            },
            SubmeshMaterials = submeshMaterials,
        };
    }

    private static Material CreateMaterial(AssetCatalogue assetCatalogue, Assimp.Material material)
    {
        TextureData? diffuseTexture = assetCatalogue.GetTextureData(material.TextureDiffuse.FilePath);

        if (diffuseTexture == null)
            throw new NotImplementedException("Nooooooo not yet :(");


        bool hasAlpha = false;

        MaterialProperty? gltfAlphaMode = material.GetProperty("$mat.gltf.alphaMode,0,0");
        if (gltfAlphaMode != null && gltfAlphaMode.GetStringValue() == "MASK")
        {
            hasAlpha = true;
        }

        return new Material()
        {
            Name = material.Name,
            AlbedoTexture = diffuseTexture.FilePath,
            AlbedoTextureHasAlpha = hasAlpha,
        };
    }

    private static Result<TextureData> CreateTexture(string rootPath, string texturePath)
    {
        using (var file = File.OpenRead(Path.Combine(rootPath, texturePath)))
        using (var stream = new MemoryStream())
        {
            try
            {
                file.CopyTo(stream);
                Stbi.InfoFromMemory(stream, out int width, out int height, out int channelCount);
                StbiImage image = Stbi.LoadFromMemory(stream, 4);

                return Result.Ok(new TextureData()
                {
                    FilePath = texturePath,
                    Texels = image.Data.ToArray(),
                    Width = image.Width,
                    Height = image.Height,
                });
            }
            catch (ArgumentException ex)
            {
                return Result.Fail(ex.Message);
            }
        }
    }

    public static void Import(AssetCatalogue catalogue, string file)
    {
        var importer = new Assimp.AssimpContext();
        var scene = importer.ImportFile(file, Assimp.PostProcessPreset.TargetRealTimeQuality);

        foreach (var material in scene.Materials)
        {
            if (!catalogue.HasTexture(material.TextureDiffuse.FilePath))
            {
                Result<TextureData> texture = CreateTexture(Path.GetDirectoryName(file) ?? string.Empty, material.TextureDiffuse.FilePath);

                if (texture.IsSuccess)
                    catalogue.AddTexture(texture.Value);
                else
                {
                    Console.Error.Write("Couldn't load file at ");
                    Console.Error.WriteLine(file);
                }
            }
        }

        foreach (var material in scene.Materials)
            catalogue.AddMaterial(CreateMaterial(catalogue, material));

        scene.Meshes.Sort((lhs, rhs) => string.Compare(lhs.Name, rhs.Name, StringComparison.Ordinal));

        // Meshes with submeshes are named Mesh-0/Mesh-1/Mesh-2. Group them
        // together before calling `Addmesh`.
        List<Assimp.Mesh> meshes = new(10);
        foreach (Assimp.Mesh mesh in scene.Meshes)
        {
            if (meshes.Count == 0)
                meshes.Add(mesh);
            else
            {
                string currentName = meshes[0].Name.Split('-')[0];
                string meshName = mesh.Name.Split('-')[0];

                if (currentName != meshName)
                {
                    MeshWithMaterial meshWithMaterial = CreateMesh(meshes, scene.Materials);
                    catalogue.AddVertexData(meshWithMaterial.Mesh);
                    catalogue.AddDefaultMaterial(meshWithMaterial.Mesh.Name, meshWithMaterial.SubmeshMaterials);
                    meshes.Clear();
                }

                meshes.Add(mesh);
            }
        }

        if (meshes.Count > 0)
        {
            MeshWithMaterial meshWithMaterial = CreateMesh(meshes, scene.Materials);
            catalogue.AddVertexData(meshWithMaterial.Mesh);
            catalogue.AddDefaultMaterial(meshWithMaterial.Mesh.Name, meshWithMaterial.SubmeshMaterials);
            meshes.Clear();
        }
    }
}
