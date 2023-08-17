#include "Shared/FrameData.cs"
#include "Shared/ModelData.cs"

struct VertexIn
{
    float3 position;
    float3 normal;
    float3 tangent;
    float2 texCoords;
};

struct VertexOut
{
    float4 svPosition : SV_POSITION;
    float2 texCoords : TEX_COORDS;
    float3 lightDirT : LIGHT_DIR;
    float3 cameraDirT : CAMERA_DIR;
};

struct InstanceData
{
    float4x4 transform;
};

cbuffer ModelData : register(b0, space0)
{
    ModelData modelData;
}

cbuffer cameraBuffer : register(b1, space0)
{
    float4x4 vpMatrix;
}

cbuffer FrameData: register(b2, space0)
{
    FrameData frameData;
};

StructuredBuffer<VertexIn> vertices[]: register(t0, space2);
StructuredBuffer<InstanceData> instanceDatas: register(t0, space5);

// https://lxjk.github.io/2017/10/01/Stop-Using-Normal-Matrix.html
float3x3 CreateNormalMatrix(float3x3 worldTransform)
{
    float sx = dot(worldTransform[0], worldTransform[0]);
    float sy = dot(worldTransform[1], worldTransform[1]);
    float sz = dot(worldTransform[2], worldTransform[2]);

    return float3x3(1.0f / sx * worldTransform[0], 1.0f / sy * worldTransform[1], 1.0f / sz * worldTransform[2]);
}

float3x3 CreateTBNMatrix(float3 normal, float3 tangent)
{
    // Re-orthogonalisefirst
    normal = normalize(normal);
    tangent = normalize(tangent);
    tangent = normalize(tangent - dot(tangent, normal) * normal);
    return float3x3(tangent, cross(normal, tangent), normal);
}

//const static float3 LIGHT_DIR = -normalize(float3(1.0f, 1.0f, 1.0f));

//const static float3 cameraPosition = float3(0.0f, 0.0f, -1.0f);

VertexOut main(uint vertexId: SV_VERTEXID, uint instanceId: SV_InstanceID) {
    //// Read
    const InstanceData instanceData = instanceDatas[modelData.instanceStartOffset + instanceId];
    const VertexIn vertexIn = vertices[modelData.vertexBufferId][vertexId];
    const float3x3 modelMatrix = (float3x3)instanceData.transform;

    
    //// Calculate
    // Normal
    const float3x3 normalMatrix = CreateNormalMatrix(modelMatrix);
    const float3 normalW = mul(vertexIn.normal, normalMatrix);
    const float3 tangentW = mul(vertexIn.tangent, normalMatrix);

    const float3x3 tbnTangent = transpose(CreateTBNMatrix(normalW, tangentW));

    // Position
    float4 worldPosition = mul(instanceData.transform, float4(vertexIn.position * 2.0f, 1.0f)); // TODO: Should be vec * mat


    //// Write
    VertexOut vertexOut;
    vertexOut.svPosition = mul(worldPosition, vpMatrix);
    //vertexOut.normal = mul(vertexIn.normal, normalMatrix);
    //vertexOut.tangent = mul(vertexIn.tangent, normalMatrix);
    vertexOut.texCoords = vertexIn.texCoords;
    vertexOut.lightDirT = mul(normalize(frameData.lightPosition.xyz - worldPosition.xyz), tbnTangent);
    vertexOut.cameraDirT = mul(normalize(frameData.cameraPosition.xyz - worldPosition.xyz), tbnTangent);
    //vertexOut.pixelPos = mul(worldPosition.xyz, tbnTangent);
    return vertexOut;
}