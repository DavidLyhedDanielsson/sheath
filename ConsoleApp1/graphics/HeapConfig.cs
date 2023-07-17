namespace ConsoleApp1.Graphics;

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

        CBV = 1,
        Texture = 2,
        VertexBuffer = 3,
        Surfaces = 4,
    };

    private static readonly int[] _sizes = new int[] {
        0,    // start, just leave this as 0
        4,    // CBV
        1024, // Texture
        1024, // Vertex buffer
        1024, // Surface
    };

    public readonly struct Segments
    {
        public static readonly int cbvs = (int)Type.CBV - 1;
        public static readonly int textures = (int)Type.Texture - 1;
        public static readonly int vertexBuffers = (int)Type.VertexBuffer - 1;
        public static readonly int surfaces = (int)Type.Surfaces - 1;
    };

    public readonly struct ArraySize
    {
        public static readonly int cbvs = _sizes[(int)Type.CBV];
        public static readonly int textures = _sizes[(int)Type.Texture];
        public static readonly int vertexBuffers = _sizes[(int)Type.VertexBuffer];
        public static readonly int surfaces = _sizes[(int)Type.Surfaces];
        public static readonly int total = _sizes.Sum();
    };

    public readonly struct DescriptorOffsetFromStart
    {
        // Sum all previous entries
        public static readonly int cbvs = _sizes.Take((int)Type.CBV).Skip(1).Sum();
        public static readonly int textures = _sizes.Take((int)Type.Texture).Skip(1).Sum();
        public static readonly int vertexBuffers = _sizes.Take((int)Type.VertexBuffer).Skip(1).Sum();
        public static readonly int surfaces = _sizes.Take((int)Type.Surfaces).Skip(1).Sum();
    };

    public struct BaseRegister
    {
        public const int perInstanceBuffer = 0;
        public const int cbvs = 1;
        public const int textures = 0;
        public const int vertexBuffers = 0;
        public const int surfaces = 0;
        public const int staticSamplers = 0;
    };

    public struct RegisterSpace
    {
        public const int perInstanceBuffer = 0;
        public const int cbvs = 0;
        public const int textures = 1;
        public const int vertexBuffers = 2;
        public const int surfaces = 3;
        public const int staticSamplers = 4;
    };
}