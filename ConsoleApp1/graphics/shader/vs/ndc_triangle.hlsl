struct Vertex {
    float4 svPosition: SV_POSITION;
    float2 texCoords: TEX_COORDS;
};

const static float2 vertices[] = {
    float2(-.5f, -.5f),
    float2(.5f, -.5f),
    float2(.5f, .5f),

    float2(-.5f, -.5f),
    float2(.5f, .5f),
    float2(-.5f, .5f),
};

const static float2 texCoords[] = {
    float2(0.0f, 0.0f),
    float2(1.0f, 0.0f),
    float2(1.0f, 1.0f),

    float2(0.0f, 0.0f),
    float2(1.0f, 1.0f),
    float2(0.0f, 1.0f),
};

Vertex main(uint vertexId : SV_VERTEXID) {
    Vertex vertex;
    vertex.svPosition = float4(vertices[vertexId], 0.5f, 1.0f);
    vertex.texCoords = texCoords[vertexId];
    return vertex;
}