using System.Numerics;

public static class AssimpExtensions
{
    public static Vector2 ToVector2(this Assimp.Vector2D vec)
    {
        return new Vector2 { X = vec.X, Y = vec.Y };
    }
    public static Vector2 ToVector2(this Assimp.Vector3D vec)
    {
        return new Vector2 { X = vec.X, Y = vec.Y };
    }
    public static Vector3 ToVector3(this Assimp.Vector3D vec)
    {
        return new Vector3 { X = vec.X, Y = vec.Y, Z = vec.Z };
    }
}

namespace ConsoleApp1
{
    public class Scene
    {
        private class Vertex
        {
            public Vector3 position;
            public Vector3 normal;
            public Vector2 texCoords;
        };
        
        private class Material
        {
            public String name;
        };

        private class SubMesh
        {
            public int StartIndex;
            public int IndexCount;
            public Material Material;
        };
        
        private class Mesh
        {
            public String Name;
            public List<Vertex> Vertices = new();
            public List<int> Indices = new();
            public List<SubMesh> SubMeshes = new();

            public Mesh(String name)
            {
                Name = name;
            }
        };
        
        private Thread _loadThread;
        public bool DoneLoading()
        {
            return !_loadThread.IsAlive;
        }
        public void WaitUntilDoneLoading()
        {
            _loadThread.Join();
        }

        private Dictionary<String, Mesh> _meshes = new();
        private List<Material> _materials = new();
        private Dictionary<String, int> _materialNameIndex = new();

        private void CreateMesh(Assimp.Mesh mesh)
        {
            String[] meshFullName = mesh.Name.Split("-");

            Mesh newMesh;
            if (meshFullName.Length > 1)
            {
                if (!_meshes.ContainsKey(meshFullName[0]))
                    _meshes.Add(meshFullName[0], new Mesh(meshFullName[0]));

                newMesh = _meshes[meshFullName[0]];
            }
            else
                newMesh = new Mesh(meshFullName[0]);

            int indexStartOffset = newMesh.Vertices.Count;
            
            for (int i = 0; i < mesh.VertexCount; ++i)
            {
                var vertex = new Vertex
                {
                    position = mesh.Vertices[i].ToVector3(),
                    normal = mesh.Normals[i].ToVector3(),
                    texCoords = mesh.TextureCoordinateChannels[0][i].ToVector2()
                };
                newMesh.Vertices.Add(vertex);
            }

            int startIndex = newMesh.Indices.Count;
            foreach (var face in mesh.Faces)
            {
                foreach (var index in face.Indices)
                    newMesh.Indices.Add(indexStartOffset + index);
            }
            int indexCount = newMesh.Indices.Count - startIndex;
            
            newMesh.SubMeshes.Add(new SubMesh
            {
                StartIndex = startIndex,
                IndexCount = indexCount,
                Material = _materials[mesh.MaterialIndex],
            });
        }
        
        private void CreateMaterial(Assimp.Material material)
        {
            _materialNameIndex.Add(material.Name, _materials.Count);
            _materials.Add(new Material{name = material.Name});
        }

        public void Import()
        {
            _loadThread = new Thread(() =>
            {
                var importer = new Assimp.AssimpContext();
                var scene = importer.ImportFile("Map/Main.gltf", Assimp.PostProcessPreset.TargetRealTimeQuality);

                foreach (var material in scene.Materials)
                    CreateMaterial(material);

                foreach (var mesh in scene.Meshes)
                    CreateMesh(mesh);
            });
            _loadThread.Start();
        }
    }
};
