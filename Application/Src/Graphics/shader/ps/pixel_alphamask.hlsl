Texture2D textures[]: register(t0, space1);
sampler samp: register(s0, space4);

cbuffer instanceData: register(b0, space0) {
    int vertexBufferID;
    int surfaceID;
}

struct Vertex {
    float4 svPosition: SV_POSITION;
    float3 normal: NORMAL;
    float2 texCoords: TEX_COORDS;
};

float4 main(Vertex vertex) : SV_TARGET {
    float2 texCoords = float2(vertex.texCoords.x, 1.0f - vertex.texCoords.y);

    float4 colour = textures[surfaceID].Sample(samp, texCoords);

    if(colour.a < 0.5f)
        discard;

    return colour;
}