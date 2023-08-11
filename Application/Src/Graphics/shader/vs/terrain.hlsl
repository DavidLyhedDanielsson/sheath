Texture2D textures[]: register(t0, space1);

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
struct InstanceData {
    float4x4 transform;
};

cbuffer modelData: register(b0, space0) {
    int vertexBufferID;
    int surfaceID;
    int instanceStartOffset;
}
cbuffer cameraBuffer: register(b1, space0) {
    float4x4 projMatrix;
}
StructuredBuffer<VertexIn> vertices[]: register(t0, space2);
StructuredBuffer<InstanceData> instanceDatas: register(t0, space5);

VertexOut main(uint vertexId: SV_VERTEXID) {
    VertexIn vertexIn = vertices[vertexBufferID][vertexId];
    InstanceData instanceData = instanceDatas[instanceStartOffset];

    float3 scale0 = (float3)instanceData.transform[0];
    float3 scale1 = (float3)instanceData.transform[1];
    float3 scale2 = (float3)instanceData.transform[2];

    float3 a = vertexIn.normal.x / dot(scale0, scale0);
    float3 b = vertexIn.normal.y / dot(scale1, scale1);
    float3 c = vertexIn.normal.z / dot(scale2, scale2);

    float3x3 normalMatrix = mul(float3x3(a, b, c), (float3x3)instanceData.transform);

    VertexOut vertexOut;
    vertexOut.svPosition = mul(mul(float4(vertexIn.position, 1.0f), instanceData.transform), projMatrix);
    vertexOut.normal = mul(float3(vertexIn.normal), normalMatrix);
    vertexOut.texCoords = vertexIn.texCoords;
    return vertexOut;
}