struct Vertex {
    float4 svPosition: SV_POSITION;
    float3 normal: NORMAL;
    float2 texCoords: TEX_COORDS;
};

float4 main(Vertex vertex) : SV_TARGET {
    float colour = dot(float3(0.0f, 1.0f, 0.0f), vertex.normal);
    return float4(colour, colour, colour, 1.0f);
}