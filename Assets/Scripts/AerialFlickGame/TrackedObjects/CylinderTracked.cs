using UnityEngine;

namespace AerialFlickGame.TrackedObjects
{
    /// <summary>
    /// XY 平面上で「円」として扱う円柱。円 vs 円の衝突は解析的に解ける。
    /// </summary>
    public class CylinderTracked : TrackedObjectBase
    {
        [Header("形状")]
        [Tooltip("XY 断面の円半径 [m]")]
        public float CylinderRadius = 0.03f;

        public override float ComputeDistanceTo(Vector2 circleCenter)
        {
            return Vector2.Distance(PositionXY, circleCenter) - CylinderRadius;
        }

        public override float? FindTimeToCollision(
            Vector2 circlePosNow,
            Vector2 circleVelocity,
            float collisionThreshold,
            float maxLookAheadSeconds)
        {
            Vector2 p0 = PositionXY;            // 追跡物体の現在位置
            Vector2 vt = Velocity;              // 追跡物体の推定速度
            Vector2 q0 = circlePosNow;          // 円の現在位置
            Vector2 vc = circleVelocity;        // 円の速度

            Vector2 dP = p0 - q0;               // 相対位置
            Vector2 dV = vt - vc;               // 相対速度
            float R = CylinderRadius + collisionThreshold;

            // |dP + dV*t|^2 = R^2 を解く
            float a = Vector2.Dot(dV, dV);
            float b = 2f * Vector2.Dot(dP, dV);
            float c = Vector2.Dot(dP, dP) - R * R;

            // 相対速度がほぼゼロ → 距離は一定なので将来の「入る瞬間」は無い
            if (a < 1e-9f)
            {
                return null;
            }

            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0f)
            {
                return null; // 実数解なし → 衝突しない
            }

            float sqrtD = Mathf.Sqrt(discriminant);
            float t1 = (-b - sqrtD) / (2f * a);
            float t2 = (-b + sqrtD) / (2f * a);

            // 最小の正の解が「最初に距離 R に入る瞬間」
            float lower = Mathf.Min(t1, t2);
            float upper = Mathf.Max(t1, t2);

            if (lower > 0f && lower <= maxLookAheadSeconds) return lower;
            if (upper > 0f && upper <= maxLookAheadSeconds) return upper;
            return null;
        }
    }
}
