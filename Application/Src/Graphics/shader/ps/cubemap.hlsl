#include "shared/ModelData.cs"

Texture2D textures[] : register(t0, space1);
sampler samp : register(s0, space4); 

cbuffer modelData : register(b0, space0) { ModelData modelData; }

struct Vertex {
  float4 svPosition : SV_POSITION;
  float3 lPosition : LOCAL_POSITION;
};

float2 SphericalToRectangular(float3 dir) {
  const static float2 invAtan = float2(0.1591f, -0.3183f);
  const float2 uv = float2(atan2(dir.z, dir.x), asin(dir.y)) * invAtan + 0.5f;
  return uv;
}

float4 main(Vertex vertex) : SV_TARGET {
  const float2 uv = SphericalToRectangular(normalize(vertex.lPosition));
  const float3 color = textures[modelData.albedoTextureId].Sample(samp, uv).xyz;
  return float4(color, 1.0f);
}
