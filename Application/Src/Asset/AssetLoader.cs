using FluentResults;
using StbiSharp;
using Assimp = Silk.NET.Assimp;
using System.Diagnostics;
using Silk.NET.Maths;
using Silk.NET.Assimp;
using File = System.IO.File;
using System.Numerics;

namespace Application.Asset;

/*public static class AssimpExtensions
{
    public static Vector2D<float> ToVector2(this Vector2D vec)
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
}*/

public class AssetLoader
{
    private class MeshWithMaterial
    {
        public VertexData Mesh { get; init; }
        public string[] SubmeshMaterials { get; init; }
    }

    private static unsafe MeshWithMaterial CreateMesh(Assimp.Assimp assimp, List<Assimp.Mesh> meshes, Assimp.Material** materials)
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
                AssimpString materialName;
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

    private static unsafe Material CreateMaterial(Assimp.Assimp assimp, AssetCatalogue assetCatalogue, ref Assimp.Material material)
    {
        TextureData? diffuseTexture;
        {
            AssimpString aiTexturePath;
            assimp.GetMaterialTexture(material, Assimp.TextureType.BaseColor, 0, &aiTexturePath, null, null, null, null, null, null);
            diffuseTexture = assetCatalogue.GetTextureData(aiTexturePath.AsString);
        }
        if (diffuseTexture == null)
            throw new NotImplementedException("Nooooooo not yet :(");

        TextureData? normalTexture;
        {
            AssimpString aiTexturePath;
            assimp.GetMaterialTexture(material, Assimp.TextureType.Normals, 0, &aiTexturePath, null, null, null, null, null, null);
            normalTexture = assetCatalogue.GetTextureData(aiTexturePath.AsString);
        }
        if (normalTexture == null)
            throw new NotImplementedException("Nooooooo not yet :(");

        TextureData? ormTexture;
        {
            AssimpString aiTexturePath;
            assimp.GetMaterialTexture(material, Assimp.TextureType.DiffuseRoughness, 0, &aiTexturePath, null, null, null, null, null, null);
            ormTexture = assetCatalogue.GetTextureData(aiTexturePath.AsString);
        }
        if (ormTexture == null)
            throw new NotImplementedException("Nooooooo not yet :(");

        bool hasAlpha = false;


        Assimp.AssimpString gltfAlphaMode;
        assimp.GetMaterialString(material, "$mat.gltf.alphaMode", 0, 0, &gltfAlphaMode);
        if (gltfAlphaMode == "MASK")
        {
            hasAlpha = true;
        }

        Assimp.AssimpString materialName;
        assimp.GetMaterialString(material, Assimp.Assimp.MaterialNameBase, 0, 0, &materialName);

        return new Material()
        {
            Name = materialName.AsString,
            AlbedoTexture = diffuseTexture.FilePath,
            AlbedoTextureHasAlpha = hasAlpha,
            NormalTexture = normalTexture.FilePath,
            ORMTexture = ormTexture.FilePath,
        };
    }

    public static Result<TextureData> CreateTexture(string rootPath, string texturePath)
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
                    Channels = 4,
                });
            }
            catch (ArgumentException ex)
            {
                return Result.Fail(ex.Message);
            }
        }
    }

    [Flags]
    private enum Channel
    {
        R = 1 << 0,
        G = 1 << 1,
        B = 1 << 2,
        A = 1 << 3,
        All = R | G | B | A,
    };

    private static Result<TextureData> CreateTexture(string rootPath, string texturePath, Channel channelsToExtract)
    {
        using (var file = File.OpenRead(Path.Combine(rootPath, texturePath)))
        using (var stream = new MemoryStream())
        {
            try
            {
                file.CopyTo(stream);
                Stbi.InfoFromMemory(stream, out int width, out int height, out int channelCount);
                StbiImage image = Stbi.LoadFromMemory(stream, channelCount);

                int numberOfChannelsToExtract = BitOperations.PopCount((uint)channelsToExtract);

                Debug.Assert(channelCount >= numberOfChannelsToExtract);

                var extractMask = new byte[]
                {
                    Convert.ToByte((channelsToExtract & Channel.R) != 0),
                    Convert.ToByte((channelsToExtract & Channel.G) != 0),
                    Convert.ToByte((channelsToExtract & Channel.B) != 0),
                    Convert.ToByte((channelsToExtract & Channel.A) != 0),
                };

                var texels = new byte[width * height * numberOfChannelsToExtract];
                for(int i = 0; i < width * height; ++i)
                {
                    for(int texelI = 0, channelI = 0; texelI < numberOfChannelsToExtract && channelI < channelCount; texelI += extractMask[channelI], ++channelI)
                        texels[i * numberOfChannelsToExtract + texelI] = (byte)(extractMask[channelI] * image.Data[i * channelCount + channelI]);
                }

                return Result.Ok(new TextureData()
                {
                    FilePath = texturePath,
                    Texels = texels,
                    Width = image.Width,
                    Height = image.Height,
                    Channels = numberOfChannelsToExtract,
                });
            }
            catch (ArgumentException ex)
            {
                return Result.Fail(ex.Message);
            }
        }
    }

    private unsafe delegate void AssimpEach<T>(ref T arg) where T : unmanaged;
    private static unsafe void AssimpForEach<T>(uint count, T** type, AssimpEach<T> action) where T: unmanaged
    {
        for (int i = 0; i < count; ++i)
            action(ref *type[i]);
    }

    public static void Import(AssetCatalogue catalogue, string file)
    {
        unsafe
        {
            Assimp.Assimp assimp = Assimp.Assimp.GetApi();
            Assimp.Scene* scene;

            scene = assimp.ImportFile(file, (uint)Assimp.PostProcessPreset.TargetRealTimeQuality);

            //var importer = new Assimp.AssimpContext();
            //var scene = importer.ImportFile(file, Assimp.PostProcessPreset.TargetRealTimeQuality);


            AssimpForEach(scene->MNumMaterials, scene->MMaterials, (ref Assimp.Material material) =>
            {
                var expectedTextures = new[]
                {
                    Assimp.TextureType.BaseColor,
                    Assimp.TextureType.Normals,
                    Assimp.TextureType.Metalness,
                    Assimp.TextureType.DiffuseRoughness,
                    Assimp.TextureType.Lightmap,
                };

                foreach(var type in expectedTextures)
                {
                    Debug.Assert(assimp.GetMaterialTextureCount(material, type) == 1);
                }

                foreach (var type in expectedTextures)
                {
                    string texturePath;

                    {
                        AssimpString aiTexturePath;
                        assimp.GetMaterialTexture(material, type, 0, &aiTexturePath, null, null, null, null, null, null);
                        texturePath = aiTexturePath.AsString;
                    }

                    if (!catalogue.HasTexture(texturePath))
                    {
                        Result<TextureData> texture = CreateTexture(Path.GetDirectoryName(file) ?? string.Empty, texturePath);

                        if (texture.IsSuccess)
                            catalogue.AddTexture(texture.Value);
                        else
                        {
                            Console.Error.Write("Couldn't load file at ");
                            Console.Error.WriteLine(file);
                        }
                    }
                }
            });

            AssimpForEach(scene->MNumMaterials, scene->MMaterials, (ref Assimp.Material material) => catalogue.AddMaterial(CreateMaterial(assimp, catalogue, ref material)));


            // TODO: Submeshes
            AssimpForEach(scene->MNumMeshes, scene->MMeshes, (ref Assimp.Mesh mesh) =>
            {
                var meshes = new List<Assimp.Mesh>();
                meshes.Add(mesh);

                MeshWithMaterial meshWithMaterial = CreateMesh(assimp, meshes, scene->MMaterials);
                catalogue.AddVertexData(meshWithMaterial.Mesh);
                catalogue.AddDefaultMaterial(meshWithMaterial.Mesh.Name, meshWithMaterial.SubmeshMaterials);
            });

            // Meshes with submeshes are named Mesh-0/Mesh-1/Mesh-2. Group them
            // together before calling `Addmesh`.
            /*scene.Meshes.Sort((lhs, rhs) => string.Compare(lhs.Name, rhs.Name, StringComparison.Ordinal));

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
            }*/
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

                vertices[y * sideLength + x] = new Vertex
                {
                    Position = vertexPosition,
                    Normal = normal,
                    Tangent = normal, // TODO
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
