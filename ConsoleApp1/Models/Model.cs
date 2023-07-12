using ConsoleApp1.Graphics;

namespace ConsoleApp1.Models;

public class Model
{
    public class Submesh
    {
        public required VIBufferView VIBufferView { get; init; }
        public required Surface Surface { get; init; }
    };

    public required Submesh[] Submeshes { get; init; }
}