namespace ConsoleApp1.Asset;

public class AssetCatalogue
{
    private Dictionary<string, Mesh> _meshes = new();
    private Dictionary<string, Material> _materials = new();
    private Dictionary<string, Texture> _textures = new();
    private Dictionary<Mesh, string[]> _defaultMeshMaterials = new();

    public bool HasMesh(string name)
    {
        return _meshes.ContainsKey(name);
    }

    public Mesh? GetMesh(string name)
    {
        _meshes.TryGetValue(name, out var mesh);
        return mesh;
    }

    public void AddMesh(Mesh mesh)
    {
        _meshes.Add(mesh.Name, mesh);
    }

    public void AddMaterial(Material material)
    {
        _materials.Add(material.Name, material);
    }

    public bool HasTexture(string filePath)
    {
        return _textures.ContainsKey(filePath);
    }

    public void AddTexture(Texture texture)
    {
        _textures.Add(texture.FilePath, texture);
    }

    public Texture? GetTexture(string filePath)
    {
        _textures.TryGetValue(filePath, out var texture);
        return texture;
    }

    public void AddDefaultMaterial(Mesh mesh, string[] submeshMaterials)
    {
        _defaultMeshMaterials.Add(mesh, submeshMaterials);
    }

    public string[]? GetDefaultMaterials(Mesh mesh)
    {
        _defaultMeshMaterials.TryGetValue(mesh, out var material);
        return material;
    }

    public void ForEachMesh(Action<Mesh> action)
    {
        foreach (var mesh in _meshes.Values)
            action(mesh);
    }

    public void ForEachMaterial(Action<Material> action)
    {
        foreach (var materials in _materials.Values)
            action(materials);
    }
}