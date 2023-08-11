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

VertexOut main(uint vertexId: SV_VERTEXID, uint instanceId: SV_InstanceID) {
    VertexIn vertexIn = vertices[vertexBufferID][vertexId];
    InstanceData instanceData = instanceDatas[instanceStartOffset + instanceId];

    float3x3 transform = (float3x3)instanceData.transform;

    float sx = dot(transform[0], transform[0]);
    float sy = dot(transform[1], transform[1]);
    float sz = dot(transform[2], transform[2]);

    float3x3 normalMatrix = float3x3(1.0f / sx * transform[0], 1.0f / sy * transform[1], 1.0f / sz * transform[2]);

    VertexOut vertexOut;
    vertexOut.svPosition = mul(mul(instanceData.transform, float4(vertexIn.position, 1.0f)), projMatrix);
    vertexOut.normal = mul(vertexIn.normal, normalMatrix);
    vertexOut.texCoords = vertexIn.texCoords;
    return vertexOut;
}