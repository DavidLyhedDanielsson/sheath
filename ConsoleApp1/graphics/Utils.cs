using Vortice.Dxc;
using FluentResults;

namespace Graphics;

public static class Utils
{
    private static Result<IDxcResult> CompileShader(DxcShaderStage shaderStage, string name)
    {
        string source = File.ReadAllText(name);
        var result = DxcCompiler.Compile(shaderStage, source, "main");
        if (result.GetStatus().Success)
            return Result.Ok(result);
        else
            return Result.Fail(result.GetErrors());
    }

    public static Result<IDxcResult> CompileVertexShader(string name)
    {
        return CompileShader(DxcShaderStage.Vertex, "shader/vs/" + name);
    }

    public static Result<IDxcResult> CompilePixelShader(string name)
    {
        return CompileShader(DxcShaderStage.Pixel, "shader/ps/" + name);
    }
}
