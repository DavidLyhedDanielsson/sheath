#include "Shared/FrameData.cs"
#include "Shared/ModelData.cs"

struct VertexIn {
  float3 position;
  float3 normal;
  float3 tangent;
  float2 texCoords;
};

struct VertexOut {
  float4 svPosition : SV_POSITION;
  float3 lPosition : LOCAL_POSITION;
};

cbuffer ModelData : register(b0, space0) { ModelData modelData; }

cbuffer cameraBuffer : register(b1, space0) { float4x4 vpMatrix; }
cbuffer FrameData : register(b2, space0) { FrameData frameData; };

StructuredBuffer<VertexIn> vertices[] : register(t0, space2);

VertexOut main(uint vertexId : SV_VERTEXID, uint instanceId : SV_InstanceID) {
  //// Read
  const VertexIn vertexIn = vertices[modelData.vertexBufferId][vertexId];

  const float4 projectedPos = mul(float4(vertexIn.position, 0.0f), vpMatrix);

  //// Write
  VertexOut vertexOut;
  vertexOut.svPosition = float4(projectedPos.x, projectedPos.y, 0.0f, projectedPos.w);
  vertexOut.lPosition = vertexIn.position;
  return vertexOut;
}
