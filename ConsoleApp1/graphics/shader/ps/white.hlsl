Texture2D textures[]: register(t0, space1);
sampler samp: register(s0, space2);

struct Vertex {
    float4 svPosition: SV_POSITION;
    float2 texCoords: TEX_COORDS;
};

float4 main(Vertex vertex) : SV_TARGET {
    float4 colour = textures[0].Sample(samp, vertex.texCoords);
    return colour;
}