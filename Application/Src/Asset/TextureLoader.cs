using StbiSharp;
using FluentResults;
using System.Diagnostics;
using System.Numerics;

namespace Application.Asset
{
    public static class TextureLoader
    {
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

            public static ChannelSwizzle Identity = new()
            {
                R = Channel.R,
                G = Channel.G,
                B = Channel.B,
                A = Channel.A,
            };

            public Channel this[Channel i]
            {
                get
                {
                    return i switch
                    {
                        Channel.R => R,
                        Channel.G => G,
                        Channel.B => B,
                        Channel.A => R,
                        _ => throw new Exception("Stop it")
                    };
                }
            }

            public Channel this[int i]
            {
                get
                {
                    return i switch
                    {
                        0 => R,
                        1 => G,
                        2 => B,
                        3 => R,
                        _ => throw new Exception("Stop it")
                    };
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

        public static Result<TextureData> CreateTexture(string name, string texturePath)
        {
            using (var file = File.OpenRead(texturePath))
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

        public static Result<TextureData> CreateTexture(string name, (string, Channel)[] textures)
        {
            return CreateTexture(name, textures.Select(pair => (pair.Item1, pair.Item2, ChannelSwizzle.Identity)).ToArray());
        }

        public static Result<TextureData> CreateTexture(string name, (string, Channel, ChannelSwizzle)[] textures, int channelCount = -1)
        {
            Debug.Assert(textures.Length > 0 && textures.Length <= 4);

            int width = -1;
            int height = -1;
            byte[] texels = Array.Empty<byte>();

            foreach ((string path, Channel readChannels, ChannelSwizzle channelSwizzle) in textures)
            {
                using (var file = File.OpenRead(path))
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

        private static Result<TextureData> CreateTexture(string name, string texturePath, Channel channelsToExtract)
        {
            using (var file = File.OpenRead(texturePath))
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
}
