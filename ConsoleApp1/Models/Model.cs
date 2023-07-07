using Vortice.Direct3D12;

namespace ConsoleApp1.Models;

public class Model
{
    public ID3D12PipelineState pipelineStateObject;
    public ID3D12Resource vertexBuffer;
    public ID3D12Resource indexBuffer;
    public int indexOffset;
}