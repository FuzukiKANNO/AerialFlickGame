using UnityEngine;

namespace AerialFlickGame.TrackedObjects
{
    /// <summary>
    /// トラッキング重心から一定距離オフセットした位置にある「円（リング）」で弾く追跡形状。
    /// 実物のアイテム: 重心の真下に直径12cmの円、円の端から重心まで(y)は1cm。
    /// → 円中心は重心から (端〜重心 + 半径) だけオフセット方向に離れる。
    /// XY 平面上では円 vs 円として解析的に扱う（CylinderTracked と同じ数式、中心だけオフセット）。
    /// </summary>
    public class RingTracked : TrackedObjectBase
    {
        [Header("形状（キャリブレ用）")]
        [Tooltip("円の直径 [m]")]
        public float Diameter = 0.12f;

        [Tooltip("円の端から重心までの距離 [m]（オフセット方向）")]
        public float EdgeToCentroid = 0.01f;

        [Tooltip("面から +法線(右/+X)側への厚み [m]。ラケットの実厚み（重心の右に1cmなら 0.01）。厚くするとすり抜けにくい")]
        public float Thickness = 0.01f;

        [Tooltip("重心から円中心への向き（ローカル）。真下=Down")]
        public Vector3 OffsetDirectionLocal = Vector3.down;

        [Tooltip("追跡姿勢でオフセット方向・平面を回す（アイテムを傾けると円も傾く）")]
        public bool UseTrackedRotation = true;

        [Tooltip("円が乗る平面の法線（ローカル）。X=(1,0,0) なら ZY 平面")]
        public Vector3 PlaneNormalLocal = Vector3.right;

        [Tooltip("予測（Predictive）時の数値ステップ [s]")]
        public float StepSize = 0.002f;

        /// <summary>円の半径 [m]。</summary>
        public float Radius => Diameter * 0.5f;

        /// <summary>重心から円中心までの距離 [m]（端〜重心 + 半径）。</summary>
        public float CenterOffsetDistance => EdgeToCentroid + Radius;

        /// <summary>円中心のワールド座標。</summary>
        public Vector3 CenterWorld
        {
            get
            {
                Vector3 dir = OffsetDirectionLocal.sqrMagnitude > 1e-9f
                    ? OffsetDirectionLocal.normalized
                    : Vector3.down;
                if (UseTrackedRotation) dir = Orientation * dir;
                return Position + dir * CenterOffsetDistance;
            }
        }

        /// <summary>円中心の XY。</summary>
        public Vector2 CenterXY => new Vector2(CenterWorld.x, CenterWorld.y);

        public override Vector2 ShapeCenterXY => CenterXY;

        /// <summary>円盤（面）のワールド法線。</summary>
        public Vector3 PlaneNormalWorld
        {
            get
            {
                Vector3 n = PlaneNormalLocal.sqrMagnitude > 1e-9f
                    ? PlaneNormalLocal.normalized : Vector3.right;
                return UseTrackedRotation ? (Orientation * n) : n;
            }
        }

        /// <summary>円が乗る平面のワールド基底ベクトル（u, v）。円上の点 = 中心 + (cosθ·u + sinθ·v)·半径。</summary>
        public void GetPlaneBasis(out Vector3 u, out Vector3 v)
        {
            Vector3 n = PlaneNormalLocal.sqrMagnitude > 1e-9f
                ? PlaneNormalLocal.normalized : Vector3.right;
            Vector3 helper = Mathf.Abs(Vector3.Dot(n, Vector3.up)) > 0.99f ? Vector3.right : Vector3.up;
            Vector3 uLocal = Vector3.Cross(helper, n).normalized;
            Vector3 vLocal = Vector3.Cross(n, uLocal);
            if (UseTrackedRotation)
            {
                u = Orientation * uLocal;
                v = Orientation * vLocal;
            }
            else
            {
                u = uLocal;
                v = vLocal;
            }
        }

        /// <summary>
        /// 点（飛来円の中心。z は結像面 PlaneZ）から、指定中心の円盤（面・厚み Thickness）までの距離。
        /// 厚みを持つ短い円柱として扱い、内側なら 0 を返す。
        /// </summary>
        private float DistancePointToDisc(Vector3 point, Vector3 discCenter)
        {
            Vector3 n = PlaneNormalWorld;
            Vector3 rel = point - discCenter;
            float dPerp = Vector3.Dot(rel, n);              // 面への垂直距離（符号付き）
            Vector3 inPlane = rel - dPerp * n;              // 面内成分
            float dIn = inPlane.magnitude;
            // 面(dPerp=0)から +法線側へ Thickness だけ伸びる片側の板 [0, Thickness] までの距離
            float t = Mathf.Max(0f, Thickness);
            float axial = Mathf.Max(0f, Mathf.Max(-dPerp, dPerp - t));
            float radial = Mathf.Max(0f, dIn - Radius);
            return Mathf.Sqrt(axial * axial + radial * radial);
        }

        public override float ComputeDistanceTo(Vector2 circleCenter)
        {
            // 飛来円は結像面(z=PlaneZ)上にある点として扱う
            Vector3 ball = new Vector3(circleCenter.x, circleCenter.y, PlaneZ);
            return DistancePointToDisc(ball, CenterWorld);
        }

        /// <summary>
        /// 常に「面の法線」で弾く（縁での斜め弾きはしない）。法線はボールがいる側へ向ける。
        /// </summary>
        public override Vector2 CollisionNormal(Vector2 ballXY)
        {
            Vector3 P = new Vector3(ballXY.x, ballXY.y, PlaneZ);
            Vector3 N = PlaneNormalWorld;
            float dPerp = Vector3.Dot(P - CenterWorld, N);
            Vector3 nWorld = dPerp >= 0f ? N : -N;   // 面法線をボール側へ
            Vector2 n = new Vector2(nWorld.x, nWorld.y);
            if (n.sqrMagnitude < 1e-10f) n = ballXY - CenterXY; // 面法線に XY 成分が無い場合の保険
            return n.sqrMagnitude < 1e-10f ? Vector2.left : n.normalized;
        }

        public override float? FindTimeToCollision(
            Vector2 circlePosNow,
            Vector2 circleVelocity,
            float collisionThreshold,
            float maxLookAheadSeconds)
        {
            // 円盤 vs 移動する点は解析が煩雑なので数値ステッピングで解く
            float step = Mathf.Max(1e-4f, StepSize);
            Vector3 c0 = CenterWorld;
            Vector3 vt = new Vector3(Velocity.x, Velocity.y, 0f);      // 円盤中心の速度（XY）
            Vector3 vc = new Vector3(circleVelocity.x, circleVelocity.y, 0f);
            Vector3 q0 = new Vector3(circlePosNow.x, circlePosNow.y, PlaneZ);

            for (float t = 0f; t <= maxLookAheadSeconds; t += step)
            {
                Vector3 ct = c0 + vt * t;
                Vector3 qt = q0 + vc * t;
                if (DistancePointToDisc(qt, ct) <= collisionThreshold)
                    return t;
            }
            return null;
        }
    }
}
