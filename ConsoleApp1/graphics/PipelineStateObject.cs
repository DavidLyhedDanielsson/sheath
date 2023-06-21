namespace ConsoleApp1;

using Vortice.Direct3D12;
using Vortice.DXGI;
using FluentResults;

public class PipelineStateObject
{
    public ID3D12PipelineState NdcTriangle { get; private set; }

    public static Result<PipelineStateObject> Create(Settings settings, ID3D12Device device, ID3D12RootSignature rootSignature)
    {
        PipelineStateObject pso = new PipelineStateObject();

        var vertexShader = Graphics.Utils.CompileVertexShader("ndc_triangle.hlsl").LogIfFailed().Value;
        var pixelShader = Graphics.Utils.CompilePixelShader("white.hlsl").LogIfFailed().Value;

        pso.NdcTriangle = device.CreateGraphicsPipelineState(new GraphicsPipelineStateDescription
        {
            RootSignature = rootSignature,
            VertexShader = vertexShader.GetObjectBytecodeMemory(),
            PixelShader = pixelShader.GetObjectBytecodeMemory(),
            DomainShader = null,
            HullShader = null,
            GeometryShader = null,
            StreamOutput = null,
            BlendState = BlendDescription.Opaque,
            SampleMask = uint.MaxValue,
            RasterizerState = new RasterizerDescription
            {
                FillMode = (FillMode)FillMode.Solid,
                CullMode = (CullMode)CullMode.Back,
                FrontCounterClockwise = true,
                DepthBias = 0,
                DepthBiasClamp = 0,
                SlopeScaledDepthBias = 0,
                DepthClipEnable = false,
                MultisampleEnable = false,
                AntialiasedLineEnable = false,
                ForcedSampleCount = 0,
                ConservativeRaster = ConservativeRasterizationMode.Off
            },
            DepthStencilState = DepthStencilDescription.None,
            InputLayout = null,
            IndexBufferStripCutValue = IndexBufferStripCutValue.Disabled,
            PrimitiveTopologyType = PrimitiveTopologyType.Triangle,
            RenderTargetFormats = new Format[]
            {
                settings.Graphics.BackBufferFormat,
            },
            DepthStencilFormat = settings.Graphics.DepthStencilFormat,
            SampleDescription = SampleDescription.Default,
            NodeMask = 0,
            CachedPSO = default,
            Flags = PipelineStateFlags.None
        });

        return Result.Ok(pso);
    }
}
