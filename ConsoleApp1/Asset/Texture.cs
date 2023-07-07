namespace ConsoleApp1.Asset;

public class Texture
{
    public required string FilePath { get; init; }
    public required byte[] Texels { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
}