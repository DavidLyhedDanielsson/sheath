using Vortice.Direct3D12;

namespace ConsoleApp1.Graphics;

public class VIBufferView
{
    public required int VertexBufferId { get; init; }
    public required ID3D12Resource VertexBuffer { get; init; }
    public required ID3D12Resource IndexBuffer { get; init; }
    public required int IndexStart { get; init; }
    public required int IndexCount { get; init; }
    public required int IndexBufferTotalCount { get; init; }
}