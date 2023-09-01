using ImGuiNET;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.DXGI;
using Silk.NET.Maths;
using static System.Runtime.InteropServices.Marshal;
using System.Numerics;
using Range = Vortice.Direct3D12.Range;
using System.Diagnostics;

namespace ImGuiBackend
{
    internal class ImGui_ImplDX12_RenderBuffers
    {
        internal ID3D12Resource? IndexBuffer;
        internal ID3D12Resource? VertexBuffer;
        internal int IndexBufferSize;
        internal int VertexBufferSize;
    };
    internal class ImGui_ImplDX12_Data
    {
        internal ID3D12Device pd3dDevice;
        internal ID3D12RootSignature pRootSignature;
        internal ID3D12PipelineState pPipelineState;
        internal Vortice.DXGI.Format RTVFormat;
        internal ID3D12Resource pFontTextureResource;
        internal CpuDescriptorHandle hFontSrvCpuDescHandle;
        internal GpuDescriptorHandle hFontSrvGpuDescHandle;
        internal uint numFramesInFlight;

        internal ImGui_ImplDX12_RenderBuffers[] FrameResources;
        internal uint frameIndex;
    };
    internal struct VERTEX_CONSTANT_BUFFER_DX12
    {
        internal Matrix4X4<float> mvp;
    }
    public class Renderer
    {
        ImGui_ImplDX12_Data bd;

        /*static ImGui_ImplDX12_Data? ImGui_ImplDX12_GetBackendData()
        {
            unsafe
            {
                return ImGui.GetCurrentContext() != null ? *(ImGui_ImplDX12_Data*)ImGui.GetIO().BackendRendererUserData : null;
            }
        }*/

        /*static*/
        private void ImGui_ImplDX12_SetupRenderState(ImDrawDataPtr draw_data, ID3D12GraphicsCommandList ctx, ImGui_ImplDX12_RenderBuffers fr)
        {
            //ImGui_ImplDX12_Data bd = ImGui_ImplDX12_GetBackendData()!;

            // Setup orthographic projection matrix into our constant buffer
            // Our visible imgui space lies from draw_data.DisplayPos (top left) to draw_data.DisplayPos+data_data.DisplaySize (bottom right).
            VERTEX_CONSTANT_BUFFER_DX12 vertex_constant_buffer;
            {
                float L = draw_data.DisplayPos.X;
                float R = draw_data.DisplayPos.X + draw_data.DisplaySize.X;
                float T = draw_data.DisplayPos.Y;
                float B = draw_data.DisplayPos.Y + draw_data.DisplaySize.Y;
                // TODO: VERIFY ORDER HERE
                vertex_constant_buffer.mvp = new(
                     2.0f / (R - L), 0.0f, 0.0f, 0.0f,
                     0.0f, 2.0f / (T - B), 0.0f, 0.0f,
                     0.0f, 0.0f, 0.5f, 0.0f,
                     (R + L) / (L - R), (T + B) / (B - T), 0.5f, 1.0f
                );
            }

            // Setup viewport
            Vortice.Mathematics.Viewport vp = new()
            {
                Width = draw_data.DisplaySize.X,
                Height = draw_data.DisplaySize.Y,
                MinDepth = 0.0f,
                MaxDepth = 1.0f,
                X = 0.0f,
                Y = 0.0f,
            };
            ctx.RSSetViewports(vp);

            // Bind shader and vertex buffers
            int stride = SizeOf<ImDrawVert>();
            uint offset = 0;
            VertexBufferView vbv = new()
            {
                BufferLocation = fr.VertexBuffer!.GPUVirtualAddress + offset,
                SizeInBytes = fr.VertexBufferSize * stride,
                StrideInBytes = stride,
            };
            ctx.IASetVertexBuffers(0, vbv);
            IndexBufferView ibv = new()
            {
                BufferLocation = fr.IndexBuffer!.GPUVirtualAddress,
                SizeInBytes = fr.IndexBufferSize * SizeOf<ushort>(),
                Format = SizeOf<ushort>() == 2 ? Format.R16_UInt : Format.R32_UInt,
            };
            ctx.IASetIndexBuffer(ibv);
            ctx.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            ctx.SetPipelineState(bd.pPipelineState);
            ctx.SetGraphicsRootSignature(bd.pRootSignature);
            unsafe
            {
                ctx.SetGraphicsRoot32BitConstants(0, 16, &vertex_constant_buffer, 0);
            }

            // Setup blend factor
            ctx.OMSetBlendFactor(0.0f, 0.0f, 0.0f, 0.0f);
        }

        public void ImGui_ImplDX12_RenderDrawData(ImDrawDataPtr draw_data, ID3D12GraphicsCommandList ctx)
        {
            // Avoid rendering when minimized
            if (draw_data.DisplaySize.X <= 0.0f || draw_data.DisplaySize.Y <= 0.0f)
                return;

            // FIXME: I'm assuming that this only gets called once per frame!
            // If not, we can't just re-allocate the IB or VB, we'll have to do a proper allocator.
            //ImGui_ImplDX12_Data bd = ImGui_ImplDX12_GetBackendData().Value;
            bd.frameIndex++;
            ImGui_ImplDX12_RenderBuffers fr = bd.FrameResources[bd.frameIndex % bd.numFramesInFlight];

            // Create and grow vertex/index buffers if needed
            if (fr.VertexBuffer == null || fr.VertexBufferSize < draw_data.TotalVtxCount)
            {
                fr.VertexBuffer?.Dispose();
                fr.VertexBufferSize = draw_data.TotalVtxCount + 5000;
                HeapProperties props = new()
                {
                    Type = HeapType.Upload,
                    CPUPageProperty = CpuPageProperty.Unknown,
                    MemoryPoolPreference = MemoryPool.Unknown,
                };
                ResourceDescription desc = new()
                {
                    Dimension = ResourceDimension.Buffer,
                    Width = (ulong)(fr.VertexBufferSize * SizeOf<ImDrawVert>()),
                    Height = 1,
                    DepthOrArraySize = 1,
                    MipLevels = 1,
                    Format = Format.Unknown,
                    SampleDescription = new()
                    {
                        Count = 1,
                        Quality = 0,
                    },
                    Layout = TextureLayout.RowMajor,
                    Flags = ResourceFlags.None
                };
                fr.VertexBuffer = bd.pd3dDevice.CreateCommittedResource(props, HeapFlags.None, desc, ResourceStates.GenericRead, null);
                fr.VertexBuffer.Name = "ImGui Vertex Buffer";
            }
            if (fr.IndexBuffer == null || fr.IndexBufferSize < draw_data.TotalIdxCount)
            {
                fr.IndexBuffer?.Dispose();
                fr.IndexBufferSize = draw_data.TotalIdxCount + 10000;
                HeapProperties props = new()
                {
                    Type = HeapType.Upload,
                    CPUPageProperty = CpuPageProperty.Unknown,
                    MemoryPoolPreference = MemoryPool.Unknown
                };
                ResourceDescription desc = new()
                {
                    Dimension = ResourceDimension.Buffer,
                    Width = (ulong)(fr.IndexBufferSize * SizeOf<ushort>()), // ImDrawIdx = ushort?
                    Height = 1,
                    DepthOrArraySize = 1,
                    MipLevels = 1,
                    Format = Format.Unknown,
                    SampleDescription = new()
                    {
                        Count = 1,
                        Quality = 0,
                    },
                    Layout = TextureLayout.RowMajor,
                    Flags = ResourceFlags.None,
                };
                fr.IndexBuffer = bd.pd3dDevice.CreateCommittedResource(props, HeapFlags.None, desc, ResourceStates.GenericRead, null);
                fr.IndexBuffer.Name = "ImGui Index Buffer";
            }

            // Upload vertex/index data into a single contiguous GPU buffer
            unsafe
            {
                void* vtx_resource;
                void* idx_resource;

                Vortice.Direct3D12.Range range = new()
                {
                    Begin = 0,
                    End = 0,
                };
                //memset(&range, 0, sizeof(D3D12_RANGE)); // TODO ???
                if (!fr.VertexBuffer.Map(0, range, &vtx_resource).Success)
                    return;
                if (!fr.IndexBuffer.Map(0, range, &idx_resource).Success)
                    return;
                ImDrawVert* vtx_dst = (ImDrawVert*)vtx_resource;
                //ImDrawIdx* idx_dst = (ImDrawIdx*)idx_resource;
                ushort* idx_dst = (ushort*)idx_resource;
                for (int n = 0; n < draw_data.CmdListsCount; n++)
                {
                    ImDrawList* cmd_list = ((ImDrawList**)draw_data.CmdLists)[n];
                    //memcpy(vtx_dst, cmd_list->VtxBuffer.Data, cmd_list->VtxBuffer.Size * SizeOf<ImDrawVert>());
                    Buffer.MemoryCopy((void*)cmd_list->VtxBuffer.Data, vtx_dst, cmd_list->VtxBuffer.Size * SizeOf<ImDrawVert>(), cmd_list->VtxBuffer.Size * SizeOf<ImDrawVert>());
                    //memcpy(idx_dst, cmd_list->IdxBuffer.Data, cmd_list->IdxBuffer.Size * sizeof(ImDrawIdx));
                    Buffer.MemoryCopy((void*)cmd_list->IdxBuffer.Data, idx_dst, cmd_list->IdxBuffer.Size * SizeOf<ushort>(), cmd_list->IdxBuffer.Size * SizeOf<ushort>());
                    vtx_dst += cmd_list->VtxBuffer.Size;
                    idx_dst += cmd_list->IdxBuffer.Size;
                }
                fr.VertexBuffer.Unmap(0, range);
                fr.IndexBuffer.Unmap(0, range);
            }

            // Setup desired DX state
            ImGui_ImplDX12_SetupRenderState(draw_data, ctx, fr);

            // Render command lists
            // (Because we merged all buffers into a single one, we maintain our own offset into them)
            int global_vtx_offset = 0;
            int global_idx_offset = 0;
            Vector2 clip_off = draw_data.DisplayPos;
            for (int n = 0; n < draw_data.CmdListsCount; n++)
            {
                unsafe
                {
                    ImDrawList* cmd_list = ((ImDrawList**)draw_data.CmdLists)[n];
                    for (int cmd_i = 0; cmd_i < cmd_list->CmdBuffer.Size; cmd_i++)
                    {
                        ImDrawCmd cmd = cmd_list->CmdBuffer.Ref<ImDrawCmd>(cmd_i);
                        if (cmd.UserCallback != IntPtr.Zero)
                        {
                            throw new NotImplementedException("Nope");
                        }
                        else
                        {
                            // Project scissor/clipping rectangles into framebuffer space
                            Vector2 clip_min = new(cmd.ClipRect.X - clip_off.X, cmd.ClipRect.Y - clip_off.Y);
                            Vector2 clip_max = new(cmd.ClipRect.Z - clip_off.X, cmd.ClipRect.W - clip_off.Y);
                            if (clip_max.X <= clip_min.X || clip_max.Y <= clip_min.Y)
                                continue;

                            // Apply Scissor/clipping rectangle, Bind texture, Draw
                            Vortice.RawRect r = new((int)clip_min.X, (int)clip_min.Y, (int)clip_max.X, (int)clip_max.Y);
                            GpuDescriptorHandle texture_handle = new()
                            {
                                Ptr = (ulong)cmd.TextureId
                            };
                            ctx.SetGraphicsRootDescriptorTable(1, texture_handle);
                            ctx.RSSetScissorRect(r);
                            ctx.DrawIndexedInstanced((int)cmd.ElemCount, 1, (int)(cmd.IdxOffset + global_idx_offset), (int)(cmd.VtxOffset + global_vtx_offset), 0);
                        }
                    }
                    global_idx_offset += cmd_list->IdxBuffer.Size;
                    global_vtx_offset += cmd_list->VtxBuffer.Size;
                }
            }
        }

        /* static */
        public void ImGui_ImplDX12_CreateFontsTexture()
        {
            unsafe
            {
                // Build texture atlas
                ImGuiIOPtr io = ImGui.GetIO();
                //ImGui_ImplDX12_Data bd = ImGui_ImplDX12_GetBackendData().Value;
                int width = -1;
                int height = -1;
                byte* pixels;
                io.Fonts.GetTexDataAsRGBA32(out pixels, out width, out height);

                // Upload texture to graphics system
                {
                    HeapProperties props = new()
                    {

                        Type = HeapType.Default,
                        CPUPageProperty = CpuPageProperty.Unknown,
                        MemoryPoolPreference = MemoryPool.Unknown
                    };

                    ResourceDescription desc = new()
                    {
                        Dimension = ResourceDimension.Texture2D,
                        Alignment = 0,
                        Width = (ulong)width,
                        Height = height,
                        DepthOrArraySize = 1,
                        MipLevels = 1,
                        Format = Format.R8G8B8A8_UNorm,
                        SampleDescription = new()
                        {
                            Count = 1,
                            Quality = 0,
                        },
                        Layout = TextureLayout.Unknown,
                        Flags = ResourceFlags.None,
                    };

                    ID3D12Resource pTexture = bd.pd3dDevice.CreateCommittedResource(props, HeapFlags.None, desc, ResourceStates.CopyDest, null);
                    pTexture.Name = "ImGui Font Texture";

                    ulong uploadPitch = (ulong)((width * 4 + D3D12.TextureDataPitchAlignment - 1u) & ~(D3D12.TextureDataPitchAlignment - 1u));
                    ulong uploadSize = (ulong)height * uploadPitch;
                    desc.Dimension = ResourceDimension.Buffer;
                    desc.Alignment = 0;
                    desc.Width = uploadSize;
                    desc.Height = 1;
                    desc.DepthOrArraySize = 1;
                    desc.MipLevels = 1;
                    desc.Format = Format.Unknown;
                    desc.SampleDescription.Count = 1;
                    desc.SampleDescription.Quality = 0;
                    desc.Layout = TextureLayout.RowMajor;
                    desc.Flags = ResourceFlags.None;

                    props.Type = HeapType.Upload;
                    props.CPUPageProperty = CpuPageProperty.Unknown;
                    props.MemoryPoolPreference = MemoryPool.Unknown;

                    //ID3D12Resource* uploadBuffer = nullptr;
                    ID3D12Resource uploadBuffer = bd.pd3dDevice.CreateCommittedResource(props, HeapFlags.None, desc, ResourceStates.GenericRead, null);
                    uploadBuffer.Name = "ImGui Font Upload Buffer";

                    byte* mapped = null; // Was uintptr_t, is byte* interchangeable?
                    Range range = new() { Begin = 0, End = new SharpGen.Runtime.PointerSize((long)uploadSize) };
                    uploadBuffer.Map(0, range, &mapped); // TODO: Assert success
                    for (uint y = 0; y < height; y++)
                        Buffer.MemoryCopy(pixels + y * width * 4, mapped + y * uploadPitch, width * 4, width * 4);
                    //memcpy((void*)((uintptr_t)mapped + y * uploadPitch), , width * 4);
                    uploadBuffer.Unmap(0, range);

                    TextureCopyLocation srcLocation = new(uploadBuffer, new PlacedSubresourceFootPrint()
                    {
                        Offset = 0,
                        Footprint = new()
                        {
                            Format = Format.R8G8B8A8_UNorm,
                            Width = width,
                            Height = height,
                            Depth = 1,
                            RowPitch = (int)uploadPitch,
                        }
                    });
                    TextureCopyLocation dstLocation = new(pTexture, 0);

                    ResourceBarrier barrier = ResourceBarrier.BarrierTransition(pTexture, ResourceStates.CopyDest, ResourceStates.PixelShaderResource, -1); // -1 = ALL_SUBRESOURCES?

                    ID3D12Fence fence = bd.pd3dDevice.CreateFence(0, FenceFlags.None);
                    fence.Name = "ImGui Fence";

                    // Can't be named `event`
                    AutoResetEvent ev = new(false);

                    CommandQueueDescription queueDesc = new()
                    {
                        Type = CommandListType.Direct,
                        Flags = CommandQueueFlags.None,
                        NodeMask = 1,
                    };

                    ID3D12CommandQueue cmdQueue = bd.pd3dDevice.CreateCommandQueue(queueDesc);
                    cmdQueue.Name = "ImGui Command Queue";
                    //IM_ASSERT(SUCCEEDED(hr));

                    ID3D12CommandAllocator cmdAlloc = bd.pd3dDevice.CreateCommandAllocator(CommandListType.Direct);
                    cmdAlloc.Name = "ImGui Command Allocator";
                    //IM_ASSERT(SUCCEEDED(hr));

                    ID3D12GraphicsCommandList cmdList = bd.pd3dDevice.CreateCommandList<ID3D12GraphicsCommandList>(CommandListType.Direct, cmdAlloc);
                    cmdList.Name = "ImGui Command List";
                    //IM_ASSERT(SUCCEEDED(hr));

                    cmdList.CopyTextureRegion(dstLocation, 0, 0, 0, srcLocation, null);
                    cmdList.ResourceBarrier(barrier);

                    cmdList.Close();
                    //IM_ASSERT(SUCCEEDED(hr));

                    cmdQueue.ExecuteCommandList(cmdList);
                    cmdQueue.Signal(fence, 1);
                    //IM_ASSERT(SUCCEEDED(hr));

                    fence.SetEventOnCompletion(1, ev);
                    ev.WaitOne(Timeout.Infinite);

                    cmdList.Dispose();
                    cmdAlloc.Dispose();
                    cmdQueue.Dispose();
                    ev.Close();
                    //CloseHandle(event);
                    fence.Dispose();
                    uploadBuffer.Dispose();

                    // Create texture view
                    ShaderResourceViewDescription srvDesc = new()
                    {
                        Format = Format.R8G8B8A8_UNorm,
                        ViewDimension = Vortice.Direct3D12.ShaderResourceViewDimension.Texture2D,
                        Texture2D = new()
                        {
                            MipLevels = desc.MipLevels,
                            MostDetailedMip = 0,
                        },
                        Shader4ComponentMapping = ShaderComponentMapping.Default,
                    };
                    bd.pd3dDevice.CreateShaderResourceView(pTexture, srvDesc, bd.hFontSrvCpuDescHandle);
                    bd.pFontTextureResource?.Dispose();
                    bd.pFontTextureResource = pTexture;
                }

                // Store our identifier
                // READ THIS IF THE STATIC_ASSERT() TRIGGERS:
                // - Important: to compile on 32-bit systems, this backend requires code to be compiled with '#define ImTextureID ImU64'.
                // - This is because we need ImTextureID to carry a 64-bit value and by default ImTextureID is defined as void*.
                // [Solution 1] IDE/msbuild: in "Properties/C++/Preprocessor Definitions" add 'ImTextureID=ImU64' (this is what we do in the 'example_win32_direct12/example_win32_direct12.vcxproj' project file)
                // [Solution 2] IDE/msbuild: in "Properties/C++/Preprocessor Definitions" add 'IMGUI_USER_CONFIG="my_imgui_config.h"' and inside 'my_imgui_config.h' add '#define ImTextureID ImU64' and as many other options as you like.
                // [Solution 3] IDE/msbuild: edit imconfig.h and add '#define ImTextureID ImU64' (prefer solution 2 to create your own config file!)
                // [Solution 4] command-line: add '/D ImTextureID=ImU64' to your cl.exe command-line (this is what we do in the example_win32_direct12/build_win32.bat file)
                // TODO: Verify below
                //static_assert(sizeof(ImTextureID) >= sizeof(bd->hFontSrvGpuDescHandle.ptr), "Can't pack descriptor handle into TexID, 32-bit not supported yet.");
                Debug.Assert(SizeOf<IntPtr>() >= SizeOf(bd.hFontSrvGpuDescHandle.Ptr));
                // TODO: Solve this somehow
                io.Fonts.SetTexID((IntPtr)bd.hFontSrvGpuDescHandle.Ptr);
            }
        }

        public bool ImGui_ImplDX12_CreateDeviceObjects()
        {
            //ImGui_ImplDX12_Data? bdOpt = ImGui_ImplDX12_GetBackendData();
            // TODO
            //if (bd == null || bd.pd3dDevice == null)
            //return false;
            if (bd.pPipelineState != null)
                ImGui_ImplDX12_InvalidateDeviceObjects();

            // Create the root signature
            {
                RootParameter[] param = new RootParameter[]{
                    new RootParameter(new RootConstants(0, 0, 16), ShaderVisibility.Vertex),
                    new RootParameter(new RootDescriptorTable(new DescriptorRange[] {
                        new DescriptorRange(DescriptorRangeType.ShaderResourceView, 1, 0, 0, 0)
                    }), ShaderVisibility.Pixel)
                };

                // Bilinear sampling is required by default. Set 'io.Fonts.Flags |= ImFontAtlasFlags_NoBakedLines' or 'style.AntiAliasedLinesUseTex = false' to allow point/nearest sampling.
                StaticSamplerDescription staticSampler = new()
                {
                    Filter = Filter.MinMagMipLinear,
                    AddressU = TextureAddressMode.Wrap,
                    AddressV = TextureAddressMode.Wrap,
                    AddressW = TextureAddressMode.Wrap,
                    MipLODBias = 0,
                    MaxAnisotropy = 0,
                    ComparisonFunction = ComparisonFunction.Always,
                    BorderColor = StaticBorderColor.TransparentBlack,
                    MinLOD = 0,
                    MaxLOD = 0,
                    ShaderRegister = 0,
                    RegisterSpace = 0,
                    ShaderVisibility = ShaderVisibility.Pixel,
                };

                VersionedRootSignatureDescription desc = new(new RootSignatureDescription()
                {
                    Parameters = param,
                    StaticSamplers = new[] { staticSampler },
                    Flags =
                    RootSignatureFlags.AllowInputAssemblerInputLayout |
                    RootSignatureFlags.DenyHullShaderRootAccess |
                    RootSignatureFlags.DenyDomainShaderRootAccess |
                    RootSignatureFlags.DenyGeometryShaderRootAccess,
                });

                // Load d3d12.dll and D3D12SerializeRootSignature() function address dynamically to facilitate using with D3D12On7.
                // See if any version of d3d12.dll is already loaded in the process. If so, give preference to that.
                /*static HINSTANCE d3d12_dll = ::GetModuleHandleA("d3d12.dll");
                if (d3d12_dll == nullptr)
                {
                    // Attempt to load d3d12.dll from local directories. This will only succeed if
                    // (1) the current OS is Windows 7, and
                    // (2) there exists a version of d3d12.dll for Windows 7 (D3D12On7) in one of the following directories.
                    // See https://github.com/ocornut/imgui/pull/3696 for details.
                    const char* localD3d12Paths[] = { ".\\d3d12.dll", ".\\d3d12on7\\d3d12.dll", ".\\12on7\\d3d12.dll" }; // A. current directory, B. used by some games, C. used in Microsoft D3D12On7 sample
                    for (int i = 0; i < IM_ARRAYSIZE(localD3d12Paths); i++)
                        if ((d3d12_dll = ::LoadLibraryA(localD3d12Paths[i])) != nullptr)
                            break;

                    // If failed, we are on Windows >= 10.
                    if (d3d12_dll == nullptr)
                        d3d12_dll = ::LoadLibraryA("d3d12.dll");

                    if (d3d12_dll == nullptr)
                        return false;
                }

                PFN_D3D12_SERIALIZE_ROOT_SIGNATURE D3D12SerializeRootSignatureFn = (PFN_D3D12_SERIALIZE_ROOT_SIGNATURE)::GetProcAddress(d3d12_dll, "D3D12SerializeRootSignature");
                if (D3D12SerializeRootSignatureFn == nullptr)
                    return false;*/

                // TODO: String???
                D3D12.D3D12SerializeVersionedRootSignature(desc, out Blob blob);
                //if (D3D12SerializeRootSignatureFn(&desc, D3D_ROOT_SIGNATURE_VERSION_1, &blob, nullptr) != S_OK)
                //return false;

                bd.pRootSignature = bd.pd3dDevice.CreateRootSignature<ID3D12RootSignature>(0, blob);
                bd.pRootSignature.Name = "ImGui Root Signature";
                blob.Dispose();
            }

            // By using D3DCompile() from <d3dcompiler.h> / d3dcompiler.lib, we introduce a dependency to a given version of d3dcompiler_XX.dll (see D3DCOMPILER_DLL_A)
            // If you would like to use this DX12 sample code but remove this dependency you can:
            //  1) compile once, save the compiled shader blobs into a file or source code and assign them to psoDesc.VS/PS [preferred solution]
            //  2) use code to detect any version of the DLL and grab a pointer to D3DCompile from the DLL.
            // See https://github.com/ocornut/imgui/pull/638 for sources and details.


            //Blob vertexShaderBlob;
            // Blob pixelShaderBlob;

            // Create the vertex shader
            string vertexShader =
@"cbuffer vertexBuffer : register(b0)
{
    float4x4 ProjectionMatrix; 
};
struct VS_INPUT
{
    float2 pos : POSITION;
    float4 col : COLOR0;
    float2 uv  : TEXCOORD0;
};

struct PS_INPUT
{
    float4 pos : SV_POSITION;
    float4 col : COLOR0;
    float2 uv  : TEXCOORD0;
};

PS_INPUT main(VS_INPUT input)
{
    PS_INPUT output;
    output.pos = mul(ProjectionMatrix, float4(input.pos.xy, 0.f, 1.f));
    output.col = input.col;
    output.uv = input.uv;
    return output;
}
";

            var vertexShaderBlob = Vortice.D3DCompiler.Compiler.Compile(vertexShader, "main", "", "vs_5_0");


            //if (FAILED(D3DCompile(vertexShader, strlen(vertexShader), nullptr, nullptr, nullptr, "main", "vs_5_0", 0, 0, &vertexShaderBlob, nullptr)))
            //return false; // NB: Pass ID3DBlob* pErrorBlob to D3DCompile() to get error showing in (const char*)pErrorBlob.GetBufferPointer(). Make sure to Release() the blob!

            // Create the pixel shader
            string pixelShader =
@"struct PS_INPUT
{
    float4 pos : SV_POSITION;
    float4 col : COLOR0;
    float2 uv  : TEXCOORD0;
};
SamplerState sampler0 : register(s0);
Texture2D texture0 : register(t0);

float4 main(PS_INPUT input) : SV_Target
{
    float4 out_col = input.col * texture0.Sample(sampler0, input.uv); 
    return out_col; 
}";

            var pixelShaderBlob = Vortice.D3DCompiler.Compiler.Compile(pixelShader, "main", "", "ps_5_0");

            // if (FAILED(D3DCompile(pixelShader, strlen(pixelShader), nullptr, nullptr, nullptr, "main", "ps_5_0", 0, 0, &pixelShaderBlob, nullptr)))
            // {
            //     vertexShaderBlob.Release();
            //     return false; // NB: Pass ID3DBlob* pErrorBlob to D3DCompile() to get error showing in (const char*)pErrorBlob.GetBufferPointer(). Make sure to Release() the blob!
            // }

            // Create the blending setup
            var blendDesc = new BlendDescription()
            {
                AlphaToCoverageEnable = false,
            };
            blendDesc.RenderTarget[0] = new()
            {
                BlendEnable = true,
                SourceBlend = Blend.SourceAlpha,
                DestinationBlend = Blend.InverseSourceAlpha,
                BlendOperation = BlendOperation.Add,
                SourceBlendAlpha = Blend.One,
                DestinationBlendAlpha = Blend.InverseSourceAlpha,
                BlendOperationAlpha = BlendOperation.Add,
                RenderTargetWriteMask = ColorWriteEnable.All,
            };

            // Create the rasterizer state
            RasterizerDescription rasterizerDesc = new()
            {
                FillMode = FillMode.Solid,
                CullMode = CullMode.None,
                FrontCounterClockwise = false,
                DepthBias = D3D12.DefaultDepthBias,
                DepthBiasClamp = D3D12.DefaultDepthBiasClamp,
                SlopeScaledDepthBias = D3D12.DefaultSlopeScaledDepthBias,
                DepthClipEnable = true,
                MultisampleEnable = false,
                AntialiasedLineEnable = false,
                ForcedSampleCount = 0,
                ConservativeRaster = ConservativeRasterizationMode.Off,
            };

            // Create depth-stencil State
            DepthStencilDescription depthStencilDesc = new()
            {
                DepthEnable = false,
                DepthWriteMask = DepthWriteMask.All,
                DepthFunc = ComparisonFunction.Always,
                StencilEnable = false,
                FrontFace = new()
                {
                    StencilFailOp = StencilOperation.Keep,
                    StencilDepthFailOp = StencilOperation.Keep,
                    StencilPassOp = StencilOperation.Keep,
                    StencilFunc = ComparisonFunction.Always,
                },
                BackFace = new()
                {
                    StencilFailOp = StencilOperation.Keep,
                    StencilDepthFailOp = StencilOperation.Keep,
                    StencilPassOp = StencilOperation.Keep,
                    StencilFunc = ComparisonFunction.Always,
                },
            };

            GraphicsPipelineStateDescription psoDesc = new()
            {
                NodeMask = 1,
                PrimitiveTopologyType = PrimitiveTopologyType.Triangle,
                RootSignature = bd.pRootSignature,
                SampleMask = uint.MaxValue,
                RenderTargetFormats = new[] {
                    bd.RTVFormat
                },
                SampleDescription = new()
                {
                    Count = 1,
                    Quality = 0
                },
                Flags = PipelineStateFlags.None,
                VertexShader = vertexShaderBlob,
                PixelShader = pixelShaderBlob,
                InputLayout = new[]
                {
                    new InputElementDescription("POSITION", 0, Format.R32G32_Float, (int)OffsetOf<ImDrawVert>("pos"), 0, InputClassification.PerVertexData, 0 ),
                    new InputElementDescription( "TEXCOORD", 0, Format.R32G32_Float,   (int)OffsetOf<ImDrawVert>("uv"), 0,  InputClassification.PerVertexData, 0 ),
                    new InputElementDescription( "COLOR",    0, Format.R8G8B8A8_UNorm, (int)OffsetOf<ImDrawVert>("col"), 0, InputClassification.PerVertexData, 0 )
                },
                BlendState = blendDesc,
                RasterizerState = rasterizerDesc,
                DepthStencilState = depthStencilDesc,
            };

            bd.pPipelineState = bd.pd3dDevice.CreateGraphicsPipelineState(psoDesc);
            bd.pPipelineState.Name = "ImGui Pipeline State";
            //vertexShaderBlob.Release();
            //pixelShaderBlob.Release();
            //if (result_pipeline_state != S_OK)
            //return false;

            ImGui_ImplDX12_CreateFontsTexture();

            return true;
        }

        public void ImGui_ImplDX12_InvalidateDeviceObjects()
        {
            //ImGui_ImplDX12_Data? bdOpt = ImGui_ImplDX12_GetBackendData();

            // TODO
            //if (bdOpt == null || !bdOpt.HasValue || bdOpt.Value.pd3dDevice == null)
            //return;

            ImGuiIOPtr io = ImGui.GetIO();
            bd.pRootSignature?.Dispose();
            bd.pPipelineState?.Dispose();
            bd.pFontTextureResource?.Dispose();
            io.Fonts.SetTexID(IntPtr.Zero); // We copied bd.pFontTextureView to io.Fonts.TexID so let's clear that as well.

            for (uint i = 0; i < bd.numFramesInFlight; i++)
            {
                ImGui_ImplDX12_RenderBuffers fr = bd.FrameResources[i];
                fr.IndexBuffer?.Dispose();
                fr.VertexBuffer?.Dispose();
            }
        }

        public bool ImGui_ImplDX12_Init(ID3D12Device device, int num_frames_in_flight, Format rtv_format, CpuDescriptorHandle font_srv_cpu_desc_handle, GpuDescriptorHandle font_srv_gpu_desc_handle)
        {
            ImGuiIOPtr io = ImGui.GetIO();
            //Debug.Assert(io.BackendRendererUserData == null);
            //IM_ASSERT(io.BackendRendererUserData == nullptr && "Already initialized a renderer backend!");

            // Setup backend capabilities flags
            ImGui_ImplDX12_Data bd = new()
            {
                pd3dDevice = device,
                RTVFormat = rtv_format,
                hFontSrvCpuDescHandle = font_srv_cpu_desc_handle,
                hFontSrvGpuDescHandle = font_srv_gpu_desc_handle,
                FrameResources = new ImGui_ImplDX12_RenderBuffers[num_frames_in_flight],
                numFramesInFlight = (uint)num_frames_in_flight,
                frameIndex = uint.MaxValue,
            };
            //io.BackendRendererUserData = bd;
            //io.BackendRendererName = new NullTerminatedString("imgui_impl_dx12\0");
            io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;  // We can honor the ImDrawCmd::VtxOffset field, allowing for large meshes.


            // Create buffers with a default size (they will later be grown as needed)
            for (int i = 0; i < num_frames_in_flight; i++)
            {
                bd.FrameResources[i] = new ImGui_ImplDX12_RenderBuffers
                {
                    IndexBuffer = null,
                    VertexBuffer = null,
                    IndexBufferSize = 10000,
                    VertexBufferSize = 5000,
                };
            }

            this.bd = bd;

            return true;
        }

        public void ImGui_ImplDX12_Shutdown()
        {
            //ImGui_ImplDX12_Data? bd = ImGui_ImplDX12_GetBackendData();
            //Debug.Assert(bd != null);
            //IM_ASSERT(bd != nullptr && "No renderer backend to shutdown, or already shutdown?");
            ImGuiIOPtr io = ImGui.GetIO();

            // Clean up windows and device objects
            ImGui_ImplDX12_InvalidateDeviceObjects();
            //delete[] bd->pFrameResources;
            //io.BackendRendererName = null;
            //io.BackendRendererUserData = null;
            //io.BackendFlags &= ~ImGuiBackendFlags_RendererHasVtxOffset;
            //IM_DELETE(bd);
        }

        public void ImGui_ImplDX12_NewFrame()
        {
            //ImGui_ImplDX12_Data? bd = ImGui_ImplDX12_GetBackendData();
            //Debug.Assert(bd != null);
            //IM_ASSERT(bd != nullptr && "Did you call ImGui_ImplDX12_Init()?");

            if (bd.pPipelineState == null)
                ImGui_ImplDX12_CreateDeviceObjects();
        }

    }
}