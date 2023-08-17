#region Header
namespace Application.Graphics.shader
{
public
#endregion

//// Struct
struct ModelData
{
    int vertexBufferId;
    int albedoTextureId;
    int normalTextureId;
    int ormTextureId;
    int instanceStartOffset;

#if !HLSL
    public int VertexBufferId { get => vertexBufferId; set => vertexBufferId = value; }
    public int AlbedoTextureId { get => albedoTextureId; set => albedoTextureId = value; }
    public int NormalTextureId { get => normalTextureId; set => normalTextureId = value; }
    public int OrmTextureId { get => ormTextureId; set => ormTextureId = value; }
    public int InstanceStartOffset { get => instanceStartOffset; set => instanceStartOffset = value; }
#endif
};
#region Footer
}
#endregion
