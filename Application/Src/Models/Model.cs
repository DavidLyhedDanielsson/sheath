using Application.Graphics;

namespace Application.Models;

public class Model
{
    // TODO: Place outside of Model?
    public class Submesh
    {
        public required VIBufferView VIBufferView { get; init; }
        public required Surface Surface { get; init; }
    };

    public required Submesh[] Submeshes { get; init; }
}