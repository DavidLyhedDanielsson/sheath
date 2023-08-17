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
    float2 texCoords : TEX_COORDS;
    float3 lightDirT : LIGHT_DIR;
    float3 cameraDirT : CAMERA_DIR;
};

float3 HalfwayVector(float3 a, float3 b)
{
    return (a + b) / length(a + b);
}

float3 FresnelSchlick(float3 reflectionCoeff, float angleCos)
{
    return reflectionCoeff + (1.0f - reflectionCoeff) * pow(clamp(1.0f - angleCos, 0.0f, 1.0f), 5.0f);
}

float TrowbridgeReitzGGX(float3 normal, float3 halfwayVector, float roughness)
{
    // Would love to benchmark pow(X, Y) to see if it is "unrolled"
    const float r4 = roughness * roughness * roughness * roughness;
    const float alignment = max(dot(normal, halfwayVector), 0.0f);
    const float r2 = alignment * alignment;
    
    const float denom = (alignment * (r4 - 1.0f) + 1.0f);
    
    return r4 / (3.1415f * denom * denom);
}

float SchlickGGX(float ndotv, float roughness)
{
    const float r = (roughness + 1.0f);
    const float k = (r * r) / 8.0f;

    const float denom = ndotv * (1.0f - k) + k;

    return ndotv / denom;
}

float Smith(float3 normal, float3 cameraDir, float3 lightDir, float roughness)
{
    float ndotv = max(dot(normal, cameraDir), 0.0f);
    float ndotl = max(dot(normal, lightDir), 0.0f);
    
    float s0 = SchlickGGX(ndotv, roughness);
    float s1 = SchlickGGX(ndotl, roughness);
    
    return s0 * s1;
}

float3 CookTorrance(float3 f, float ndf, float g, float3 normal, float3 cameraDir, float3 lightDir)
{
    const float3 num = f * ndf * g;
    const float3 denom = 4.0f * max(dot(normal, cameraDir), 0.0f) * max(dot(normal, lightDir), 0.0f) + 0.00001f;
    
    return num / denom;
}

// TODOS
//const static float albedo = 1.0f;
//const static float metallic = 0.0f;
//const static float roughness = 0.0f;
//const static float ambientOcclusion = 0.0f;

const static float3 LIGHT_COLOUR = float3(23.47f, 21.31f, 20.79f);
const static float3 SURFACE_REFLECTANCE = float3(0.04f, 0.04f, 0.04f);

float4 main(Vertex vertex) : SV_TARGET { 
    const float2 texCoords = float2(vertex.texCoords.x, 1.0f - vertex.texCoords.y);
    const float3 normal = textures[normalTextureId].Sample(samp, texCoords).xyz * 2.0f - 1.0f;
    const float3 albedo = textures[albedoTextureId].Sample(samp, texCoords).xyz;
    const float3 orm = textures[ormTextureId].Sample(samp, texCoords).xyz;
    
    const float metallic = orm.b;
    const float roughness = orm.g;
    const float ambienOcclusion = orm.r;

    const float3 cameraDir = vertex.cameraDirT;
    const float3 lightDir = vertex.lightDirT;
    const float3 halfwayVector = HalfwayVector(lightDir, cameraDir);

    //const float lightFac = max(dot(normal, -vertex.lightDirT), 0.0f);
    const float3 radiance = LIGHT_COLOUR;

    const float3 f = FresnelSchlick(lerp(SURFACE_REFLECTANCE, albedo, metallic), max(dot(halfwayVector, vertex.cameraDirT), 0.0f));
    const float ndf = TrowbridgeReitzGGX(normal, halfwayVector, roughness);
    const float g = Smith(normal, cameraDir, lightDir, roughness);
    const float3 brdf = CookTorrance(f, ndf, g, normal, cameraDir, lightDir);
    
    const float3 specularFac = f;
    const float3 diffuseFac = (float3(1.0f, 1.0f, 1.0f) - specularFac) * (1.0f - metallic);
    
    const float3 outgoingRadiance = (diffuseFac * albedo / 3.1415f + brdf) * radiance * max(dot(normal, lightDir), 0.0f);
    
    const float3 outColour = outgoingRadiance + (0.03f * albedo * ambienOcclusion);
    

    //float lightFac = dot(normal, -vertex.lightDirT);
    //lightFac = max(lightFac, 0.3f);

    float4 colour = float4(outColour, 1.0f);
    //float4 colour = float4(normal, 1.0f);
    
    return colour;
}