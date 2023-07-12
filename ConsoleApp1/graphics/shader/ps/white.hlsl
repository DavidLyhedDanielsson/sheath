Texture2D textures[]: register(t0, space1);
sampler samp: register(s0, space2);

struct Vertex {
    float4 svPosition: SV_POSITION;
    float3 normal: NORMAL;
    float2 texCoords: TEX_COORDS;
};

float4 main(Vertex vertex) : SV_TARGET {
    float4 colour = float4(vertex.normal * 0.5f + 0.5f, 1.0f);
    return colour;
}