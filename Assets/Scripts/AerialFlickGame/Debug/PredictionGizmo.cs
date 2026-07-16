using UnityEngine;
using AerialFlickGame.Core;
using AerialFlickGame.TrackedObjects;
using AerialFlickGame.Circle;

namespace AerialFlickGame.DebugTools
{
    /// <summary>
    /// Scene ビューで衝突予測の状態を可視化するデバッグ用コンポーネント。
    /// </summary>
    public class PredictionGizmo : MonoBehaviour
    {
        [Header("参照")]
        public PredictiveHitDetector Detector;
        public TrackedObjectBase TrackedObject;

        [Header("表示")]
        public bool DrawTrackedObject = true;
        public bool DrawCircles = true;
        public bool DrawPredictions = true;
        [Range(0f, 0.5f)] public float TrajectorySeconds = 0.3f;

        private void Reset()
        {
            Detector = GetComponent<PredictiveHitDetector>();
            TrackedObject = GetComponent<TrackedObjectBase>();
        }

        private void OnDrawGizmos()
        {
            if (Detector == null) Detector = GetComponent<PredictiveHitDetector>();
            if (TrackedObject == null && Detector != null) TrackedObject = Detector.TrackedObject;
            if (TrackedObject == null) TrackedObject = GetComponent<TrackedObjectBase>();

            float planeZ = TrackedObject != null ? TrackedObject.PlaneZ : 0f;

            // --- 追跡物体 ---
            if (DrawTrackedObject && TrackedObject != null)
            {
                Vector3 p = new Vector3(TrackedObject.PositionXY.x, TrackedObject.PositionXY.y, planeZ);
                Gizmos.color = Color.cyan;

                if (TrackedObject is CylinderTracked cyl)
                {
                    DrawWireCircle(p, cyl.CylinderRadius, planeZ);

                    // collisionMargin 分の有効ゾーン
                    if (Detector != null)
                    {
                        Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
                        DrawWireCircle(p, cyl.CylinderRadius + Detector.CollisionMargin, planeZ);
                    }
                }
                else if (TrackedObject is BoxTracked box)
                {
                    Gizmos.DrawWireCube(p, new Vector3(box.BoxWidth, box.BoxHeight, 0f));
                    if (Detector != null)
                    {
                        Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
                        Gizmos.DrawWireCube(p, new Vector3(
                            box.BoxWidth + Detector.CollisionMargin * 2f,
                            box.BoxHeight + Detector.CollisionMargin * 2f, 0f));
                    }
                }

                // 予測軌跡（速度方向の矢印 + 0.1/0.2/0.3s の点）
                Gizmos.color = Color.green;
                Vector2 v = TrackedObject.Velocity;
                Vector3 tip = p + new Vector3(v.x, v.y, 0f) * TrajectorySeconds;
                Gizmos.DrawLine(p, tip);
                for (float t = 0.1f; t <= TrajectorySeconds + 1e-3f; t += 0.1f)
                {
                    Vector3 pt = p + new Vector3(v.x, v.y, 0f) * t;
                    Gizmos.DrawSphere(pt, 0.003f);
                }

                // 奥行き(Z)の許容範囲（緑の半透明ボックス）
                if (Detector != null && Detector.UseDepthGate)
                {
                    float zLo = Detector.ImagePlaneZ - Detector.ZToleranceBack;
                    float zHi = Detector.ImagePlaneZ + Detector.ZToleranceFront;
                    Vector3 zCenter = new Vector3(TrackedObject.PositionXY.x, TrackedObject.PositionXY.y, (zLo + zHi) * 0.5f);
                    Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
                    Gizmos.DrawCube(zCenter, new Vector3(0.03f, 0.03f, zHi - zLo));
                }

                // 高さ(Y)の許容範囲（黄の水平線）
                if (Detector != null && Detector.UseHeightGate)
                {
                    Gizmos.color = new Color(1f, 0.9f, 0f, 0.7f);
                    float ht = Detector.HeightTolerance;
                    float half = 0.05f;
                    Gizmos.DrawLine(new Vector3(p.x - half, p.y + ht, planeZ), new Vector3(p.x + half, p.y + ht, planeZ));
                    Gizmos.DrawLine(new Vector3(p.x - half, p.y - ht, planeZ), new Vector3(p.x + half, p.y - ht, planeZ));
                }
            }

            // --- 飛来円と予測衝突点 ---
            if (Application.isPlaying)
            {
                foreach (FlyingCircle circle in FlyingCircle.Active)
                {
                    if (circle == null) continue;
                    Vector3 q = circle.transform.position;

                    if (DrawCircles)
                    {
                        Gizmos.color = Color.yellow;
                        DrawWireCircle(q, circle.Radius, q.z);
                        Vector3 qTip = q + Vector3.right * circle.Speed * TrajectorySeconds;
                        Gizmos.DrawLine(q, qTip);
                    }

                    if (DrawPredictions && TrackedObject != null)
                    {
                        Vector2 vCircle = new Vector2(circle.Speed, 0f);
                        float threshold = circle.Radius + (Detector != null ? Detector.CollisionMargin : 0f);
                        float maxLook = Detector != null ? Detector.MaxLookAhead : 0.5f;
                        float? tCol = TrackedObject.FindTimeToCollision(
                            circle.PositionXY, vCircle, threshold, maxLook);

                        if (tCol.HasValue)
                        {
                            float t = tCol.Value;
                            Vector2 trackedFuture = TrackedObject.PositionXY + TrackedObject.Velocity * t;
                            Vector2 circleFuture = circle.PositionXY + vCircle * t;
                            Vector3 contact = new Vector3(
                                (trackedFuture.x + circleFuture.x) * 0.5f,
                                (trackedFuture.y + circleFuture.y) * 0.5f, planeZ);

                            bool imminent = Detector != null && t <= Detector.EffectiveDetectionLeadTime;
                            Gizmos.color = Color.red;
                            DrawCross(contact, 0.01f);

                            if (imminent)
                            {
                                // 点滅する赤い球
                                float pulse = 0.006f + 0.004f * Mathf.Abs(Mathf.Sin(Time.realtimeSinceStartup * 8f));
                                Gizmos.DrawSphere(contact, pulse);
                            }
                        }
                    }
                }
            }
        }

        private static void DrawWireCircle(Vector3 center, float radius, float z, int segments = 32)
        {
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            prev.z = z;
            for (int i = 1; i <= segments; i++)
            {
                float ang = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius, 0f);
                next.z = z;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        private static void DrawCross(Vector3 c, float size)
        {
            Gizmos.DrawLine(c + new Vector3(-size, -size, 0f), c + new Vector3(size, size, 0f));
            Gizmos.DrawLine(c + new Vector3(-size, size, 0f), c + new Vector3(size, -size, 0f));
        }
    }
}
