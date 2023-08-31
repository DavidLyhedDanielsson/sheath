using System.Diagnostics;
using Application.Asset;
using Application.Models;
using FluentResults;
using Silk.NET.Maths;
using Vortice.Direct3D12;
using Vortice.Dxc;
using Vortice.DXGI;
using static System.Runtime.InteropServices.Marshal;

namespace Application.Graphics;

public interface IResourceBuilder
{
    public static abstract Mesh CreateMesh(GraphicsState graphicsState, HeapState heapState, VertexData mesh);
    public static abstract Texture CreateTexture(GraphicsState graphicsState, HeapState heapState, TextureData texture);
    public static abstract Surface CreateSurface(Settings settings, GraphicsState graphicsState, HeapState heapState, Material material, Dictionary<string, Texture> textures);
    public static abstract Result<HeapState> CreateHeapState(GraphicsState graphicsState);
}

public class LinearResourceBuilder : IResourceBuilder
{
    public static Mesh CreateMesh(
        GraphicsState graphicsState
        , HeapState heapState
        , VertexData vertexData)
    {
        ulong totalVertexCount = (ulong)vertexData.Vertices.Length;
        ulong totalIndexCount = (ulong)vertexData.Submeshes.Sum(submesh => submesh.Indices.Length);

        ulong vertexByteSize = (ulong)SizeOf(typeof(Vertex));
        ulong verticesByteSize = vertexByteSize * totalVertexCount;

        ulong indexByteSize = (ulong)SizeOf(typeof(uint));
        ulong indicesByteSize = indexByteSize * totalIndexCount;

        var device = graphicsState.Device;

        ID3D12Resource vertexBuffer = heapState.VertexHeap.AppendBuffer(device, verticesByteSize);
        device.CreateShaderResourceView(vertexBuffer, new ShaderResourceViewDescription
        {
            Format = Format.Unknown,
            ViewDimension = ShaderResourceViewDimension.Buffer,
            Shader4ComponentMapping = ShaderComponentMapping.Default,
            Buffer = new BufferShaderResourceView
            {
                FirstElement = 0,
                Flags = BufferShaderResourceViewFlags.None,
                NumElements = checked((int)totalVertexCount),
                StructureByteStride = checked((int)vertexByteSize),
            },
        }, heapState.CbvUavSrvDescriptorHeap.Segments[HeapConfig.Segments.vertexBuffers].NextCpuHandle()
        );

        ID3D12Resource indexBuffer = heapState.IndexHeap.AppendBuffer(device, indicesByteSize);

        List<VIBufferView> viBufferViews = new(vertexData.Submeshes.Length);

        unsafe
        {
            fixed (void* source = &vertexData.Vertices[0])
            {
                heapState.UploadBuffer.QueueBufferUpload(graphicsState.CommandList, vertexBuffer, 0, source, verticesByteSize);
            }

            int indexStart = 0;

            ulong offset = 0;
            foreach (var submesh in vertexData.Submeshes)
            {
                ulong submeshIndicesByteSize = (ulong)submesh.Indices.Length * indexByteSize;
                fixed (void* source = &submesh.Indices[0])
                {
                    heapState.UploadBuffer.QueueBufferUpload(graphicsState.CommandList, indexBuffer, offset, source, submeshIndicesByteSize);
                }
                offset += submeshIndicesByteSize;

                viBufferViews.Add(new VIBufferView
                {
                    VertexBufferId = heapState.CbvUavSrvDescriptorHeap.Segments[HeapConfig.Segments.vertexBuffers].Used - 1,
                    VertexBuffer = vertexBuffer,
                    IndexBuffer = indexBuffer,
                    IndexStart = indexStart,
                    IndexCount = checked(submesh.Indices.Length),
                    IndexBufferTotalCount = checked((int)totalIndexCount),
                });

                indexStart += submesh.Indices.Length;
            }
        }

        // TODO: Can't be here if the data is queued for copying
        graphicsState.CommandList.ResourceBarrierTransition(
            vertexBuffer
            , ResourceStates.CopyDest
            , ResourceStates.AllShaderResource
        );
        graphicsState.CommandList.ResourceBarrierTransition(
            indexBuffer
            , ResourceStates.CopyDest
            , ResourceStates.IndexBuffer
        );

        return new Mesh
        {
            BufferViews = viBufferViews.ToArray()
        };
    }

    public static Texture CreateTexture(GraphicsState graphicsState, HeapState heapState, TextureData textureData)
    {
        ID3D12Resource resource = heapState.TextureHeap.AppendTexture2D(
            graphicsState.Device
            , ResourceDescription.Texture2D(Utils.GetDXGIFormat(textureData.Channels, textureData.ChannelByteSize), (uint)textureData.Width, (uint)textureData.Height, 1, 1)
        );

        int textureId = heapState.CbvUavSrvDescriptorHeap.Segments[HeapConfig.Segments.textures].Used;
        graphicsState.Device.CreateShaderResourceView(resource,
            new ShaderResourceViewDescription
            {
                Format = Utils.GetDXGIFormat(textureData.Channels, textureData.ChannelByteSize),
                ViewDimension = ShaderResourceViewDimension.Texture2D,
                Shader4ComponentMapping = ShaderComponentMapping.Default,
                Texture2D = new Texture2DShaderResourceView
                {
                    MostDetailedMip = 0,
                    MipLevels = 1,
                    PlaneSlice = 0,
                    ResourceMinLODClamp = 0.0f,
                },
            },
            heapState.CbvUavSrvDescriptorHeap.Segments[HeapConfig.Segments.textures].NextCpuHandle()
        );

        unsafe
        {
            heapState.UploadBuffer.QueueTextureUpload(graphicsState.CommandList, resource, textureData);
        }

        return new Texture { ID = textureId };
    }

    public static void RecreatePsos(Settings settings, GraphicsState graphicsState, string? vertexShaderPath, string? pixelShaderPath)
    {
        foreach (PSO pso in graphicsState.livePsos)
        {
            if (pso.VertexShader != vertexShaderPath && pso.PixelShader != pixelShaderPath)
                continue;

            IDxcResult vertexShader;
            if (vertexShaderPath != null)
                vertexShader = Graphics.Utils.CompileVertexShader(vertexShaderPath).LogIfFailed().Value;
            else
                vertexShader = Graphics.Utils.CompileVertexShader(pso.VertexShader).LogIfFailed().Value;

            IDxcResult pixelShader;
            if (pixelShaderPath != null)
                pixelShader = Graphics.Utils.CompilePixelShader(pixelShaderPath).LogIfFailed().Value;
            else
                pixelShader = Graphics.Utils.CompilePixelShader(pso.PixelShader).LogIfFailed().Value;

            var pipelineState = graphicsState.Device.CreateGraphicsPipelineState(new GraphicsPipelineStateDescription
            {
                RootSignature = graphicsState.RootSignature,
                VertexShader = vertexShader.GetObjectBytecodeMemory(),
                PixelShader = pixelShader.GetObjectBytecodeMemory(),
                DomainShader = null,
                HullShader = null,
                GeometryShader = null,
                StreamOutput = null,
                BlendState = BlendDescription.Opaque,
                SampleMask = uint.MaxValue,
                RasterizerState = pso.RasterizerDescription,
                DepthStencilState = DepthStencilDescription.ReverseZ,
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

            pso.ID3D12PipelineState.Release();
            pso.ID3D12PipelineState = pipelineState;
        }
    }

    public static Surface CreateSurface(Settings settings, GraphicsState graphicsState, HeapState heapState, Material material, Dictionary<string, Texture> textures)
    {
        var vertexShader = Graphics.Utils.CompileVertexShader("vertex.hlsl").LogIfFailed().Value;

        string pixelShaderPath = material.AlbedoTextureHasAlpha ? "pixel_alphamask.hlsl" : "pixel.hlsl";
        var pixelShader = Graphics.Utils.CompilePixelShader(pixelShaderPath).LogIfFailed().Value;

        var rasterizerDescription = new RasterizerDescription
        {
            FillMode = FillMode.Solid,
            CullMode = material.AlbedoTextureHasAlpha ? CullMode.None : CullMode.Back,
            FrontCounterClockwise = true,
            DepthBias = 0,
            DepthBiasClamp = 0,
            SlopeScaledDepthBias = 0,
            DepthClipEnable = true,
            MultisampleEnable = false,
            AntialiasedLineEnable = false,
            ForcedSampleCount = 0,
            ConservativeRaster = ConservativeRasterizationMode.Off
        };

        var pipelineState = graphicsState.Device.CreateGraphicsPipelineState(new GraphicsPipelineStateDescription
        {
            RootSignature = graphicsState.RootSignature,
            VertexShader = vertexShader.GetObjectBytecodeMemory(),
            PixelShader = pixelShader.GetObjectBytecodeMemory(),
            DomainShader = null,
            HullShader = null,
            GeometryShader = null,
            StreamOutput = null,
            BlendState = BlendDescription.Opaque,
            SampleMask = uint.MaxValue,
            RasterizerState = rasterizerDescription,
            DepthStencilState = DepthStencilDescription.ReverseZ,
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

        textures.TryGetValue(material.AlbedoTexture, out Texture? albedoTexture);
        Debug.Assert(albedoTexture != null);

        textures.TryGetValue(material.NormalTexture, out Texture? normalTexture);
        Debug.Assert(normalTexture != null);

        textures.TryGetValue(material.ORMTexture, out Texture? ormTexture);
        Debug.Assert(ormTexture != null);

        PSO pso = new PSO
        {
            VertexShader = "vertex.hlsl",
            PixelShader = "pixel.hlsl",
            BackfaceCulling = !material.AlbedoTextureHasAlpha,
            ID = material.AlbedoTextureHasAlpha ? 1 : 0, // TODO :)
            RasterizerDescription = rasterizerDescription,
            ID3D12PipelineState = pipelineState,
        };

        graphicsState.livePsos.Add(pso);

        return new Surface
        {
            ID = heapState.SurfaceCounter++,
            PSO = pso,
            AlbedoTexture = albedoTexture,
            NormalTexture = normalTexture,
            ORMTexture = ormTexture,
        };
    }

    public static Surface CreateBillboardSurface(Settings settings, GraphicsState graphicsState, HeapState heapState, Texture albedoTexture)
    {
        var vertexShader = Graphics.Utils.CompileVertexShader("billboard.hlsl").LogIfFailed().Value;
        var pixelShader = Graphics.Utils.CompilePixelShader("billboard.hlsl").LogIfFailed().Value;

        var rasterizerDescription = new RasterizerDescription
        {
            FillMode = FillMode.Solid,
            CullMode = CullMode.None,
            FrontCounterClockwise = true,
            DepthBias = 0,
            DepthBiasClamp = 0,
            SlopeScaledDepthBias = 0,
            DepthClipEnable = false,
            MultisampleEnable = false,
            AntialiasedLineEnable = false,
            ForcedSampleCount = 0,
            ConservativeRaster = ConservativeRasterizationMode.Off
        };

        var pipelineState = graphicsState.Device.CreateGraphicsPipelineState(new GraphicsPipelineStateDescription
        {
            RootSignature = graphicsState.RootSignature,
            VertexShader = vertexShader.GetObjectBytecodeMemory(),
            PixelShader = pixelShader.GetObjectBytecodeMemory(),
            DomainShader = null,
            HullShader = null,
            GeometryShader = null,
            StreamOutput = null,
            BlendState = BlendDescription.Opaque,
            SampleMask = uint.MaxValue,
            RasterizerState = rasterizerDescription,
            DepthStencilState = DepthStencilDescription.ReverseZ,
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

        PSO pso = new PSO
        {
            VertexShader = "billboard.hlsl",
            PixelShader = "billboard.hlsl",
            BackfaceCulling = true,
            ID = 2, // TODO :)
            RasterizerDescription = rasterizerDescription,
            ID3D12PipelineState = pipelineState,
        };

        graphicsState.livePsos.Add(pso);

        return new Surface
        {
            ID = heapState.SurfaceCounter++,
            PSO = pso,
            AlbedoTexture = albedoTexture,
            NormalTexture = null,
            ORMTexture = null,
        };
    }

    public static Surface CreateBlinnPhongSurface(Settings settings, GraphicsState graphicsState, HeapState heapState, Material material, Dictionary<string, Texture> textures)
    {
        var vertexShader = Graphics.Utils.CompileVertexShader("vertex.hlsl").LogIfFailed().Value;

        string pixelShaderPath = "pixel_blinnphong.hlsl";
        var pixelShader = Graphics.Utils.CompilePixelShader(pixelShaderPath).LogIfFailed().Value;

        var rasterizerDescription = new RasterizerDescription
        {
            FillMode = FillMode.Solid,
            CullMode = CullMode.Back,
            FrontCounterClockwise = true,
            DepthBias = 0,
            DepthBiasClamp = 0,
            SlopeScaledDepthBias = 0,
            DepthClipEnable = true,
            MultisampleEnable = false,
            AntialiasedLineEnable = false,
            ForcedSampleCount = 0,
            ConservativeRaster = ConservativeRasterizationMode.Off
        };

        var pipelineState = graphicsState.Device.CreateGraphicsPipelineState(new GraphicsPipelineStateDescription
        {
            RootSignature = graphicsState.RootSignature,
            VertexShader = vertexShader.GetObjectBytecodeMemory(),
            PixelShader = pixelShader.GetObjectBytecodeMemory(),
            DomainShader = null,
            HullShader = null,
            GeometryShader = null,
            StreamOutput = null,
            BlendState = BlendDescription.Opaque,
            SampleMask = uint.MaxValue,
            RasterizerState = rasterizerDescription,
            DepthStencilState = DepthStencilDescription.ReverseZ,
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

        textures.TryGetValue(material.AlbedoTexture, out Texture? albedoTexture);
        Debug.Assert(albedoTexture != null);

        textures.TryGetValue(material.NormalTexture, out Texture? normalTexture);
        Debug.Assert(normalTexture != null);

        textures.TryGetValue(material.ORMTexture, out Texture? ormTexture);
        Debug.Assert(ormTexture != null);

        PSO pso = new PSO
        {
            VertexShader = "vertex.hlsl",
            PixelShader = "pixel_blinnphong.hlsl",
            BackfaceCulling = false,
            ID = 3, // TODO :)
            RasterizerDescription = rasterizerDescription,
            ID3D12PipelineState = pipelineState,
        };

        graphicsState.livePsos.Add(pso);

        return new Surface
        {
            ID = heapState.SurfaceCounter++,
            PSO = pso,
            AlbedoTexture = albedoTexture,
            NormalTexture = normalTexture,
            ORMTexture = ormTexture,
        };
    }
    public static Surface CreateTerrainSurface(Settings settings, GraphicsState graphicsState, HeapState heapState)
    {
        var vertexShader = Graphics.Utils.CompileVertexShader("terrain.hlsl").LogIfFailed().Value;
        var pixelShader = Graphics.Utils.CompilePixelShader("terrain.hlsl").LogIfFailed().Value;

        RasterizerDescription rasterizerDecsription = new RasterizerDescription
        {
            FillMode = FillMode.Solid,
            CullMode = CullMode.None, // TODO
            FrontCounterClockwise = true,
            DepthBias = 0,
            DepthBiasClamp = 0,
            SlopeScaledDepthBias = 0,
            DepthClipEnable = true,
            MultisampleEnable = false,
            AntialiasedLineEnable = false,
            ForcedSampleCount = 0,
            ConservativeRaster = ConservativeRasterizationMode.Off
        };

        var pipelineState = graphicsState.Device.CreateGraphicsPipelineState(new GraphicsPipelineStateDescription
        {
            RootSignature = graphicsState.RootSignature,
            VertexShader = vertexShader.GetObjectBytecodeMemory(),
            PixelShader = pixelShader.GetObjectBytecodeMemory(),
            DomainShader = null,
            HullShader = null,
            GeometryShader = null,
            StreamOutput = null,
            BlendState = BlendDescription.Opaque,
            SampleMask = uint.MaxValue,
            RasterizerState = rasterizerDecsription,
            DepthStencilState = DepthStencilDescription.ReverseZ,
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

        return new Surface
        {
            ID = heapState.SurfaceCounter++,
            PSO = new PSO
            {
                VertexShader = "terrain.hlsl",
                PixelShader = "terrain.hlsl",
                BackfaceCulling = false,
                ID = 4, // TODO :)
                RasterizerDescription = rasterizerDecsription,
                ID3D12PipelineState = pipelineState,
            },
            AlbedoTexture = new Texture { ID = -1, },
            NormalTexture = new Texture { ID = -1, },
            ORMTexture = new Texture { ID = -1, },
        };
    }

    public static Result<HeapState> CreateHeapState(GraphicsState graphicsState)
    {
        DescriptorHeap cbvSrvUavHeap;
        {
            ID3D12DescriptorHeap id3d12DescriptorHeap = graphicsState.Device.CreateDescriptorHeap(
                new DescriptorHeapDescription(
                    DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView
                    , HeapConfig.ArraySize.total
                    , DescriptorHeapFlags.ShaderVisible
                )
            );

            int handleSize = graphicsState.Device.GetDescriptorHandleIncrementSize(DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView);
            cbvSrvUavHeap = new DescriptorHeap.Builder(id3d12DescriptorHeap, handleSize)
                .WithSegment(HeapConfig.ArraySize.cbvs)
                .WithSegment(HeapConfig.ArraySize.textures)
                .WithSegment(HeapConfig.ArraySize.vertexBuffers)
                .WithSegment(HeapConfig.ArraySize.surfaces)
                .WithSegment(HeapConfig.ArraySize.instanceDatas)
                .Build();
        }

        const int uploadHeapSize = 256 * 1024 * 1024;
        ID3D12Heap uploadHeap =
            graphicsState.Device.CreateHeap<ID3D12Heap>(
                new HeapDescription(uploadHeapSize, HeapType.Upload)
            );
        ID3D12Resource uploadBuffer = graphicsState.Device.CreatePlacedResource<ID3D12Resource>(
            uploadHeap
            , 0
            , ResourceDescription.Buffer(uploadHeapSize)
            , ResourceStates.Common
        );

        const ulong vertexHeapSize = 1024 * 1024 * 1024;
        Heap vertexHeap = Heap.New(
            graphicsState.Device.CreateHeap<ID3D12Heap>(
                new HeapDescription(vertexHeapSize, HeapType.Default)
            )
            , vertexHeapSize
        );

        const ulong indexHeapSize = 512 * 1024 * 1024;
        Heap indexHeap = Heap.New(
            graphicsState.Device.CreateHeap<ID3D12Heap>(
                new HeapDescription(indexHeapSize, HeapType.Default)
            )
            , indexHeapSize
        );

        const ulong textureHeapSize = 2048L * 1024 * 1024;
        Heap textureHeap = Heap.New(
            graphicsState.Device.CreateHeap<ID3D12Heap>(
                new HeapDescription(textureHeapSize, HeapType.Default)
            )
            , textureHeapSize
        );

        Heap instanceDataHeap;
        ID3D12Resource instanceDataBuffer;
        {
            const ulong instanceDataHeapSize = 64 * 1024 * 1024;
            instanceDataHeap = Heap.New(
                graphicsState.Device.CreateHeap<ID3D12Heap>(
                    new HeapDescription(instanceDataHeapSize, HeapType.Upload)
                )
                , instanceDataHeapSize
            );

            instanceDataBuffer = graphicsState.Device.CreatePlacedResource<ID3D12Resource>(
                instanceDataHeap.ID3D12Heap,
                0,
                ResourceDescription.Buffer(instanceDataHeapSize),
                ResourceStates.AllShaderResource);

            graphicsState.Device.CreateShaderResourceView(instanceDataBuffer,
                new ShaderResourceViewDescription
                {
                    Format = Format.Unknown,
                    ViewDimension = ShaderResourceViewDimension.Buffer,
                    Shader4ComponentMapping = ShaderComponentMapping.Default,
                    Buffer = new BufferShaderResourceView
                    {
                        FirstElement = 0,
                        NumElements = (int)(instanceDataHeapSize / (4 * 4)),
                        StructureByteStride = 4 * 4,
                        Flags = BufferShaderResourceViewFlags.None,
                    }
                }, cbvSrvUavHeap.Segments[HeapConfig.Segments.instanceDatas].NextCpuHandle());
        }

        Heap perDrawConstantBufferHeap;
        ID3D12Resource perDrawBuffer;
        {
            const ulong perDrawConstantBufferHeapSize = 2 * 1024 * 1024;
            perDrawConstantBufferHeap = Heap.New(
                graphicsState.Device.CreateHeap<ID3D12Heap>(
                    new HeapDescription(perDrawConstantBufferHeapSize, HeapType.Upload)
                )
                , perDrawConstantBufferHeapSize
            );

            perDrawBuffer = graphicsState.Device.CreatePlacedResource<ID3D12Resource>(
                perDrawConstantBufferHeap.ID3D12Heap,
                0,
                ResourceDescription.Buffer(perDrawConstantBufferHeapSize),
                ResourceStates.VertexAndConstantBuffer);

            /*graphicsState.device.CreateShaderResourceView(perDrawBuffer,
                new ShaderResourceViewDescription
                {
                    Format = Format.Unknown,
                    ViewDimension = ShaderResourceViewDimension.Buffer,
                    Shader4ComponentMapping = ShaderComponentMapping.Default,
                    Buffer = new BufferShaderResourceView
                    {
                        FirstElement = 0,
                        NumElements = (int)(perDrawConstantBufferHeapSize / (4 * 4)),
                        StructureByteStride = 4 * 4,
                        Flags = BufferShaderResourceViewFlags.None,
                    }
                }, descriptorHeap.Segments[HeapConfig.Segments.instanceDatas].NextCpuHandle());*/
        }

        DescriptorHeap rtvHeap;
        {
            // Environment cube map
            ID3D12DescriptorHeap id3d12DescriptorHeap = graphicsState.Device.CreateDescriptorHeap(
                new DescriptorHeapDescription(
                    DescriptorHeapType.RenderTargetView
                    , 6
                    , DescriptorHeapFlags.None
                )
            );

            int handleSize = graphicsState.Device.GetDescriptorHandleIncrementSize(DescriptorHeapType.RenderTargetView);
            rtvHeap = new DescriptorHeap.Builder(id3d12DescriptorHeap, handleSize)
                .WithSegment(6)
                .Build();
        }

        return new HeapState
        {
            UploadBuffer = new LinearUploader(uploadBuffer, uploadHeapSize),
            UploadHeap = Heap.New(uploadHeap, uploadHeapSize),
            VertexHeap = vertexHeap,
            IndexHeap = indexHeap,
            TextureHeap = textureHeap,
            CbvUavSrvDescriptorHeap = cbvSrvUavHeap,
            RtvDescriptorHeap = rtvHeap,
            InstanceDataHeap = instanceDataHeap,
            InstanceDataBuffer = instanceDataBuffer,
            PerDrawConstantBufferHeap = perDrawConstantBufferHeap,
            PerDrawBuffer = perDrawBuffer,
            SurfaceCounter = 0,
        };
    }
}