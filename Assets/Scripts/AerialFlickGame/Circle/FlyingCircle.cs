using System.Collections.Generic;
using UnityEngine;
using AerialFlickGame.Core;
using AerialFlickGame.Game;

namespace AerialFlickGame.Circle
{
    /// <summary>
    /// 左→右へ等速直線運動で飛来する円。CircleSpawner から Initialize される。
    /// </summary>
    public class FlyingCircle : MonoBehaviour
    {
        public enum CircleState { Flying, Hit, Missed }

        [Header("パラメータ（Initialize で上書き）")]
        public float Speed = 0.3f;       // [m/s]
        public float Radius = 0.010f;    // [m]
        public float RightBoundX = 0.3f; // この X を超えたらミス

        [Tooltip("反発係数 e。1=完全弾性(よく跳ねる) / 0=完全非弾性(指の速さで押し出す・論文相当)")]
        public float Restitution = 1.0f;

        [Tooltip("true=水平方向のみに跳ね返す(論文の1D) / false=当たり位置で角度がつく2D")]
        public bool HorizontalBounceOnly = false;

        [Tooltip("ヒット後に消えるまでの時間 [s]")]
        public float HitLifetime = 0.6f;

        public CircleState State { get; private set; } = CircleState.Flying;

        public Vector2 PositionXY => new Vector2(transform.position.x, transform.position.y);

        /// <summary>Gizmo などが参照する現在アクティブな円の一覧。</summary>
        public static readonly List<FlyingCircle> Active = new List<FlyingCircle>();

        private PredictiveHitDetector _detector;
        private float _hitTimer;
        private Vector2 _bounceVelocity; // ヒット後の速度ベクトル [m/s]

        public void Initialize(PredictiveHitDetector detector, float speed, float radius,
            float rightBoundX, float restitution, bool horizontalBounceOnly)
        {
            _detector = detector;
            Speed = speed;
            Radius = radius;
            RightBoundX = rightBoundX;
            Restitution = restitution;
            HorizontalBounceOnly = horizontalBounceOnly;
            State = CircleState.Flying;
        }

        private void OnEnable() { Active.Add(this); }
        private void OnDisable() { Active.Remove(this); }

        private void Update()
        {
            switch (State)
            {
                case CircleState.Flying:
                    transform.position += Vector3.right * Speed * Time.deltaTime;

                    // 追跡物体は detector が参照しているので、ここでは自分の位置を評価してもらう
                    if (_detector != null) _detector.EvaluateForCircle(this);

                    if (transform.position.x > RightBoundX)
                    {
                        State = CircleState.Missed;
                        if (GameManager.Instance != null) GameManager.Instance.RegisterMiss();
                        Destroy(gameObject);
                    }
                    break;

                case CircleState.Hit:
                    // 反発後の速度ベクトルで移動、一定時間後に消滅
                    transform.position += new Vector3(_bounceVelocity.x, _bounceVelocity.y, 0f) * Time.deltaTime;
                    _hitTimer += Time.deltaTime;
                    if (_hitTimer >= HitLifetime) Destroy(gameObject);
                    break;
            }
        }

        /// <summary>PredictiveHitDetector からヒット発火時に呼ばれる。</summary>
        public void OnHitDetected(HitPrediction pred)
        {
            if (State != CircleState.Flying) return; // 同一円へのヒットは 1 回だけ

            State = CircleState.Hit;
            _hitTimer = 0f;
            _bounceVelocity = ComputeBounceVelocity();

            if (GameManager.Instance != null) GameManager.Instance.RegisterHit();

            Debug.Log("[HIT] " +
                $"leadTime={pred.TimeToCollision * 1000f:F1}ms | " +
                $"v_tracked={pred.VTracked:F3}m/s | " +
                $"v_rel={pred.VRelative:F3}m/s | " +
                $"margin={pred.CollisionMarginUsed * 100f:F2}cm | " +
                $"v_bounce={_bounceVelocity.magnitude:F3}m/s | e={Restitution:F2}");
        }

        /// <summary>
        /// 反発後の円の速度を求める（追跡物体 ≫ 円 の質量極限）。
        /// 衝突法線 n（指→円）方向に反発係数の式を適用し、接線成分は保存する。
        ///   v_c'(法線) = (1+e)·v_f(法線) − e·v_c(法線)
        /// </summary>
        private Vector2 ComputeBounceVelocity()
        {
            Vector2 circleVel = new Vector2(Speed, 0f); // 飛来中の速度は +X 一定

            if (_detector == null || _detector.TrackedObject == null)
            {
                return -circleVel; // フォールバック: 逆向き同速
            }

            Vector2 fingerVel = _detector.TrackedObject.Velocity;

            // 衝突法線 n＝「円と形状が接触した箇所」基準（形状ごとに算出。円柱=中心方向、面=接触点方向）
            Vector2 n = _detector.TrackedObject.CollisionNormal(PositionXY);

            if (HorizontalBounceOnly)
            {
                // 水平方向に固定
                n = new Vector2(n.x, 0f);
                if (n.sqrMagnitude < 1e-8f) n = Vector2.left;
                n.Normalize();
            }

            float e = Restitution;
            float vcn = Vector2.Dot(circleVel, n); // 円の法線成分
            float vfn = Vector2.Dot(fingerVel, n); // 指の法線成分
            float vcnNew = (1f + e) * vfn - e * vcn;

            // 接線成分は保持し、法線成分だけ差し替える
            return circleVel + (vcnNew - vcn) * n;
        }
    }
}
