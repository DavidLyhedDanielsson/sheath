//#include "shared/ModelData.cs"

Texture2D textures[]: register(t0, space1);
sampler samp: register(s0, space4);

cbuffer modelData : register(b0, space0)
{
    //ModelData modelData;
    int vertexBufferId;
    int albedoTextureId;
    int normalTextureId;
    int ormTextureId;
    int instanceStartOffset;
}

struct Vertex
{
    float4 svPosition : SV_POSITION;
    float3 normal : NORMAL;
    float2 texCoords : TEX_COORDS;
    float4 lightDirT : LIGHT_DIR;
    float3 cameraDirT : CAMERA_DIR;
};

const static float3 LIGHT_COLOUR = float3(23.47f, 21.31f, 20.79f);
const static float3 SURFACE_REFLECTANCE = float3(0.04f, 0.04f, 0.04f);

const static float PI = 3.141592653589793f;
const static float epsilon = 0.000000001f;

float dotP(float3 a, float3 b)
{
    return max(dot(a, b), 0.0f);
}

float pow2p(float value)
{
    value = max(value, 0.0f);
    return value * value;
}

float pow2(float value)
{
    return value * value;
}

float3 float3Rep(float value)
{
    return float3(value, value, value);
}

float3 HalfwayVector(float3 a, float3 b)
{
    return (a + b) / (length(a + b) + epsilon);
}

float4 main(Vertex vertex) : SV_TARGET
{
    const float2 texCoords = float2(vertex.texCoords.x, 1.0f - vertex.texCoords.y);
    const float3 normal = textures[normalTextureId].Sample(samp, texCoords).xyz * 2.0f - 1.0f;
    const float3 albedo = pow(textures[albedoTextureId].Sample(samp, texCoords).xyz, 2.2f);
    const float3 orm = textures[ormTextureId].Sample(samp, texCoords).xyz;
    const float ambientOcclusion = orm.r;
    
    const float3 cameraDir = normalize(vertex.cameraDirT);
    const float3 lightDir = normalize(vertex.lightDirT.xyz);
    const float lightDistance = vertex.lightDirT.w;
    const float specularStrength = 0.1f;

    const float attenuation = 1.0f / pow2(lightDistance);

    const float ambient = 0.005f;
    const float diffuse = dotP(normal, lightDir);
    const float specular = pow(dotP(vertex.cameraDirT, reflect(-lightDir, normal)), 32.0f) * specularStrength;
    
    // Cheat a diffuse colour using albedo + AO, not correct but more correct than just albedo
    float3 colour = LIGHT_COLOUR * albedo * ambientOcclusion * (ambient + diffuse) + specular;
    colour /= (colour + float3Rep(1.0f));
    colour = pow(colour, float3Rep(1.0f / 2.2f));
    
    return float4(colour, 1.0f);
}
