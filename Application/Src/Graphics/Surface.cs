using Application.Graphics;

namespace Application.Models;

public class Surface : IDisposable
{
    public required int ID { get; init; }
    public required PSO PSO { get; init; }
    public required Texture? AlbedoTexture { get; init; }
    public required Texture? NormalTexture { get; init; }
    public required Texture? ORMTexture { get; init; }

    public void Dispose()
    {
        PSO.Dispose();
    }
}