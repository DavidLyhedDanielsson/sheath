namespace Application.Asset;

public class TextureData
{
    public required string Name { get; init; }
    public required byte[] Texels { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int Channels { get; init; }
}