using UnityEngine;

namespace AerialFlickGame.Core
{
    /// <summary>予測ヒット発火時に渡される情報。</summary>
    public struct HitPrediction
    {
        public float TimeToCollision;      // 予測衝突までの残り時間 [s]
        public float VTracked;             // 追跡物体の速度の大きさ [m/s]
        public float VCircle;              // 円の速度 [m/s]
        public float VRelative;            // 相対速度の大きさ [m/s]
        public float CollisionMarginUsed;  // 使用した CollisionMargin [m]
        public Vector2 PredictedContactPoint; // 予測衝突位置 [m]
    }
}
