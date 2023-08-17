#include "shared/ModelData.cs"

Texture2D textures[]: register(t0, space1);
sampler samp: register(s0, space4);

cbuffer modelData : register(b0, space0)
{
    ModelData modelData;
}

struct Vertex {
    float4 svPosition: SV_POSITION;
    float2 texCoords: TEX_COORDS;
};

float4 main(Vertex vertex) : SV_TARGET {
    const float2 texCoords = float2(vertex.texCoords.x, 1.0f - vertex.texCoords.y);
    const float4 albedo = textures[modelData.albedoTextureId].Sample(samp, texCoords);

    if(albedo.a < 0.5f)
        discard;

    return float4(albedo.xyz, 1.0f);

}
