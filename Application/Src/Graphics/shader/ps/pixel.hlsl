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

float SchlickGGX(float dotVal, float roughness)
{
    const float k = (roughness + 1.0f) * (roughness + 1.0f) / 8.0f;
    const float val = dotVal / (dotVal * (1 - k) + k);

    return val;
}

float SmithGGX(float3 normal, float3 lightDir, float3 cameraDir, float roughness)
{
    const float s0 = SchlickGGX(dotP(normal, cameraDir), roughness);
    const float s1 = SchlickGGX(dotP(normal, lightDir), roughness);
    return s0 * s1;
}

float3 Fresnel(float3 baseReflectivity, float3 surfaceColour, float metalness, float3 halfwayDir, float3 cameraDir)
{
    const float3 reflectivity = lerp(baseReflectivity, surfaceColour, metalness);
    const float sq = 1.0f - dotP(halfwayDir, cameraDir);
    const float3 val = reflectivity + (1.0f - reflectivity) * pow(sq, 5);
    
    return val;
}

float TrowbridgeReitz(float roughness, float3 normal, float3 halfwayDir)
{
    const float r2 = pow2(roughness);
    
    const float a = pow2p(dot(normal, halfwayDir));
    const float b = r2 - 1.0f;
    
    const float num = r2;
    const float denom = PI * pow2(a * b + 1);
    
    return num / denom;
}

float4 main(Vertex vertex) : SV_TARGET { 
    // Render eq:
    // BRDF * Incoming radiance * Lambert's law
    
    // Cook-Torrance BRDF:
    // Diffuse ratio * Lambert + Specular ratio * Cook-Torrance
    // Cook-Torrance:
    // (Normal distribution * Fresnel * Geometry)/(4(ViewDir * normal)(LightDir * normal))
    
    // Normal distribution (Trowbridge-Reitz):
    // r² / pi((n * h)²(r² - 1) + 1)²
    
    // Fresnel (Fresnel-Schlick)
    // Reflectivity = lerp(float3(0.04), surfaceColor, metalness)
    // Reflectivity + (1 - Reflectivity)(1 - h * v)^5
    
    // Geometry (Smith + Schlick-GGX)
    // Schlick-GGX:
    // k = (r + 1)² / 8
    // (n * v) / ((n * v)(1 - k) + k)
    // Smith:
    // Shlick-GGX(n,v,k)Schlick-GGx(n,l,k)

    // Read data
    const float2 texCoords = float2(vertex.texCoords.x, 1.0f - vertex.texCoords.y);
    const float3 normal = textures[normalTextureId].Sample(samp, texCoords).xyz * 2.0f - 1.0f;
    const float3 albedo = pow(textures[albedoTextureId].Sample(samp, texCoords).xyz, 2.2f);
    const float3 orm = textures[ormTextureId].Sample(samp, texCoords).xyz;
    
    const float metalness = orm.b;
    const float roughness = orm.g;
    const float ambientOcclusion = orm.r;

    const float3 cameraDir = normalize(vertex.cameraDirT);
    const float3 lightDir = normalize(vertex.lightDirT.xyz);
    const float lightDistance = vertex.lightDirT.w;
    const float3 halfwayVector = normalize(HalfwayVector(lightDir, cameraDir));

    float attenuation = 1.0f / lightDistance;
    const float3 radiance = LIGHT_COLOUR * attenuation;
    
    // TODO: reorder parameterss
    const float normalDistribution = TrowbridgeReitz(roughness, normal, halfwayVector);
    const float3 fresnel = Fresnel(float3Rep(0.04f), albedo, metalness, halfwayVector, cameraDir);
    const float geometry = SmithGGX(normal, lightDir, cameraDir, roughness);
    
    const float3 specular = (normalDistribution * fresnel * geometry) / (4.0 * dotP(normal, cameraDir) * dotP(normal, lightDir) + epsilon);
    
    const float specularTerm = fresnel;
    const float diffuseTerm = (float3Rep(1.0f) - specularTerm) * metalness;
    
    const float3 outgoingRadiance = (diffuseTerm * albedo / PI + specular) * radiance * dotP(normal, lightDir);
    const float3 ambient = float3Rep(0.03f) * albedo * ambientOcclusion;
    
    float3 colour = ambient + outgoingRadiance;
    colour /= (colour + float3Rep(1.0f));
    colour = pow(colour, float3Rep(1.0f / 2.2f));

    //float4 colour = float4(ambient + outgoingRadiance, 1.0f);
    //float4 colour = float4(radiance, 1.0f);
    
    return float4(colour, 1.0f);
}