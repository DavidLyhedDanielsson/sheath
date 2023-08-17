#include "Shared/FrameData.cs"

struct VertexOut
{
    float4 svPosition : SV_POSITION;
    float2 texCoords : TEX_COORDS;
};

cbuffer cameraBuffer : register(b1, space0)
{
    float4x4 vpMatrix;
}

cbuffer frameData: register(b2, space0)
{
    FrameData frameData;
};

const static float2 vertexPositions[4] = {
    float2(-1.0f, 1.0f),
    float2(-1.0f, -1.0f),
    float2(1.0f, -1.0f),
    float2(1.0f, 1.0f)
};

const static float2 texCoords[4] = {
    float2(0.0f, 1.0f),
    float2(0.0f, 0.0f),
    float2(1.0f, 0.0f),
    float2(1.0f, 1.0f),
};

const static float3 up = float3(0.0f, 1.0f, 0.0f);
const static float2 scale = float2(0.25f, 0.25f);

VertexOut main(uint vertexId: SV_VERTEXID, uint instanceId: SV_InstanceID) {
    const uint vertexIndex = (vertexId - (vertexId >= 3)) % 4;
    
    float2 positionOffset = vertexPositions[vertexIndex];
    float2 texCoord = texCoords[vertexIndex];
    
    float3 forward = normalize(frameData.cameraPosition.xyz - frameData.lightPosition.xyz);
    float3 right;
    if (dot(forward, up) < 0.99f)
        right = cross(forward, up);
    else
        right = (0.0f, 0.0f, 1.0f);
    right = normalize(right);
    
    float3 worldPosition = frameData.lightPosition.xyz + right * scale.x * positionOffset.x + up * scale.y * positionOffset.y;

    //// Write
    VertexOut vertexOut;
    vertexOut.svPosition = mul(float4(worldPosition, 1.0f), vpMatrix);
    vertexOut.texCoords = texCoord;

    return vertexOut;
}
