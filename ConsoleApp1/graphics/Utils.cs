using Vortice.Dxc;
using FluentResults;

namespace ConsoleApp1.Graphics;

public static class Utils
{
    private static Result<IDxcResult> CompileShader(DxcShaderStage shaderStage, string name, KeyValuePair<string, string>[]? defines = null)
    {
        DxcDefine[]? dxcDefines = null;
        if (defines != null)
            dxcDefines = defines.Select(pair => new DxcDefine { Name = pair.Key, Value = pair.Value }).ToArray();

        string source = File.ReadAllText(name);
        var result = DxcCompiler.Compile(shaderStage, source, "main", null, null, dxcDefines);
        if (result.GetStatus().Success)
            return Result.Ok(result);
        else
            return Result.Fail(result.GetErrors());
    }

    public static Result<IDxcResult> CompileVertexShader(string name)
    {
        return CompileShader(DxcShaderStage.Vertex, "shader/vs/" + name);
    }

    public static Result<IDxcResult> CompileVertexShader(string name, KeyValuePair<string, string>[] defines)
    {
        return CompileShader(DxcShaderStage.Vertex, "shader/vs/" + name, defines);
    }

    public static Result<IDxcResult> CompilePixelShader(string name)
    {
        return CompileShader(DxcShaderStage.Pixel, "shader/ps/" + name);
    }

    public static Result<IDxcResult> CompilePixelShader(string name, KeyValuePair<string, string>[] defines)
    {
        return CompileShader(DxcShaderStage.Pixel, "shader/ps/" + name, defines);
    }
}
