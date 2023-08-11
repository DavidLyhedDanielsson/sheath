namespace Application.Asset;

public class Material
{
   public required string Name { get; init; }

   public required string AlbedoTexture { get; init; }
   public required bool AlbedoTextureHasAlpha { get; init; }
}