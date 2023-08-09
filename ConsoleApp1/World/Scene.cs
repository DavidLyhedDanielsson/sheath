using System.Diagnostics;
using System.Runtime.InteropServices;
using ConsoleApp1.Graphics;
using ConsoleApp1.Models;
using Silk.NET.Maths;
using Vortice.Direct3D12;
using static System.Runtime.InteropServices.Marshal;

namespace ConsoleApp1.World;

public class Scene
{
    private struct SubmeshRef
    {
        public required Model Model { get; init; }
        public required int Submesh { get; init; }
    };

    private Dictionary<Model, List<InstanceData>> _instances = new();
    private Dictionary<int, List<SubmeshRef>> _psos = new();
    private Dictionary<int, PSO> _psoMapping = new(); // :(

    public Box3D<float> GetBounds()
    {
        var min = new Vector4D<float>(float.MaxValue);
        var max = new Vector4D<float>(float.MinValue);

        foreach (var instance in _instances)
        {
            foreach (var instanceData in instance.Value)
            {
                min = Vector4D.Min(min, instanceData.transform.Column4);
                max = Vector4D.Max(max, instanceData.transform.Column4);
            }
        }

        return new Box3D<float>(
            new Vector3D<float> { X = min.X, Y = min.Y, Z = min.Z },
            new Vector3D<float> { X = max.X, Y = max.Y, Z = max.Z }
        );
    }

    public void AddInstancesFromFile(Showroom showroom, string filePath)
    {
        var importer = new Assimp.AssimpContext();
        var scene = importer.ImportFile(filePath, Assimp.PostProcessPreset.TargetRealTimeQuality);

        string[] meshNames = scene.Meshes.Select(mesh => mesh.Name).ToArray();

        Action<Assimp.Node> AddInstancesFrom = null;
        AddInstancesFrom = (Assimp.Node node) =>
        {
            if (node.HasMeshes)
            {
                foreach (int meshIndex in node.MeshIndices)
                {
                    string meshName = meshNames[meshIndex];
                    string[] splitName = meshName.Split('-');

                    var t = node.Transform;

                    Matrix4X4<float> transform = new(
                        t.A1, t.B1, t.C1, t.D1,
                        t.A2, t.B2, t.C2, t.D2,
                        t.A3, t.B3, t.C3, t.D3,
                        t.A4, t.B4, t.C4, t.D4
                        );

                    var m = Matrix4X4.CreateTranslation(1.0f, 0.0f, 0.0f);

                    if (splitName[1] == "0")
                        AddInstance(showroom.GetShowcase(splitName[0]).Model, new InstanceData { transform = transform });
                }
            }

            foreach (Assimp.Node child in node.Children)
            {
                AddInstancesFrom(child);
            }
        };

        AddInstancesFrom(scene.RootNode);
    }

    public void AddInstance(Model model, InstanceData instanceData)
    {
        instanceData.transform = Matrix4X4.Transpose<float>(instanceData.transform);

        _instances.TryGetValue(model, out var instance);
        if (instance != null)
        {
            // Simple case; pso info has already been added for this model
            instance.Add(instanceData);
            return;
        }

        _instances.Add(model, new List<InstanceData>() { instanceData });

        for (int i = 0; i < model.Submeshes.Length; ++i)
        {
            var submesh = model.Submeshes[i];

            List<SubmeshRef> submeshRefs;
            if (_psos.ContainsKey(submesh.Surface.PSO.ID))
                _psos.TryGetValue(submesh.Surface.PSO.ID, out submeshRefs!);
            else
            {
                submeshRefs = new();
                _psos.Add(submesh.Surface.PSO.ID, submeshRefs);
                _psoMapping.Add(submesh.Surface.PSO.ID, submesh.Surface.PSO);
            }

            submeshRefs.Add(new SubmeshRef
            {
                Model = model,
                Submesh = i,
            });
        }
    }

    public int DrawCounter { get; private set; } = 0;

    public void Render(GraphicsState graphicsState, ID3D12Resource instanceDataBuffer, ID3D12Resource perDrawBuffer)
    {
        int instanceCounter = 0;
        DrawCounter = 0;

        foreach (KeyValuePair<int, List<SubmeshRef>> psoRenderInfo in _psos)
        {
            var psoId = psoRenderInfo.Key;
            var submeshReferences = psoRenderInfo.Value;

            _psoMapping.TryGetValue(psoId, out PSO? pso);
            Debug.Assert(pso != null);

            graphicsState.commandList.SetPipelineState(pso.ID3D12PipelineState);

            foreach (SubmeshRef submeshRef in submeshReferences)
            {
                Model.Submesh submesh = submeshRef.Model.Submeshes[submeshRef.Submesh];

                graphicsState.commandList.IASetIndexBuffer(new IndexBufferView(submesh.VIBufferView.IndexBuffer.GPUVirtualAddress, submesh.VIBufferView.IndexBufferTotalCount * SizeOf(typeof(uint)), Vortice.DXGI.Format.R32_UInt));

                _instances.TryGetValue(submeshRef.Model, out List<InstanceData>? instanceData);
                Debug.Assert(instanceData != null);

                ReadOnlySpan<InstanceData> d = CollectionsMarshal.AsSpan(instanceData);
                instanceDataBuffer.SetData(d, instanceCounter * (4 * 4 * 4));

                unsafe
                {
                    byte* data;
                    perDrawBuffer.Map(0, (void**)&data);
                    data += DrawCounter * 256;

                    int textureId = submesh.Surface.AlbedoTexture.ID;
                    int vertexBufferId = submesh.VIBufferView.VertexBufferId;
                    int instanceDataStartOffset = instanceCounter;

                    Buffer.MemoryCopy(&vertexBufferId, data, 4, 4);
                    Buffer.MemoryCopy(&textureId, data + 4, 4, 4);
                    Buffer.MemoryCopy(&instanceDataStartOffset, data + 4 + 4, 4, 4);

                    perDrawBuffer.Unmap(0);
                }

                instanceCounter += instanceData.Count;

                graphicsState.commandList.SetGraphicsRootConstantBufferView(0, perDrawBuffer.GPUVirtualAddress + (ulong)(DrawCounter * 256));
                graphicsState.commandList.DrawIndexedInstanced(submesh.VIBufferView.IndexCount, instanceData.Count, submesh.VIBufferView.IndexStart, 0, 0);

                DrawCounter += 1;
            }
        }
    }
}