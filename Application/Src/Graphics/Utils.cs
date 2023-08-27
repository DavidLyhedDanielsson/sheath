using Vortice.Dxc;
using Vortice.DXGI;
using FluentResults;
using System.Diagnostics;
using SharpGen.Runtime;
using System.Runtime.InteropServices;
using System.Diagnostics.Tracing;

namespace Application.Graphics;

public static class Utils
{
#if DEBUG
    public const string ShaderRootPath = "../../../Src/Graphics/shader/";
#else
    public const Path ShaderRootPath = "Src/Graphics/shader";
#endif
    public class ShaderIncludeHandler : CallbackBase, IDxcIncludeHandler
    {
        private readonly string[] _includeDirectories;
        private readonly Dictionary<string, SourceCodeBlob> _sourceFiles = new Dictionary<string, SourceCodeBlob>();

        public ShaderIncludeHandler(params string[] includeDirectories)
        {
            _includeDirectories = includeDirectories;
        }

        protected override void DisposeCore(bool disposing)
        {
            foreach (var pinnedObject in _sourceFiles.Values)
                pinnedObject?.Dispose();

            _sourceFiles.Clear();
        }

        private void Preprocess(byte[] text)
        {
            bool TextInSpan(byte[] source, int offset, string text)
            {
                for (int i = offset; i < source.Length && i - offset < text.Length; ++i)
                {
                    if (source[i] != text[i - offset])
                        return false;
                }

                return true;
            }

            void ReplaceUntil(byte[] source, int offset, string text)
            {
                for (int i = offset; i < source.Length; ++i)
                {
                    if (TextInSpan(source, i, text))
                    {
                        for (int j = i; j < source.Length && j - i < text.Length; ++j)
                            source[j] = 32;

                        return;
                    }
                    else if (source[i] != '\n' && source[i] != '\r')
                    {
                        source[i] = 32; // Space
                    }
                }
            }

            for (int i = 0; i < text.Length; ++i)
            {
                if (TextInSpan(text, i, "#region"))
                    ReplaceUntil(text, i, "#endregion");

                if (TextInSpan(text, i, "#if !HLSL"))
                    ReplaceUntil(text, i, "#endif");
            }
        }

        public SharpGen.Runtime.Result LoadSource(string fileName, out IDxcBlob? includeSource)
        {
            if (fileName.StartsWith("./"))
                fileName = fileName.Substring(2);

            var includeFile = GetFilePath(fileName);

            if (string.IsNullOrEmpty(includeFile))
            {
                includeSource = default;

                return SharpGen.Runtime.Result.Fail;
            }

            if (!_sourceFiles.TryGetValue(includeFile, out SourceCodeBlob? sourceCodeBlob))
            {
                byte[] data = NewMethod(includeFile);

                Preprocess(data);

                sourceCodeBlob = new SourceCodeBlob(data);
                _sourceFiles.Add(includeFile, sourceCodeBlob);
            }

            includeSource = sourceCodeBlob.Blob;

            return SharpGen.Runtime.Result.Ok;
        }

        private static byte[] NewMethod(string includeFile) => File.ReadAllBytes(includeFile);

        private string? GetFilePath(string fileName)
        {
            for (int i = 0; i < _includeDirectories.Length; i++)
            {
                var filePath = Path.GetFullPath(Path.Combine(_includeDirectories[i], fileName));

                if (File.Exists(filePath))
                    return filePath;
            }

            return null;
        }


        private class SourceCodeBlob : IDisposable
        {
            private byte[] _data;
            private GCHandle _dataPointer;
            private IDxcBlobEncoding? _blob;

            internal IDxcBlob? Blob { get => _blob; }

            public SourceCodeBlob(byte[] data)
            {
                _data = data;

                _dataPointer = GCHandle.Alloc(data, GCHandleType.Pinned);

                _blob = DxcCompiler.Utils.CreateBlob(_dataPointer.AddrOfPinnedObject(), data.Length, Dxc.DXC_CP_UTF8);
            }

            public void Dispose()
            {
                //_blob?.Dispose();
                _blob = null;

                if (_dataPointer.IsAllocated)
                    _dataPointer.Free();
                _dataPointer = default;
            }
        }
    }

    //static ShaderIncludeHandler includeHandler = new(ShaderRootPath);

    private static Result<IDxcResult> CompileShader(DxcShaderStage shaderStage, string name, KeyValuePair<string, string>[]? defines = null)
    {
        List<string> arguments = new()
        {
            "-E",
            "main",
            "-T",
            DxcCompiler.GetShaderProfile(shaderStage, DxcShaderModel.Model6_0),
            "-Zi",
            "-Od",
            "-WX",
            "-Qembed_debug",
        };

        arguments.Add("-DHLSL");
        if (defines != null)
            arguments.AddRange(defines.Select(pair => "-D " + pair.Key + "=" + pair.Value));

        using (ShaderIncludeHandler includeHandler = new(ShaderRootPath))
        {
            string source = File.ReadAllText(name);
            var result = DxcCompiler.Compile(source, arguments.ToArray(), includeHandler);
            if (result.GetStatus().Success)
                return FluentResults.Result.Ok(result);
            else
                return FluentResults.Result.Fail(result.GetErrors());
        }
    }

    public static Result<IDxcResult> CompileVertexShader(string name)
    {
        return CompileShader(DxcShaderStage.Vertex, ShaderRootPath + "vs/" + name);
    }

    public static Result<IDxcResult> CompileVertexShader(string name, KeyValuePair<string, string>[] defines)
    {
        return CompileShader(DxcShaderStage.Vertex, ShaderRootPath + "vs/", defines);
    }

    public static Result<IDxcResult> CompilePixelShader(string name)
    {
        return CompileShader(DxcShaderStage.Pixel, ShaderRootPath + "ps/" + name);
    }

    public static Result<IDxcResult> CompilePixelShader(string name, KeyValuePair<string, string>[] defines)
    {
        return CompileShader(DxcShaderStage.Pixel, ShaderRootPath + "ps/" + name, defines);
    }

    public static Format GetDXGIFormat(int channelCount, int channelByteSize)
    {
        Debug.Assert(channelByteSize == 1 || channelByteSize == 4);
        Debug.Assert(channelCount > 0 && channelCount != 3 && channelCount < 5);

        if (channelByteSize == 1)
        {
            return new Format[]
            {
                Format.Unknown,
                Format.R8_UNorm,
                Format.R8G8_UNorm,
                Format.Unknown,
                Format.R8G8B8A8_UNorm,
            }[channelCount];
        }
        else
        {
            return new Format[]
            {
                Format.Unknown,
                Format.R32_Float,
                Format.R32G32_Float,
                Format.Unknown,
                Format.R32G32B32A32_Float,
            }[channelCount];
        }
    }
}
