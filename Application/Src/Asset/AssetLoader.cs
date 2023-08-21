using FluentResults;
using StbiSharp;
using Assimp = Silk.NET.Assimp;
using System.Diagnostics;
using Silk.NET.Maths;
using Silk.NET.Assimp;
using File = System.IO.File;
using System.Numerics;
using System.Threading.Channels;

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
        string materialName;
        {
            AssimpString aiMaterialName;
            assimp.GetMaterialString(material, Assimp.Assimp.MaterialNameBase, 0, 0, &aiMaterialName);
            materialName = aiMaterialName;
        }

        TextureData? albedoTexture = assetCatalogue.GetTextureData(materialName + "_Albedo");
        if (albedoTexture == null)
            throw new NotImplementedException("Nooooooo not yet :(");

        TextureData? normalTexture = assetCatalogue.GetTextureData(materialName + "_Normal");
        if (normalTexture == null)
            throw new NotImplementedException("Nooooooo not yet :(");

        TextureData? ormTexture = assetCatalogue.GetTextureData(materialName + "_ORM");
        if (ormTexture == null)
            throw new NotImplementedException("Nooooooo not yet :(");

        bool hasAlpha = false;

        Assimp.AssimpString gltfAlphaMode;
        assimp.GetMaterialString(material, "$mat.gltf.alphaMode", 0, 0, &gltfAlphaMode);
        if (gltfAlphaMode == "MASK")
        {
            hasAlpha = true;
        }

        return new Material()
        {
            Name = materialName,
            AlbedoTexture = albedoTexture.Name,
            AlbedoTextureHasAlpha = hasAlpha,
            NormalTexture = normalTexture.Name,
            ORMTexture = ormTexture.Name,
        };
    }

    public static Result<TextureData> CreateTexture(string name, string rootPath, string texturePath)
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
                    Name = name,
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
    public enum Channel
    {
        R = 1 << 0,
        G = 1 << 1,
        B = 1 << 2,
        A = 1 << 3,
    };

    public struct ChannelSwizzle
    {
        public readonly Channel R { get; init; }
        public readonly Channel G { get; init; }
        public readonly Channel B { get; init; }
        public readonly Channel A { get; init; }

        public Channel this[Channel i] {
            get
            {
                return i switch {
                    Channel.R => R,
                    Channel.G => G,
                    Channel.B => B,
                    Channel.A => R,
                    _ => throw new Exception("Stop it")
                };
            }
        }

        public Channel this[int i] {
            get
            {
                return i switch {
                    0 => R,
                    1 => G,
                    2 => B,
                    3 => R,
                    _ => throw new Exception("Stop it")
                };
            }
        }


        public static ChannelSwizzle Identity = new()
        {
            R = Channel.R,
            G = Channel.G,
            B = Channel.B,
            A = Channel.A,
        };

        public ChannelSwizzle(
            Channel R = Channel.R,
            Channel G = Channel.G,
            Channel B = Channel.B,
            Channel A = Channel.A)
        {
            this.R = R;
            this.G = G;
            this.B = B;
            this.A = A;
        } 
    }

    public static Result<TextureData> CreateTexture(string name, string rootPath, (string, Channel)[] textures)
    {
        return CreateTexture(name, rootPath, textures.Select(pair => (pair.Item1, pair.Item2, ChannelSwizzle.Identity)).ToArray());
    }

    public static Result<TextureData> CreateTexture(string name, string rootPath, (string, Channel, ChannelSwizzle)[] textures, int channelCount = -1)
    {
        Debug.Assert(textures.Length > 0 && textures.Length <= 4);

        int width = -1;
        int height = -1;
        byte[] texels = Array.Empty<byte>();

        foreach ((string path, Channel readChannels, ChannelSwizzle channelSwizzle) in textures)
        {
            using (var file = File.OpenRead(Path.Combine(rootPath, path)))
            using (var stream = new MemoryStream())
            {
                try
                {
                    file.CopyTo(stream);
                    Stbi.InfoFromMemory(stream, out int w, out int h, out int cc);
                    if (width == -1)
                    {
                        width = w;
                        height = h;
                        texels = new byte[width * height * channelCount];
                    }
                    else
                    {
                        Debug.Assert(width == w);
                        Debug.Assert(height == h);
                    }
                    StbiImage image = Stbi.LoadFromMemory(stream, channelCount);

                    LoadTexels(image, texels, channelCount, readChannels, channelSwizzle);

                }
                catch (ArgumentException ex)
                {
                    return Result.Fail(ex.Message);
                }
            }
        }

        return Result.Ok(new TextureData()
        {
            Name = name,
            Texels = texels,
            Width = width,
            Height = height,
            Channels = channelCount == -1 ? textures.Select(tup => BitOperations.PopCount((uint)tup.Item2)).Sum() : channelCount,
        });
    }

    private static Result<TextureData> CreateTexture(string name, string rootPath, string texturePath, Channel channelsToExtract)
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

                var texels = new byte[width * height * numberOfChannelsToExtract];
                LoadTexels(image, texels, numberOfChannelsToExtract, channelsToExtract, ChannelSwizzle.Identity);

                return Result.Ok(new TextureData()
                {
                    Name = name,
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

    private static void LoadTexels(StbiImage image, byte[] texels, int texelChannelCount, Channel channelsToExtract, ChannelSwizzle channelSwizzle)
    {
        int numberOfChannelsToExtract = BitOperations.PopCount((uint)channelsToExtract);

        Debug.Assert(image.NumChannels >= numberOfChannelsToExtract);
        Debug.Assert(image.Width * image.Height * texelChannelCount == texels.Length);

        var extractMask = new bool[]
        {
            (channelsToExtract & Channel.R) == Channel.R,
            (channelsToExtract & Channel.G) == Channel.G,
            (channelsToExtract & Channel.B) == Channel.B,
            (channelsToExtract & Channel.A) == Channel.A,
        };

        //var texels = new byte[width * height * numberOfChannelsToExtract];
        for (int texelI = 0; texelI < image.Width * image.Height; ++texelI)
        {
            for (int readChannel = 0; readChannel < 4; ++readChannel)
            {
                if (extractMask[readChannel])
                {
                    int writeChannel = BitOperations.TrailingZeroCount((int)channelSwizzle[readChannel]);
                    texels[texelI * texelChannelCount + writeChannel] = image.Data[texelI * image.NumChannels + readChannel];
                }
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

            var expectedTextures = new[]
            {
                TextureType.BaseColor,
                TextureType.Normals,
                TextureType.Metalness,
                TextureType.DiffuseRoughness,
                TextureType.Lightmap,
            };

            string rootDir = Path.GetDirectoryName(file) ?? string.Empty;

            AssimpForEach(scene->MNumMaterials, scene->MMaterials, (ref Assimp.Material material) =>
            {
                foreach (var type in expectedTextures)
                    Debug.Assert(assimp.GetMaterialTextureCount(material, type) == 1);

                Dictionary<TextureType, string> texturePaths = new();

                foreach (var type in expectedTextures)
                {
                    AssimpString aiTexturePath;
                    assimp.GetMaterialTexture(material, type, 0, &aiTexturePath, null, null, null, null, null, null);
                    texturePaths.Add(type, aiTexturePath.AsString);
                }

                string metalnessPath = texturePaths[TextureType.Metalness];
                string roughnessPath = texturePaths[TextureType.DiffuseRoughness];
                string occlusionPath = texturePaths[TextureType.Lightmap];

                string materialName;
                {
                    AssimpString aiMaterialName;
                    assimp.GetMaterialString(material, Assimp.Assimp.MaterialNameBase, 0, 0, &aiMaterialName);
                    materialName = aiMaterialName.AsString;
                }
                string ormTextureName = materialName + "_ORM";

                TextureData ormTexture;
                if (metalnessPath == roughnessPath && metalnessPath == occlusionPath)
                {
                    Debugger.Break(); // TODO: Reminder to test :)
                    ormTexture = CreateTexture(ormTextureName, rootDir, new[]
                    {
                        (metalnessPath, Channel.R | Channel.G | Channel.B)
                    }).Value;

                }
                else if (metalnessPath == roughnessPath)
                {
                    ormTexture = CreateTexture(ormTextureName, rootDir, new[]
                    {
                        (occlusionPath, Channel.R, ChannelSwizzle.Identity),
                        (metalnessPath, Channel.G | Channel.B, new ChannelSwizzle(G: Channel.G, B: Channel.B)),
                    }, 4).Value;
                }
                else
                {
                    Debugger.Break(); // TODO: Reminder to test :)
                    ormTexture = CreateTexture(ormTextureName, rootDir, new[]
                    {
                        (occlusionPath, Channel.R, ChannelSwizzle.Identity),
                        (roughnessPath, Channel.G, new ChannelSwizzle(R: Channel.G)),
                        (metalnessPath, Channel.B, new ChannelSwizzle(R: Channel.B)),
                    }, 4).Value;
                }

                catalogue.AddTexture(ormTexture);
                catalogue.AddTexture(CreateTexture(materialName + "_Albedo", rootDir, texturePaths[TextureType.BaseColor]).Value);
                catalogue.AddTexture(CreateTexture(materialName + "_Normal", rootDir, texturePaths[TextureType.Normals]).Value);
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
