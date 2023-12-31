using Vortice.Direct3D12;

namespace Application.Graphics;

public class PSO : IDisposable
{
    public required string VertexShader { get; init; }
    public required string PixelShader { get; init; }
    public required bool BackfaceCulling { get; init; }
    public required int ID { get; init; }
    public required RasterizerDescription RasterizerDescription { get; init; }
    public required ID3D12PipelineState ID3D12PipelineState { get; set; }

    public void Dispose()
    {
        ID3D12PipelineState.Dispose();
    }
}

