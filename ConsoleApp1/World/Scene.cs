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

    public void Render(GraphicsState graphicsState, ID3D12Resource instanceDataBuffer, ID3D12Resource perDrawBuffer)
    {
        int instanceCounter = 0;
        int drawCounter = 0;

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
                    data += drawCounter * 256;

                    int textureId = submesh.Surface.AlbedoTexture.ID;
                    int vertexBufferId = submesh.VIBufferView.VertexBufferId;
                    int instanceDataStartOffset = instanceCounter;

                    Buffer.MemoryCopy(&vertexBufferId, data, 4, 4);
                    Buffer.MemoryCopy(&textureId, data + 4, 4, 4);
                    Buffer.MemoryCopy(&instanceDataStartOffset, data + 4 + 4, 4, 4);

                    perDrawBuffer.Unmap(0);
                }

                instanceCounter += instanceData.Count;

                graphicsState.commandList.SetGraphicsRootConstantBufferView(0, perDrawBuffer.GPUVirtualAddress + (ulong)(drawCounter * 256));
                graphicsState.commandList.DrawIndexedInstanced(submesh.VIBufferView.IndexCount, instanceData.Count, submesh.VIBufferView.IndexStart, 0, 0);

                drawCounter++;
            }
        }
    }
}