using FluentResults;
using StbiSharp;

namespace Application.Asset;

using System.Diagnostics;

//using Assimp;
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

        Assimp.MaterialProperty? gltfAlphaMode = material.GetProperty("$mat.gltf.alphaMode,0,0");
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

        // Meshes with submeshes are named Mesh-0/Mesh-1/Mesh-2. Group them
        // together before calling `Addmesh`.
        scene.Meshes.Sort((lhs, rhs) => string.Compare(lhs.Name, rhs.Name, StringComparison.Ordinal));

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

    public static void ImportHeightmap(AssetCatalogue catalogue, string file)
    {
        //Result<TextureData> textureRes = CreateTexture("", file);
        byte[] fileBytes = File.ReadAllBytes(file);

        var sideLength = (int)Math.Sqrt(fileBytes.Length / 2);
        var heights = new ushort[sideLength, sideLength];

        ReadOnlySpan<byte> span = fileBytes;
        for (int y = 0; y < sideLength; ++y)
        {
            for (int x = 0; x < sideLength; ++x)
                heights[y, x] = BitConverter.ToUInt16(span.Slice((y * sideLength + x) * 2, 2));
        }

        // if (!textureRes.IsSuccess)
        // {
        //     Console.Error.Write("Couldn't load file at ");
        //     Console.Error.WriteLine(file);
        // }

        float minH = 9999999;
        float maxH = -999999;

        //TextureData texture = textureRes.Value;
        var GetVertexAt = (int x, int y) =>
        {
            float height = heights[Math.Clamp(y, 0, sideLength - 1), Math.Clamp(x, 0, sideLength - 1)];

            //float minHeight = 121.0f;
            //float maxHeight = 193.0f;

            //float transformedHeight = (height - minHeight) / (maxHeight - minHeight);

            minH = Math.Min(minH, height);
            maxH = Math.Max(maxH, height);

            return new Vector3D<float>(
                x,
                height,
                y);
        };
        List<Vector3D<float>> adjacent = new(4);

        var vertices = new Vertex[sideLength * sideLength];
        for (int y = 0; y < sideLength; ++y)
        {
            for (int x = 0; x < sideLength; ++x)
            {
                adjacent.Clear();

                Vector3D<float> normal = Vector3D<float>.Zero;

                adjacent.Add(GetVertexAt(x - 1, y));
                adjacent.Add(GetVertexAt(x, y + 1));
                adjacent.Add(GetVertexAt(x + 1, y));
                adjacent.Add(GetVertexAt(x, y - 1));

                Vector3D<float> vertexPosition = GetVertexAt(x, y);

                var adj0 = adjacent[^1];
                var adj1 = adjacent[0];

                normal += Vector3D.Cross(adj0 - vertexPosition, adj1 - vertexPosition);

                for (int i = 0; i < adjacent.Count - 1; ++i)
                {
                    adj0 = adjacent[i];
                    adj1 = adjacent[i + 1];

                    normal += Vector3D.Cross(adj0 - vertexPosition, adj1 - vertexPosition);
                }

                Debug.Assert(normal.LengthSquared != 0.0f);
                if (normal.LengthSquared > 0.0f)
                    normal = Vector3D.Normalize(normal);

                // interpolate normal between steps

                vertices[y * sideLength + x] = new Vertex
                {
                    Position = vertexPosition,
                    Normal = normal,
                    TextureCoordinates = new()
                    {
                        X = 0.0f,
                        Y = 0.0f,
                    },
                };
            }
        }

        var widthM = sideLength - 1;
        var heightM = sideLength - 1;

        var indices = new uint[heightM * widthM * 6];
        for (int y = 0; y < heightM; ++y)
        {
            for (int x = 0; x < widthM; ++x)
            {
                int indexOffset = (y * widthM + x) * 6;

                indices[indexOffset + 0] = (uint)(y * sideLength + x);
                indices[indexOffset + 1] = (uint)((y + 1) * sideLength + x);
                indices[indexOffset + 2] = (uint)(y * sideLength + (x + 1));

                indices[indexOffset + 3] = (uint)((y + 1) * sideLength + x);
                indices[indexOffset + 4] = (uint)((y + 1) * sideLength + (x + 1));
                indices[indexOffset + 5] = (uint)(y * sideLength + (x + 1));
            }
        }

        catalogue.AddVertexData(new VertexData()
        {
            Name = file,
            Vertices = vertices,
            Submeshes = new[] {
                new Submesh() {
                    Indices = indices,
                }
            },
        });
    }
}
