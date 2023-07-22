using System.Diagnostics;
using ConsoleApp1.Models;

namespace ConsoleApp1.Graphics;

public static class ModelBuilder
{
    public static Model CreateModel(Mesh mesh, Surface[] surfaces)
    {
        Debug.Assert(mesh.BufferViews.Length == surfaces.Length);

        Model.Submesh[] submeshes = new Model.Submesh[mesh.BufferViews.Length];
        for (int i = 0; i < mesh.BufferViews.Length; ++i)
        {
            submeshes[i] = new Model.Submesh
            {
                VIBufferView = mesh.BufferViews[i],
                Surface = surfaces[i]
            };
        }

        return new Model
        {
            Submeshes = submeshes,
        };
    }
}