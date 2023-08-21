using Quaternion = Silk.NET.Maths.Quaternion<float>;
using Matrix = Silk.NET.Maths.Matrix4X4<float>;
using Vector3 = Silk.NET.Maths.Vector3D<float>;

namespace Application
{
    public class Camera
    {
        private Quaternion _pitch = Quaternion.Identity;
        private Quaternion _yaw = Quaternion.Identity;
        public Quaternion _rotation = Quaternion.Identity;
        public float _viewDistance = 1.0f;
        public float _targetViewDistance = 1.0f;

        public void Pitch(float increase)
        {
            _pitch *= Quaternion.CreateFromYawPitchRoll(0.0f, increase, 0.0f);
        }

        public void SetPitch(float val)
        {
            _pitch = Quaternion.CreateFromYawPitchRoll(0.0f, val, 0.0f);
        }

        public void Yaw(float increase)
        {
            _yaw *= Quaternion.CreateFromYawPitchRoll(increase, 0.0f, 0.0f);
        }

        public void SetYaw(float val)
        {
            _yaw = Quaternion.CreateFromYawPitchRoll(val, 0.0f, 0.0f);
        }

        public void Update(float delta)
        {
            _rotation = Quaternion.Slerp(_rotation, _yaw * _pitch, delta);
            _viewDistance = _viewDistance * (1.0f - delta) + _targetViewDistance * delta;
        }

        public Matrix CreateViewMatrix()
        {
            return Matrix4X4.CreateFromQuaternion(_rotation);
        }
    }
}
