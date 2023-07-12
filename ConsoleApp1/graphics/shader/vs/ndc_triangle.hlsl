cbuffer cameraBuffer: register(b0, space0) {
    float4x4 projMatrix;
}

struct VertexIn {
    float3 position;
    float3 normal;
    float2 texCoords;
};

StructuredBuffer<VertexIn> vertices: register(t0, space2);

struct Vertex {
    float4 svPosition: SV_POSITION;
    float3 normal: NORMAL;
    float2 texCoords: TEX_COORDS;
};

Vertex main(uint vertexId : SV_VERTEXID) {
    Vertex vertex;
    vertex.svPosition = mul(float4(vertices[vertexId].position, 1.0f), projMatrix);
    vertex.normal = vertices[vertexId].normal;
    return vertex;
}