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
    #ifdef RENDER_WHITE
    float4 colour = float4(1.0f, 1.0f, 1.0f, 1.0f);
    #else
    //float4 colour = float4(vertex.normal * 0.5f + 0.5f, 1.0f);
    #endif

    float4 colour = float4(textures[1].Sample(samp, vertex.texCoords).xyz, 1.0f);

    return colour;
}