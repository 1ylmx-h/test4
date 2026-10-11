using UnityEngine;

namespace QuaternionDemo
{
    /// <summary>
    /// 课程演示用的四元数数学实现。所有旋转结果都归一化为单位四元数。
    /// </summary>
    public static class QuaternionMath
    {
        public static Quaternion FromAxisAngle(Vector3 axis, float angleDegrees)
        {
            if (axis.sqrMagnitude < 0.000001f)
            {
                axis = Vector3.up;
            }

            axis.Normalize();
            float halfAngle = angleDegrees * Mathf.Deg2Rad * 0.5f;
            float sinHalfAngle = Mathf.Sin(halfAngle);

            return Normalize(new Quaternion(
                axis.x * sinHalfAngle,
                axis.y * sinHalfAngle,
                axis.z * sinHalfAngle,
                Mathf.Cos(halfAngle)));
        }

        /// <summary>
        /// Hamilton product。Unity 中 lhs * rhs 表示先执行 rhs，再执行 lhs。
        /// </summary>
        public static Quaternion Multiply(Quaternion lhs, Quaternion rhs)
        {
            return Normalize(new Quaternion(
                lhs.w * rhs.x + lhs.x * rhs.w + lhs.y * rhs.z - lhs.z * rhs.y,
                lhs.w * rhs.y - lhs.x * rhs.z + lhs.y * rhs.w + lhs.z * rhs.x,
                lhs.w * rhs.z + lhs.x * rhs.y - lhs.y * rhs.x + lhs.z * rhs.w,
                lhs.w * rhs.w - lhs.x * rhs.x - lhs.y * rhs.y - lhs.z * rhs.z));
        }

        /// <summary>
        /// q * p * q^-1 的展开形式。
        /// </summary>
        public static Vector3 RotateVector(Quaternion rotation, Vector3 vector)
        {
            rotation = Normalize(rotation);
            Vector3 u = new Vector3(rotation.x, rotation.y, rotation.z);
            float scalar = rotation.w;

            return 2f * Vector3.Dot(u, vector) * u
                   + (scalar * scalar - Vector3.Dot(u, u)) * vector
                   + 2f * scalar * Vector3.Cross(u, vector);
        }

        public static Quaternion Normalize(Quaternion quaternion)
        {
            float length = Magnitude(quaternion);
            if (length < 0.000001f)
            {
                return Quaternion.identity;
            }

            float inverseLength = 1f / length;
            return new Quaternion(
                quaternion.x * inverseLength,
                quaternion.y * inverseLength,
                quaternion.z * inverseLength,
                quaternion.w * inverseLength);
        }

        public static float Magnitude(Quaternion quaternion)
        {
            return Mathf.Sqrt(
                quaternion.x * quaternion.x
                + quaternion.y * quaternion.y
                + quaternion.z * quaternion.z
                + quaternion.w * quaternion.w);
        }

        /// <summary>
        /// q 与 -q 表示同一旋转，因此比较点积的绝对值。
        /// </summary>
        public static bool RepresentsSameRotation(
            Quaternion first,
            Quaternion second,
            float tolerance = 0.0001f)
        {
            first = Normalize(first);
            second = Normalize(second);
            return 1f - Mathf.Abs(Quaternion.Dot(first, second)) <= tolerance;
        }
    }
}
