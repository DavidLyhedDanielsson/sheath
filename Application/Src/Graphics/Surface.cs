using Application.Graphics;
using Vortice.Direct3D12;

namespace Application.Models;

public class Surface
{
    public required int ID { get; init; }
    public required PSO PSO { get; init; }
    public required Texture? AlbedoTexture { get; init; }
    public required Texture? NormalTexture { get; init; }
    public required Texture? ORMTexture { get; init; }
}