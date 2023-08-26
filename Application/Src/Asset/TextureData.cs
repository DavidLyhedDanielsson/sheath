namespace Application.Asset;

public class TextureData
{
    public required string Name { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int Channels { get; init; }
    // This data here may not be bytes, check ChannelByteSize before assuming.
    // It's currently either bytes or floats.
    public required byte[] Data { get; init; }
    public required int ChannelByteSize { get; init; }
}
