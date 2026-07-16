using UnityEngine;

namespace AerialFlickGame.TrackedObjects
{
    /// <summary>
    /// XY 平面上で「軸平行な矩形」として扱う直方体。
    /// 矩形 vs 円の衝突予測は解析解が煩雑なため、数値ステッピングで解く。
    /// ※ 向き（pose.rotation）は考慮しない。必要なら後で拡張可能。
    /// </summary>
    public class BoxTracked : TrackedObjectBase
    {
        [Header("形状")]
        [Tooltip("X 方向の幅 [m]")]
        public float BoxWidth = 0.06f;

        [Tooltip("Y 方向の高さ [m]")]
        public float BoxHeight = 0.04f;

        [Header("数値解")]
        [Tooltip("時間ステップ [s]。小さいほど高精度・高負荷")]
        public float StepSize = 0.002f;

        public override float ComputeDistanceTo(Vector2 circleCenter)
        {
            return RectPointDistance(PositionXY, BoxWidth, BoxHeight, circleCenter);
        }

        public override float? FindTimeToCollision(
            Vector2 circlePosNow,
            Vector2 circleVelocity,
            float collisionThreshold,
            float maxLookAheadSeconds)
        {
            float step = Mathf.Max(1e-4f, StepSize);
            float R = collisionThreshold; // 円半径 + 余裕（矩形側の距離関数が矩形サイズを内包）
            Vector2 p0 = PositionXY;
            Vector2 vt = Velocity;
            Vector2 q0 = circlePosNow;
            Vector2 vc = circleVelocity;

            for (float t = 0f; t <= maxLookAheadSeconds; t += step)
            {
                Vector2 pt = p0 + vt * t;   // 矩形中心の将来位置
                Vector2 qt = q0 + vc * t;   // 円中心の将来位置
                float dist = RectPointDistance(pt, BoxWidth, BoxHeight, qt);
                if (dist <= R)
                {
                    return t; // 最初に閾値以内になった時刻
                }
            }
            return null;
        }

        /// <summary>軸平行矩形（中心 rectCenter, 幅 w, 高さ h）と点 point の距離。内部なら 0。</summary>
        public static float RectPointDistance(Vector2 rectCenter, float w, float h, Vector2 point)
        {
            Vector2 local = point - rectCenter;
            float hw = w * 0.5f;
            float hh = h * 0.5f;
            float dx = Mathf.Max(0f, Mathf.Abs(local.x) - hw);
            float dy = Mathf.Max(0f, Mathf.Abs(local.y) - hh);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
    }
}
