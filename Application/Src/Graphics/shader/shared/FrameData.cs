//`#region` is stripped out when included from HLSL
#region Header
using float3 = Silk.NET.Maths.Vector3D<float>;
using float4 = Silk.NET.Maths.Vector4D<float>;

namespace Application.Graphics.shader
{
public
#endregion

//// Struct
struct FrameData
{
    float3 cameraPosition;
    float p0;
    float3 lightPosition;
    float p1;
#if !HLSL
    public float3 CameraPosition { get => cameraPosition; set => cameraPosition = value; }
    public float3 LightPosition { get => lightPosition; set => lightPosition = value; }
#endif
};

#region Footer
}
#endregion
