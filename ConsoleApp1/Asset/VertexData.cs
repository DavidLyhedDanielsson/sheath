using System.Numerics;

namespace ConsoleApp1.Asset;

public readonly struct Vertex
{
    public required Vector3 Position { get; init; }
    public required Vector3 Normal { get; init; }
    public required Vector2 TextureCoordinates { get; init; }
}

public class VertexData
{
    public required string Name { get; init; }
    public required Submesh[] Submeshes { get; init; }
    public required Vertex[] Vertices { get; init; }
}

public class Submesh
{
    public required uint[] Indices { get; init; }
}