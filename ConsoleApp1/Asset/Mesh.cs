using System.Numerics;

namespace ConsoleApp1.Asset;

public struct Vertex
{
    public required Vector3 Position { get; init; }
    public required Vector3 Normal { get; init; }
    public required Vector2 TextureCoordinates { get; init; }
}

public class Mesh
{
    public required string Name { get; init; }
    public required Submesh[] Submeshes { get; init; }
    public required Vertex[] Vertices { get; init; }
}

public class Submesh
{
    //public required string MaterialName { get; init; }
    public required int[] Indices { get; init; }
}