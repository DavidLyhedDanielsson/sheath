using System.Diagnostics;
using ConsoleApp1.Asset;
using Vortice.Direct3D12;

namespace ConsoleApp1.Graphics;

class UploadTask
{
    public enum Type { BUFFER, TEXTURE };

    public required ID3D12Resource TargetResource { get; init; }
    public required Type UploadType { get; init; }
}

class BufferUploadTask : UploadTask
{
    public required ulong TargetOffset { get; init; }
    public required byte[] Data { get; init; }

    unsafe public void Deconstruct(out ID3D12Resource targetResource, out ulong targetOffset, out byte[] data)
    {
        targetResource = TargetResource;
        targetOffset = TargetOffset;
        data = Data;
    }
}

class TextureUploadTask : UploadTask
{
    public required TextureData Texture { get; init; }

    unsafe public void Deconstruct(out ID3D12Resource targetResource, out TextureData texture)
    {
        targetResource = TargetResource;
        texture = Texture;
    }
}

class LinearUploader
{
    private readonly ID3D12Resource _uploadBuffer;
    private readonly ulong _uploadBufferSize;
    private ulong _currentOffset;
    private List<UploadTask> _uploadTasks;

    public LinearUploader(ID3D12Resource uploadBuffer, ulong uploadBufferSize)
    {
        _uploadBuffer = uploadBuffer;
        _uploadBufferSize = uploadBufferSize;
        _currentOffset = 0;
        _uploadTasks = new();
    }

    private unsafe ulong DoBufferUpload(ID3D12GraphicsCommandList commandList, ID3D12Resource targetResource, ulong targetOffset, void* data, ulong dataLength)
    {
        byte* destination;
        _uploadBuffer.Map(0, (void**)&destination);
        Buffer.MemoryCopy(data, destination + _currentOffset, _uploadBufferSize - _currentOffset, dataLength);
        _uploadBuffer.Unmap(0);

        commandList.CopyBufferRegion(targetResource, targetOffset, _uploadBuffer, _currentOffset, dataLength);

        return _currentOffset += dataLength;
    }

    private unsafe ulong DoTextureUpload(ID3D12GraphicsCommandList commandList, ID3D12Resource target, TextureData texture)
    {
        var offset = _currentOffset;
        var startOffset = offset;

        ulong alignment = D3D12.TextureDataPitchAlignment;
        ulong rowByteSize = ((ulong)texture.Width * 4 + alignment - 1) / alignment * alignment;

        {
            byte* destination;
            _uploadBuffer.Map(0, (void**)&destination);
            for (ulong i = 0; i < (ulong)texture.Height; ++i)
            {
                fixed (byte* textureCopyPtr = &texture.Texels[rowByteSize * i])
                {
                    Buffer.MemoryCopy(textureCopyPtr, destination + offset, _uploadBufferSize - offset, rowByteSize);

                    offset += rowByteSize;
                }
            }
            _uploadBuffer.Unmap(0);
        }

        {
            TextureCopyLocation destination = new(target, 0);
            TextureCopyLocation source = new(_uploadBuffer, new PlacedSubresourceFootPrint
            {
                Offset = startOffset,
                Footprint = new SubresourceFootPrint(Vortice.DXGI.Format.R8G8B8A8_UNorm, texture.Width, texture.Height, 1, checked((int)rowByteSize)),
            });

            commandList.CopyTextureRegion(destination, 0, 0, 0, source);
        }

        return offset;
    }

    public unsafe void QueueBufferUpload(ID3D12GraphicsCommandList commandList, ID3D12Resource targetResource, ulong targetOffset, void* data, ulong dataLength)
    {
        Debug.Assert(dataLength <= _uploadBufferSize);

        if (_currentOffset + dataLength > _uploadBufferSize)
        {
            byte[] dataCopy = new byte[dataLength];
            fixed (byte* dataCopyPtr = &dataCopy[0])
            {
                Buffer.MemoryCopy(data, dataCopyPtr, dataLength, dataLength);
                _uploadTasks.Add(new BufferUploadTask
                {
                    TargetResource = targetResource,
                    UploadType = UploadTask.Type.BUFFER,
                    TargetOffset = targetOffset,
                    Data = dataCopy,
                });
            }
            return;
        }

        _currentOffset = DoBufferUpload(commandList, targetResource, targetOffset, data, dataLength);
    }

    public unsafe void QueueTextureUpload(ID3D12GraphicsCommandList commandList, ID3D12Resource targetResource, TextureData texture)
    {
        ulong alignment = D3D12.TextureDataPitchAlignment;
        ulong rowByteSize = ((ulong)texture.Width * 4 + alignment - 1) / alignment * alignment;

        var dataLength = rowByteSize * (ulong)texture.Height;

        Debug.Assert(dataLength <= _uploadBufferSize);

        if (_currentOffset + dataLength > _uploadBufferSize)
        {
            _uploadTasks.Add(new TextureUploadTask
            {
                TargetResource = targetResource,
                UploadType = UploadTask.Type.TEXTURE,
                Texture = texture,
            });
            return;
        }

        _currentOffset = DoTextureUpload(commandList, targetResource, texture);
    }

    public void UploadsDone()
    {
        _currentOffset = 0;
    }

    public bool HasMoreUploads()
    {
        return _uploadTasks.Count > 0;
    }

    public unsafe void QueueRemainingUploads(ID3D12GraphicsCommandList commandList)
    {
        for (int taskI = 0; taskI < _uploadTasks.Count; ++taskI)
        {
            if (_uploadTasks[taskI] is BufferUploadTask bufferTask)
            {
                var (targetResource, targetOffset, data) = bufferTask;

                if (_currentOffset + (ulong)data.Length <= _uploadBufferSize)
                {
                    fixed (void* source = &data[0])
                    {
                        _currentOffset = DoBufferUpload(commandList, targetResource, targetOffset, source, (ulong)data.Length);
                    }
                }
                else
                {
                    Debug.Assert(taskI != 0);
                    _uploadTasks.RemoveRange(0, taskI);
                    return;
                }
            }
            else if (_uploadTasks[taskI] is TextureUploadTask textureTask)
            {
                var (targetResource, texture) = textureTask;

                ulong alignment = D3D12.TextureDataPitchAlignment;
                ulong rowByteSize = ((ulong)texture.Width * 4 + alignment - 1) / alignment * alignment;
                ulong totalByteSize = rowByteSize * (ulong)texture.Height;

                if (_currentOffset + totalByteSize <= _uploadBufferSize)
                {
                    _currentOffset = DoTextureUpload(commandList, targetResource, texture);
                }
                else
                {
                    Debug.Assert(taskI != 0);
                    _uploadTasks.RemoveRange(0, taskI);
                    return;
                }
            }
        }

        _uploadTasks.Clear();

        return;
    }
}
