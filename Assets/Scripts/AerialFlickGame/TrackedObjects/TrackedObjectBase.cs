using UnityEngine;
using UnityEngine.InputSystem;
using AerialFlickGame.Core;

namespace AerialFlickGame.TrackedObjects
{
    /// <summary>
    /// 追跡物体の基底クラス。OptiTrack から位置を取得し、速度を推定する。
    /// OptiTrack が接続されていない／トラッキングが外れている場合は
    /// マウスで XY 平面上を動かせるフォールバックを備える。
    /// </summary>
    public abstract class TrackedObjectBase : MonoBehaviour
    {
        [Header("OptiTrack")]
        [Tooltip("OptitrackStreamingClient を持つオブジェクト。null ならマウス操作にフォールバック。")]
        public OptitrackStreamingClient StreamingClient;

        [Tooltip("Motive 上のリジッドボディ Streaming ID")]
        public int RigidBodyId = 1;

        [Tooltip("速度推定に使う過去フレーム数")]
        public int VelocityFrames = 5;

        [Header("マウスフォールバック（トラッキング無しでのテスト用）")]
        [Tooltip("トラッキングが得られないときマウスで動かす")]
        public bool UseMouseWhenUntracked = true;

        [Tooltip("マウス操作を常に優先する（OptiTrack を無視）")]
        public bool ForceMouseControl = false;

        [Tooltip("マウス座標をワールドに投影するカメラ。null なら Camera.main")]
        public Camera InputCamera;

        [Tooltip("XY 平面の Z 位置（この平面上で衝突判定する）")]
        public float PlaneZ = 0f;

        [Header("姿勢フリップ除去")]
        [Tooltip("急な大ジャンプ姿勢（OptiTrackの反転など）を弾く。緩やかな傾きは追従する")]
        public bool RejectOrientationFlips = true;

        [Tooltip("1フレームでこの角度[deg]を超える姿勢変化は棄却し直前を保持する。" +
                 "緩やかな傾きはこれ未満なので追従する（急な反転・横向きは想定しないので復帰しない）")]
        public float MaxAnglePerFrame = 30f;

        // ---- 公開プロパティ ----
        public Vector3 Position { get; private set; }
        public Vector2 Velocity { get; private set; }
        public bool IsTracking { get; private set; }

        /// <summary>追跡剛体の姿勢（トラッキング時のみ有効。マウス時は identity）。</summary>
        public Quaternion Orientation { get; private set; } = Quaternion.identity;

        /// <summary>現在位置の XY 成分。</summary>
        public Vector2 PositionXY => new Vector2(Position.x, Position.y);

        /// <summary>衝突形状の中心 XY（既定は重心。オフセットのある形状はオーバーライド）。</summary>
        public virtual Vector2 ShapeCenterXY => PositionXY;

        /// <summary>
        /// 速度推定に使うサンプル点（既定は重心）。オフセット形状は「実際に接触する部分」を返す
        /// ことで、論文の“指中心の速度”に相当する量を推定できる。
        /// </summary>
        protected virtual Vector3 VelocitySamplePoint => Position;

        /// <summary>
        /// 飛来円が ballXY にあるときの衝突法線（XY, 単位ベクトル）。跳ね返り方向に使う。
        /// 既定は形状中心→円の向き。面など接触点で決めたい形状はオーバーライドする。
        /// </summary>
        public virtual Vector2 CollisionNormal(Vector2 ballXY)
        {
            Vector2 d = ballXY - ShapeCenterXY;
            return d.sqrMagnitude < 1e-10f ? Vector2.left : d.normalized;
        }

        private VelocityEstimator _estimator;

        // 姿勢フィルタ状態
        private Quaternion _lastOrientation = Quaternion.identity;
        private bool _hasOrientation;

        /// <summary>
        /// 1フレームで大きく飛ぶ姿勢（≈180°反転・横向きなど）を棄却して直前を保持する。
        /// 復帰はしない（急な反転・横向きは想定しないため）。緩やかな傾きは MaxAnglePerFrame 未満なので追従する。
        /// トラッキングが切れて再取得したときは基準を取り直す。
        /// </summary>
        private Quaternion FilterOrientation(Quaternion raw, bool tracked)
        {
            if (!tracked || !RejectOrientationFlips)
            {
                _lastOrientation = raw;
                _hasOrientation = tracked;
                return raw;
            }

            if (!_hasOrientation)
            {
                _lastOrientation = raw;
                _hasOrientation = true;
                return raw;
            }

            float angle = Quaternion.Angle(_lastOrientation, raw);
            if (angle > MaxAnglePerFrame)
            {
                // 急な大ジャンプ → 採用せず直前を保持（反転が続いてもホールドし続ける）
                return _lastOrientation;
            }

            _lastOrientation = raw; // 緩やかな変化 → 追従
            return raw;
        }

        protected virtual void Awake()
        {
            _estimator = new VelocityEstimator(VelocityFrames);
            Position = transform.position;
        }

        protected virtual void Update()
        {
            Vector3 newPos = Position;
            Quaternion rawRot = Orientation;
            bool tracked = false;

            // 1. OptiTrack から位置を取得（マウス強制でなければ）
            if (!ForceMouseControl && StreamingClient != null)
            {
                OptitrackRigidBodyState rbState = StreamingClient.GetLatestRigidBodyState(RigidBodyId);
                if (rbState != null && rbState.Pose != null)
                {
                    newPos = rbState.Pose.Position;
                    rawRot = rbState.Pose.Orientation;
                    tracked = true;
                }
            }

            // 2. トラッキングが無ければマウスにフォールバック
            if (!tracked && (UseMouseWhenUntracked || ForceMouseControl))
            {
                if (TryGetMouseWorldPosition(out Vector3 mousePos))
                {
                    newPos = mousePos;
                }
                rawRot = Quaternion.identity;
            }

            IsTracking = tracked;
            Position = newPos;
            Orientation = FilterOrientation(rawRot, tracked);
            transform.position = new Vector3(newPos.x, newPos.y, PlaneZ);

            // 3. 速度推定（接触部の速度を推定：既定は重心、リングはリング中心）
            _estimator.AddSample(VelocitySamplePoint, Time.time);
            Velocity = _estimator.EstimateVelocity();
        }

        /// <summary>マウスのスクリーン座標を XY 平面（Z = PlaneZ）上のワールド座標へ投影する。</summary>
        private bool TryGetMouseWorldPosition(out Vector3 world)
        {
            world = Position;
            if (Mouse.current == null) return false;

            Camera cam = InputCamera != null ? InputCamera : Camera.main;
            if (cam == null) return false;

            Vector2 screen = Mouse.current.position.ReadValue();

            if (cam.orthographic)
            {
                // 直交カメラ: スクリーン → ワールド。奥行きはカメラからの距離で指定。
                float depth = Mathf.Abs(cam.transform.position.z - PlaneZ);
                Vector3 sp = new Vector3(screen.x, screen.y, depth);
                Vector3 w = cam.ScreenToWorldPoint(sp);
                world = new Vector3(w.x, w.y, PlaneZ);
                return true;
            }
            else
            {
                // 透視カメラ: XY 平面（法線 +Z, Z = PlaneZ）とレイの交点を求める
                Ray ray = cam.ScreenPointToRay(new Vector3(screen.x, screen.y, 0f));
                Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, PlaneZ));
                if (plane.Raycast(ray, out float enter))
                {
                    Vector3 w = ray.GetPoint(enter);
                    world = new Vector3(w.x, w.y, PlaneZ);
                    return true;
                }
                return false;
            }
        }

        // ---- サブクラスで実装する抽象メソッド ----

        /// <summary>現在位置における追跡物体表面から円中心までの距離 [m]（内部なら負/0）。</summary>
        public abstract float ComputeDistanceTo(Vector2 circleCenter);

        /// <summary>
        /// 追跡物体と円が実効衝突距離に入る最初の時刻 [s] を返す。範囲内に無ければ null。
        /// 現在フレームの情報のみで完結する純粋計算（副作用なし）。
        /// </summary>
        /// <param name="circlePosNow">円の現在 XY 位置</param>
        /// <param name="circleVelocity">円の速度ベクトル [m/s]</param>
        /// <param name="collisionThreshold">円半径 + 余裕 [m]（各形状は自身の半径を加算する）</param>
        /// <param name="maxLookAheadSeconds">予測する上限時間 [s]</param>
        public abstract float? FindTimeToCollision(
            Vector2 circlePosNow,
            Vector2 circleVelocity,
            float collisionThreshold,
            float maxLookAheadSeconds);
    }
}
