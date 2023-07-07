struct Vertex {
  float4 svPosition : SV_POSITION;
};

const static float2 vertices[] = {
    float2(0.0f, 0.5f),
    float2(-0.5f, -0.5f),
    float2(0.5f, -0.5f),
};

Vertex main(uint vertexId : SV_VERTEXID) {
  Vertex vertex;
  vertex.svPosition = float4(vertices[vertexId], 0.5f, 1.0f);
  return vertex;
}