using ConsoleApp1.Graphics;
using Vortice.Direct3D12;

namespace ConsoleApp1.Models;

public class Surface
{
    public required int ID { get; init; }
    public required ID3D12PipelineState PSO { get; init; }
    public required Texture AlbedoTexture { get; init; }
}