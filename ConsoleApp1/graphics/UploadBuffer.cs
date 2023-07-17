using System.Diagnostics;
using Vortice.Direct3D12;

namespace ConsoleApp1.Graphics;

class UploadTask
{
    public required ID3D12Resource TargetResource { get; init; }
    public required int TargetOffset { get; init; }
    public required byte[] Data { get; init; }

    unsafe public void Deconstruct(out ID3D12Resource targetResource, out int targetOffset, out byte[] data)
    {
        targetResource = TargetResource;
        targetOffset = TargetOffset;
        data = Data;
    }
}

class LinearUploader
{
    private readonly ID3D12Resource _uploadBuffer;
    private readonly int _uploadBufferSize;
    private int _currentOffset;
    private List<UploadTask> _uploadTasks;

    public LinearUploader(ID3D12Resource uploadBuffer, int uploadBufferSize)
    {
        _uploadBuffer = uploadBuffer;
        _uploadBufferSize = uploadBufferSize;
        _currentOffset = 0;
        _uploadTasks = new();
    }

    public unsafe void QueueUpload(ID3D12GraphicsCommandList commandList, ID3D12Resource targetResource, int targetOffset, void* data, int dataLength)
    {
        Debug.Assert(dataLength <= _uploadBufferSize);

        if (_currentOffset + dataLength > _uploadBufferSize)
        {
            byte[] dataCopy = new byte[dataLength];
            fixed (byte* dataCopyPtr = &dataCopy[0])
            {
                Buffer.MemoryCopy(data, dataCopyPtr, dataLength, dataLength);
                _uploadTasks.Add(new UploadTask
                {
                    TargetResource = targetResource,
                    TargetOffset = targetOffset,
                    Data = dataCopy,
                });
            }
            return;
        }

        byte* destination;
        _uploadBuffer.Map(0, (void**)&destination);
        Buffer.MemoryCopy(data, destination + _currentOffset, _uploadBufferSize - _currentOffset, dataLength);
        _uploadBuffer.Unmap(0);

        commandList.CopyBufferRegion(targetResource, (ulong)targetOffset, _uploadBuffer, (ulong)_currentOffset, (ulong)dataLength);

        _currentOffset += dataLength;
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
        for (int i = 0; i < _uploadTasks.Count; ++i)
        {
            var (targetResource, targetOffset, data) = _uploadTasks[i];

            if (_currentOffset + data.Length <= _uploadBufferSize)
            {
                void* destination;
                _uploadBuffer.Map(0, (void**)&destination);
                fixed (void* source = &data[0])
                {
                    Buffer.MemoryCopy(source, destination, _uploadBufferSize - _currentOffset, data.Length);
                }
                _uploadBuffer.Unmap(0);

                commandList.CopyBufferRegion(targetResource, (ulong)targetOffset, _uploadBuffer, (ulong)_currentOffset, (ulong)data.Length);

                _currentOffset += data.Length;
            }
            else
            {
                Debug.Assert(i != 0);
                _uploadTasks.RemoveRange(0, i);
                return;
            }
        }

        _uploadTasks.Clear();

        return;
    }
}
