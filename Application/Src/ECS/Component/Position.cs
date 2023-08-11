namespace Application.ECS.Component
{
    internal struct Position
    {
        public float X;
        public float Y;
        public float Z;

        public Position(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}
