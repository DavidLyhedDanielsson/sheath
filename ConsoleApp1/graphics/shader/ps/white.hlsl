Texture2D textures[]: register(t0, space1);
sampler samp: register(s0, space4);

// cbuffer surfaceData: register(b0, space3) {
//     int surfaceID;
// }

struct Vertex {
    float4 svPosition: SV_POSITION;
    float3 normal: NORMAL;
    float2 texCoords: TEX_COORDS;
};

float4 main(Vertex vertex) : SV_TARGET {
    //float4 colour = float4(textures[0].Sample(samp, vertex.texCoords).xyz, 1.0f);
    float4 colour = float4(vertex.normal * 0.5f + 0.5f, 1.0f);
    return colour;
}