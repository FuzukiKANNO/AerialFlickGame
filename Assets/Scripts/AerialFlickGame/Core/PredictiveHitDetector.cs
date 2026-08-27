using System;
using UnityEngine;
using AerialFlickGame.TrackedObjects;
using AerialFlickGame.Circle;

namespace AerialFlickGame.Core
{
    /// <summary>
    /// 飛来する円 1 つ 1 つに対し毎フレーム衝突予測を行い、
    /// 物理接触の detectionLeadTime 前にヒットを発火する。
    /// </summary>
    /// <summary>リード時間の指定単位。</summary>
    public enum LeadTimeMode
    {
        Seconds, // 秒で指定（フレームレート非依存・再現性重視）
        Frames,  // フレーム数で指定（表示フレームに合わせたいとき）
    }

    /// <summary>検出の方式。</summary>
    public enum DetectionMode
    {
        // 論文（VRSJ2026）に忠実なモデル。追跡物体（指）だけを LeadTime 分
        // 先読みし、円は実位置で判定する。有効当たり判定距離 = LeadTime×指速度 + margin。
        // 指が静止なら実接触で発火し、円の速度は発火タイミングに関係しない。
        [InspectorName("CompensatedContact (論文モデル・推奨)")]
        CompensatedContact,
        // 相対的な衝突時刻を予測し、その LeadTime 前に発火する（円の速度でも早出しする）。
        // 反応時間モデルの根拠から外れ、静止時も円速度で早出しするため【非実験向け／ゲーム演出用】。
        [InspectorName("Predictive (非実験向け/演出用)")]
        Predictive,
        // 予測も補償もせず、実際に接触した瞬間（今の実距離）で発火する。
        [InspectorName("PhysicalContact (実接触)")]
        PhysicalContact,
    }

    public class PredictiveHitDetector : MonoBehaviour
    {
        [Header("検出方式")]
        [Tooltip("CompensatedContact=論文モデル(推奨) / Predictive=相対衝突時刻の先出し【非実験向け/演出用】 / PhysicalContact=実接触")]
        public DetectionMode Mode = DetectionMode.CompensatedContact;

        [Header("予測パラメータ")]
        [Tooltip("リード時間を秒で指定するかフレーム数で指定するか")]
        public LeadTimeMode LeadMode = LeadTimeMode.Seconds;

        [Tooltip("物理接触の何秒前に判定を発火するか [s]（LeadMode = Seconds のとき使用）")]
        public float DetectionLeadTime = 0.100f;

        [Tooltip("物理接触の何フレーム前に判定を発火するか（LeadMode = Frames のとき使用）")]
        public int DetectionLeadFrames = 6;

        [Tooltip("追加の衝突余裕 [m]")]
        public float CollisionMargin = 0.000f;

        [Tooltip("円の半径 [m]（スポーンのデフォルト。判定は各円の実半径を使用）")]
        public float CircleRadius = 0.010f;

        [Tooltip("予測する上限時間 [s]")]
        public float MaxLookAhead = 0.500f;

        [Header("奥行き(Z)の許容範囲 — finger_from_side 準拠")]
        [Tooltip("奥行きゲートを使う。追跡物体の実 Z がこの範囲外なら発火しない")]
        public bool UseDepthGate = true;

        [Tooltip("空中像が結像する基準 Z 座標")]
        public float ImagePlaneZ = 0f;

        [Tooltip("基準面より +Z 側（手前/カメラ寄り）にどれだけ離れても反応するか [m]")]
        public float ZToleranceFront = 0.015f;

        [Tooltip("基準面より -Z 側（奥）にどれだけ離れても反応するか [m]")]
        public float ZToleranceBack = 0.010f;

        [Header("高さ(Y)の許容範囲")]
        [Tooltip("高さゲートを使う。指と円中心の Y 差がこの値を超えたら発火しない")]
        public bool UseHeightGate = false;

        [Tooltip("円中心との Y 差の許容 [m]")]
        public float HeightTolerance = 0.02f;

        [Header("参照")]
        [Tooltip("CylinderTracked または BoxTracked")]
        public TrackedObjectBase TrackedObject;

        /// <summary>外部購読用イベント（スコア表示・ロギング等）。</summary>
        public event Action<HitPrediction> OnHitDetected;

        /// <summary>
        /// 実際に発火判定に使うリード時間 [s]。Frames モードでは
        /// 「N フレーム × 1フレームの実時間」に換算する。
        /// フレーム時間は平滑化した smoothDeltaTime を使い、フレーム変動でしきい値が
        /// ばたつくのを防ぐ。
        /// </summary>
        public float EffectiveDetectionLeadTime
        {
            get
            {
                if (LeadMode == LeadTimeMode.Frames)
                {
                    float dt = Time.smoothDeltaTime;
                    if (dt <= 0f) dt = Time.deltaTime;
                    if (dt <= 0f) dt = 1f / 60f; // エディタ停止時などの保険
                    return DetectionLeadFrames * dt;
                }
                return DetectionLeadTime;
            }
        }

        // --- デバッグ／Gizmo 用: 直近の予測結果 ---
        public bool HasPrediction { get; private set; }
        public float LastTimeToCollision { get; private set; }
        public Vector2 LastPredictedContactPoint { get; private set; }

        private void Awake()
        {
            if (TrackedObject == null)
            {
                TrackedObject = GetComponentInChildren<TrackedObjectBase>();
            }
        }

        /// <summary>
        /// 飛んでくる円 1 つについて衝突予測を評価する。FlyingCircle.Update から毎フレーム呼ばれる。
        /// Flying 状態の円のみ判定する。ヒットしたら円へ通知し、イベントを発火する。
        /// </summary>
        public void EvaluateForCircle(FlyingCircle circle)
        {
            HasPrediction = false;

            if (TrackedObject == null || circle == null) return;
            if (circle.State != FlyingCircle.CircleState.Flying) return;

            // 奥行き(Z)・高さ(Y)の許容範囲チェック（範囲外なら発火しない）
            if (!PassesSensingGate(circle)) return;

            Vector2 qNow = circle.PositionXY;
            Vector2 vCircle = new Vector2(circle.Speed, 0f); // 左→右の水平飛来

            switch (Mode)
            {
                case DetectionMode.CompensatedContact:
                    EvaluateCompensatedContact(circle, qNow, vCircle);
                    break;
                case DetectionMode.PhysicalContact:
                    EvaluatePhysicalContact(circle, qNow, vCircle);
                    break;
                default:
                    EvaluatePredictive(circle, qNow, vCircle);
                    break;
            }
        }

        /// <summary>
        /// 奥行き(Z)・高さ(Y)の許容範囲内かどうか。範囲外なら発火させない。
        /// Z は追跡物体の実位置 Position.z（transform は PlaneZ に潰しているが Position は実値を保持）。
        /// </summary>
        private bool PassesSensingGate(FlyingCircle circle)
        {
            // 奥行き(Z): 基準面より手前(+Z)と奥(-Z)で別々の許容範囲
            if (UseDepthGate)
            {
                float zDiff = TrackedObject.Position.z - ImagePlaneZ;
                bool inZ = zDiff >= 0f
                    ? zDiff <= ZToleranceFront
                    : Mathf.Abs(zDiff) <= ZToleranceBack;
                if (!inZ) return false;
            }

            // 高さ(Y): 指（ラケット）と円中心の Y 差
            if (UseHeightGate)
            {
                float yDiff = Mathf.Abs(TrackedObject.Position.y - circle.PositionXY.y);
                if (yDiff > HeightTolerance) return false;
            }

            return true;
        }

        /// <summary>
        /// 論文モデル: 追跡物体（指）だけを LeadTime 分先読みし、円は実位置で判定する。
        /// 有効当たり判定距離 = 円半径 + CollisionMargin + (LeadTime × 指の接近速度)。
        /// 指が静止していれば LeadTime 項は消え、実接触の瞬間に発火する。
        /// </summary>
        private void EvaluateCompensatedContact(FlyingCircle circle, Vector2 qNow, Vector2 vCircle)
        {
            float lead = EffectiveDetectionLeadTime;
            Vector2 vTracked = TrackedObject.Velocity;

            // 追跡物体を lead 分先読みするのと、円を -vTracked*lead ずらして今の距離を測るのは等価
            Vector2 qShifted = qNow - vTracked * lead;
            float surfaceToCenter = TrackedObject.ComputeDistanceTo(qShifted);
            float contactThreshold = circle.Radius + CollisionMargin;

            // Gizmo 用: 予測しているラケットｍの到達位置を接触予測点として保持
            HasPrediction = surfaceToCenter <= contactThreshold;
            LastTimeToCollision = 0f;
            LastPredictedContactPoint = qNow;

            if (surfaceToCenter <= contactThreshold)
            {
                // 接触の瞬間で発火（先読みは距離に埋め込み済みなので t=0）
                Fire(circle, 0f, vCircle, qNow);
            }
        }

        /// <summary>未来予測モード: 接触の EffectiveDetectionLeadTime 前に発火。</summary>
        private void EvaluatePredictive(FlyingCircle circle, Vector2 qNow, Vector2 vCircle)
        {
            float threshold = circle.Radius + CollisionMargin;

            float? tCol = TrackedObject.FindTimeToCollision(
                qNow, vCircle, threshold, MaxLookAhead);

            if (tCol == null) return; // 予測範囲内に衝突なし

            float t = tCol.Value;
            Vector2 trackedFuture = TrackedObject.ShapeCenterXY + TrackedObject.Velocity * t;
            Vector2 circleFuture = qNow + vCircle * t;
            Vector2 contact = (trackedFuture + circleFuture) * 0.5f;

            // Gizmo 用に予測情報を保持
            HasPrediction = true;
            LastTimeToCollision = t;
            LastPredictedContactPoint = contact;

            if (t <= EffectiveDetectionLeadTime)
            {
                Fire(circle, t, vCircle, contact);
            }
        }

        /// <summary>物理接触モード: 予測せず、今の実距離が接触距離以下になった瞬間に発火。</summary>
        private void EvaluatePhysicalContact(FlyingCircle circle, Vector2 qNow, Vector2 vCircle)
        {
            // ComputeDistanceTo は「追跡物体の表面から円中心までの距離」。
            // 円半径（+余裕）以下なら実際に接触している。
            float surfaceToCenter = TrackedObject.ComputeDistanceTo(qNow);
            float contactThreshold = circle.Radius + CollisionMargin;

            if (surfaceToCenter <= contactThreshold)
            {
                Fire(circle, 0f, vCircle, qNow); // 接触なのでリード時間 0
            }
        }

        /// <summary>ヒット情報を組み立てて外部へ通知する。</summary>
        private void Fire(FlyingCircle circle, float t, Vector2 vCircle, Vector2 contact)
        {
            Vector2 vTracked = TrackedObject.Velocity;
            Vector2 vRel = vTracked - vCircle;

            HitPrediction pred = new HitPrediction
            {
                TimeToCollision = t,
                VTracked = vTracked.magnitude,
                VCircle = vCircle.magnitude,
                VRelative = vRel.magnitude,
                CollisionMarginUsed = CollisionMargin,
                PredictedContactPoint = contact,
            };

            // 円へ通知（状態遷移・スコア・ログ）。以降この円は判定されない。
            circle.OnHitDetected(pred);
            // 監視者へ通知
            OnHitDetected?.Invoke(pred);
        }
    }
}
