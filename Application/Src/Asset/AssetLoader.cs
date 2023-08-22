using Assimp = Silk.NET.Assimp;
using AssimpString = Silk.NET.Assimp.AssimpString;
using System.Diagnostics;
using static Application.Asset.TextureLoader;
using Channel = Application.Asset.TextureLoader.Channel;
using static Application.Asset.MeshLoader;

namespace Application.Asset;

public class AssetLoader
{
    private static unsafe Material CreateMaterial(Assimp.Assimp assimp, AssetCatalogue assetCatalogue, ref Assimp.Material material)
    {
        string materialName;
        {
            AssimpString aiMaterialName;
            assimp.GetMaterialString(material, Assimp.Assimp.MaterialNameBase, 0, 0, &aiMaterialName);
            materialName = aiMaterialName;
        }

        TextureData? albedoTexture = assetCatalogue.GetTextureData(materialName + "_Albedo");
        if (albedoTexture == null)
            throw new NotImplementedException("Nooooooo not yet :(");

        TextureData? normalTexture = assetCatalogue.GetTextureData(materialName + "_Normal");
        if (normalTexture == null)
            throw new NotImplementedException("Nooooooo not yet :(");

        TextureData? ormTexture = assetCatalogue.GetTextureData(materialName + "_ORM");
        if (ormTexture == null)
            throw new NotImplementedException("Nooooooo not yet :(");

        bool hasAlpha = false;

        AssimpString gltfAlphaMode;
        assimp.GetMaterialString(material, "$mat.gltf.alphaMode", 0, 0, &gltfAlphaMode);
        if (gltfAlphaMode == "MASK")
        {
            hasAlpha = true;
        }

        return new Material()
        {
            Name = materialName,
            AlbedoTexture = albedoTexture.Name,
            AlbedoTextureHasAlpha = hasAlpha,
            NormalTexture = normalTexture.Name,
            ORMTexture = ormTexture.Name,
        };
    }


    private unsafe delegate void AssimpEach<T>(ref T arg) where T : unmanaged;
    private static unsafe void AssimpForEach<T>(uint count, T** type, AssimpEach<T> action) where T: unmanaged
    {
        for (int i = 0; i < count; ++i)
            action(ref *type[i]);
    }

    public static void Import(AssetCatalogue catalogue, string file)
    {
        unsafe
        {
            Assimp.Assimp assimp = Assimp.Assimp.GetApi();
            Assimp.Scene* scene;

            scene = assimp.ImportFile(file, (uint)Assimp.PostProcessPreset.TargetRealTimeQuality);

            var expectedTextures = new[]
            {
                Assimp.TextureType.BaseColor,
                Assimp.TextureType.Normals,
                Assimp.TextureType.Metalness,
                Assimp.TextureType.DiffuseRoughness,
                Assimp.TextureType.Lightmap,
            };

            string rootDir = Path.GetDirectoryName(file) ?? string.Empty;

            AssimpForEach(scene->MNumMaterials, scene->MMaterials, (ref Assimp.Material material) =>
            {
                foreach (var type in expectedTextures)
                    Debug.Assert(assimp.GetMaterialTextureCount(material, type) == 1);

                Dictionary<Assimp.TextureType, string> texturePaths = new();

                foreach (var type in expectedTextures)
                {
                    AssimpString aiTexturePath;
                    assimp.GetMaterialTexture(material, type, 0, &aiTexturePath, null, null, null, null, null, null);
                    texturePaths.Add(type, aiTexturePath.AsString);
                }

                string metalnessPath = texturePaths[Assimp.TextureType.Metalness];
                string roughnessPath = texturePaths[Assimp.TextureType.DiffuseRoughness];
                string occlusionPath = texturePaths[Assimp.TextureType.Lightmap];

                string materialName;
                {
                    AssimpString aiMaterialName;
                    assimp.GetMaterialString(material, Assimp.Assimp.MaterialNameBase, 0, 0, &aiMaterialName);
                    materialName = aiMaterialName.AsString;
                }
                string ormTextureName = materialName + "_ORM";

                TextureData ormTexture;
                if (metalnessPath == roughnessPath && metalnessPath == occlusionPath)
                {
                    Debugger.Break(); // TODO: Reminder to test :)
                    ormTexture = CreateTexture(ormTextureName, new[]
                    {
                        (Path.Combine(rootDir, metalnessPath), Channel.R | Channel.G | Channel.B)
                    }).Value;

                }
                else if (metalnessPath == roughnessPath)
                {
                    ormTexture = CreateTexture(ormTextureName, new[]
                    {
                        (Path.Combine(rootDir, occlusionPath), Channel.R, ChannelSwizzle.Identity),
                        (Path.Combine(rootDir, metalnessPath), Channel.G | Channel.B, new ChannelSwizzle(G: Channel.G, B: Channel.B)),
                    }, 4).Value;
                }
                else
                {
                    Debugger.Break(); // TODO: Reminder to test :)
                    ormTexture = CreateTexture(ormTextureName, new[]
                    {
                        (Path.Combine(rootDir, occlusionPath), Channel.R, ChannelSwizzle.Identity),
                        (Path.Combine(rootDir, roughnessPath), Channel.G, new ChannelSwizzle(R: Channel.G)),
                        (Path.Combine(rootDir, metalnessPath), Channel.B, new ChannelSwizzle(R: Channel.B)),
                    }, 4).Value;
                }

                catalogue.AddTexture(ormTexture);
                catalogue.AddTexture(CreateTexture(materialName + "_Albedo", texturePaths[Assimp.TextureType.BaseColor]).Value);
                catalogue.AddTexture(CreateTexture(materialName + "_Normal", texturePaths[Assimp.TextureType.Normals]).Value);
            });

            AssimpForEach(scene->MNumMaterials, scene->MMaterials, (ref Assimp.Material material) => catalogue.AddMaterial(CreateMaterial(assimp, catalogue, ref material)));


            // TODO: Submeshes
            AssimpForEach(scene->MNumMeshes, scene->MMeshes, (ref Assimp.Mesh mesh) =>
            {
                var meshes = new List<Assimp.Mesh>();
                meshes.Add(mesh);

                MeshWithMaterial meshWithMaterial = CreateMeshWithMaterial(assimp, meshes, scene->MMaterials);
                catalogue.AddVertexData(meshWithMaterial.Mesh);
                catalogue.AddDefaultMaterial(meshWithMaterial.Mesh.Name, meshWithMaterial.SubmeshMaterials);
            });

            // Meshes with submeshes are named Mesh-0/Mesh-1/Mesh-2. Group them
            // together before calling `Addmesh`.
            /*scene.Meshes.Sort((lhs, rhs) => string.Compare(lhs.Name, rhs.Name, StringComparison.Ordinal));

            List<Assimp.Mesh> meshes = new(10);
            foreach (Assimp.Mesh mesh in scene.Meshes)
            {
                if (meshes.Count == 0)
                    meshes.Add(mesh);
                else
                {
                    string currentName = meshes[0].Name.Split('-')[0];
                    string meshName = mesh.Name.Split('-')[0];

                    if (currentName != meshName)
                    {
                        MeshWithMaterial meshWithMaterial = CreateMesh(meshes, scene.Materials);
                        catalogue.AddVertexData(meshWithMaterial.Mesh);
                        catalogue.AddDefaultMaterial(meshWithMaterial.Mesh.Name, meshWithMaterial.SubmeshMaterials);
                        meshes.Clear();
                    }

                    meshes.Add(mesh);
                }
            }

            if (meshes.Count > 0)
            {
                MeshWithMaterial meshWithMaterial = CreateMesh(meshes, scene.Materials);
                catalogue.AddVertexData(meshWithMaterial.Mesh);
                catalogue.AddDefaultMaterial(meshWithMaterial.Mesh.Name, meshWithMaterial.SubmeshMaterials);
                meshes.Clear();
            }*/
        }
    }
}
