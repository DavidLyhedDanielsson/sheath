using Vortice.Direct3D12;

namespace Application.Graphics;

public class PSO
{
    public required int ID { get; init; }
    public required ID3D12PipelineState ID3D12PipelineState { get; init; }
}

