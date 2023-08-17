namespace Application.Graphics;

// There's no magic here, it's just a collection of constants to help with
// development. The point is not to automagically convert these numbers and
// structs into HeapSegment, DescriptorRange, or anything else.

// It should, however, hopefully, reduce typos and facilitate changing array
// sizes, shader registers, and such

public static class HeapConfig
{
    private enum Type
    {
        Start = 0,

        CBV = 1, // camera config, per-frame variables
        Texture = 2,
        VertexBuffer = 3,
        Surfaces = 4,
        InstanceDatas = 5,
    };

    private static readonly int[] _segmentSizes = new int[] {
        0,    // start, just leave this as 0
        4,    // CBV
        1024, // Texture
        1024, // Vertex buffer
        1024, // Surface
        1, // InstanceDatas
    };

    public readonly struct Segments
    {
        public static readonly int cbvs = (int)Type.CBV - 1;
        public static readonly int textures = (int)Type.Texture - 1;
        public static readonly int vertexBuffers = (int)Type.VertexBuffer - 1;
        public static readonly int surfaces = (int)Type.Surfaces - 1;
        public static readonly int instanceDatas = (int)Type.InstanceDatas - 1;
    };

    public readonly struct ArraySize
    {
        public static readonly int cbvs = _segmentSizes[(int)Type.CBV];
        public static readonly int textures = _segmentSizes[(int)Type.Texture];
        public static readonly int vertexBuffers = _segmentSizes[(int)Type.VertexBuffer];
        public static readonly int surfaces = _segmentSizes[(int)Type.Surfaces];
        public static readonly int instanceDatas = _segmentSizes[(int)Type.InstanceDatas];
        public static readonly int total = _segmentSizes.Sum();
    };

    public readonly struct DescriptorOffsetFromStart
    {
        // Sum all previous entries
        public static readonly int cbvs = _segmentSizes.Take((int)Type.CBV).Skip(1).Sum();
        public static readonly int textures = _segmentSizes.Take((int)Type.Texture).Skip(1).Sum();
        public static readonly int vertexBuffers = _segmentSizes.Take((int)Type.VertexBuffer).Skip(1).Sum();
        public static readonly int surfaces = _segmentSizes.Take((int)Type.Surfaces).Skip(1).Sum();
        public static readonly int instanceDatas = _segmentSizes.Take((int)Type.InstanceDatas).Skip(1).Sum();
    };

    public struct BaseRegister
    {
        public const int modelData = 0;
        public const int cbvs = 1;
        public const int textures = 0;
        public const int vertexBuffers = 0;
        public const int surfaces = 0;
        public const int staticSamplers = 0;
        public const int instanceDatas = 0;
    };

    public struct RegisterSpace
    {
        public const int modelData = 0;
        public const int cbvs = 0;
        public const int textures = 1;
        public const int vertexBuffers = 2;
        public const int surfaces = 3;
        public const int staticSamplers = 4;
        public const int instanceDatas = 5;
    };
}