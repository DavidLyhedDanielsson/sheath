struct VertexIn {
    float3 position;
    float3 normal;
    float2 texCoords;
};

struct VertexOut {
    float4 svPosition: SV_POSITION;
    float3 normal: NORMAL;
    float2 texCoords: TEX_COORDS;
};

cbuffer instanceData: register(b0, space0) {
    int vertexBufferID;
    int surfaceID;
}
cbuffer cameraBuffer: register(b1, space0) {
    float4x4 projMatrix;
}
StructuredBuffer<VertexIn> vertices[]: register(t0, space2);

VertexOut main(uint vertexId : SV_VERTEXID) {
    VertexIn vertexIn = vertices[vertexBufferID][vertexId];

    VertexOut vertexOut;
    vertexOut.svPosition = mul(float4(vertexIn.position, 1.0f), projMatrix);
    vertexOut.normal = vertexIn.normal;
    vertexOut.texCoords = vertexIn.texCoords;
    return vertexOut;
}