namespace Application.Asset;

public class AssetCatalogue
{
    private Dictionary<string, VertexData> _meshes = new();
    private Dictionary<string, Material> _materials = new();
    private Dictionary<string, TextureData> _textures = new();
    // One string per submesh
    private Dictionary<string, string[]> _defaultMaterials = new();

    public bool HasVertexData(string vertexDataId)
    {
        return _meshes.ContainsKey(vertexDataId);
    }

    public VertexData? GetVertexData(string vertexDataId)
    {
        _meshes.TryGetValue(vertexDataId, out var mesh);
        return mesh;
    }

    public void AddVertexData(VertexData vertexDataId)
    {
        _meshes.Add(vertexDataId.Name, vertexDataId);
    }

    public void AddMaterial(Material materialId)
    {
        _materials.Add(materialId.Name, materialId);
    }

    public Material? GetMaterial(string materialId)
    {
        _materials.TryGetValue(materialId, out var material);
        return material;
    }

    public bool HasTexture(string textureId)
    {
        return _textures.ContainsKey(textureId);
    }

    public void AddTexture(TextureData textureDataId)
    {
        _textures.Add(textureDataId.FilePath, textureDataId);
    }

    public TextureData? GetTextureData(string textureDataId)
    {
        _textures.TryGetValue(textureDataId, out var texture);
        return texture;
    }

    public void AddDefaultMaterial(string vertexDataId, string[] submeshMaterials)
    {
        _defaultMaterials.Add(vertexDataId, submeshMaterials);
    }

    public string[]? GetDefaultMaterials(string vertexDataId)
    {
        _defaultMaterials.TryGetValue(vertexDataId, out var material);
        return material;
    }

    public void ForEachTextureData(Action<TextureData> action)
    {
        foreach (var textureData in _textures.Values)
            action(textureData);
    }

    public void ForEachVertexData(Action<VertexData> action)
    {
        foreach (var mesh in _meshes.Values)
            action(mesh);
    }

    public void ForEachMaterial(Action<Material> action)
    {
        foreach (var materials in _materials.Values)
            action(materials);
    }

    // mesh, material
    public void ForEachDefaultMaterial(Action<string, string[]> action)
    {
        foreach (var pair in _defaultMaterials)
            action(pair.Key, pair.Value);
    }
}