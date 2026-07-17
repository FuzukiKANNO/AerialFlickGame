using UnityEngine;
using AerialFlickGame.Core;
using AerialFlickGame.Game;
using AerialFlickGame.Presentation;

namespace AerialFlickGame.Circle
{
    /// <summary>
    /// 一定間隔で FlyingCircle を左端からスポーンする。
    /// </summary>
    public class CircleSpawner : MonoBehaviour
    {
        [Header("参照")]
        public PredictiveHitDetector HitDetector;

        [Tooltip("スポーンする円のプレハブ。null なら Sphere プリミティブを生成")]
        public GameObject CirclePrefab;

        [Header("スポーン設定")]
        public float SpawnInterval = 2.0f;   // [s]
        public float SpawnX = -0.3f;          // スポーン X 座標 [m]
        public float YRange = 0.08f;          // ±Y 範囲 [m]
        public float CircleRadius = 0.010f;   // [m]
        public float CircleSpeed = 0.3f;      // [m/s]

        [Tooltip("反発係数 e。1=完全弾性 / 0=非弾性(指の速さで押し出す・論文相当)")]
        [Range(0f, 1f)]
        public float Restitution = 1.0f;

        [Tooltip("true=水平方向のみに跳ね返す(論文の1D) / false=当たり位置で角度がつく2D")]
        public bool HorizontalBounceOnly = false;

        [Tooltip("この X を超えた円はミス扱いで消滅。既定はスポーン X の対称位置")]
        public float RightBoundX = 0.3f;

        [Tooltip("生成する円の Z 位置（判定平面に合わせる）")]
        public float PlaneZ = 0f;

        [Tooltip("円に適用するマテリアル（任意）")]
        public Material CircleMaterial;

        [Header("演出")]
        [Tooltip("設定すると、ペンギンが targetY までジャンプして投げた瞬間に球を発生させる")]
        public PenguinThrower Thrower;

        /// <summary>円をスポーンした瞬間に発火（ペンギンの投げモーション等の同期用）。</summary>
        public event System.Action<Vector3> OnSpawn;

        private float _timer;

        private void Awake()
        {
            if (HitDetector == null) HitDetector = FindFirstObjectByType<PredictiveHitDetector>();
        }

        private void Update()
        {
            // ゲーム中でなければスポーンしない
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

            _timer += Time.deltaTime;
            if (_timer >= SpawnInterval)
            {
                _timer = 0f;
                Spawn();
            }
        }

        private void Spawn()
        {
            float y = Random.Range(-YRange, YRange);

            // ペンギンがいれば、頂点(targetY)で投げたリリース位置から球を発生させる
            if (Thrower != null && Thrower.isActiveAndEnabled)
                Thrower.JumpAndThrow(y, SpawnCircleAt);
            else
                SpawnCircleAt(new Vector3(SpawnX, y, PlaneZ));
        }

        private void SpawnCircleAt(Vector3 pos)
        {
            pos.z = PlaneZ; // 判定面に固定（発生源の Z によらず）

            GameObject obj;
            if (CirclePrefab != null)
            {
                obj = Instantiate(CirclePrefab, pos, Quaternion.identity);
            }
            else
            {
                obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                obj.transform.position = pos;
                // 物理判定は使わないのでコライダー除去
                Collider col = obj.GetComponent<Collider>();
                if (col != null) Destroy(col);
                if (CircleMaterial != null)
                {
                    obj.GetComponent<Renderer>().sharedMaterial = CircleMaterial;
                }
            }

            obj.name = "FlyingCircle";
            obj.transform.localScale = Vector3.one * (CircleRadius * 2f);

            FlyingCircle ctrl = obj.GetComponent<FlyingCircle>();
            if (ctrl == null) ctrl = obj.AddComponent<FlyingCircle>();
            ctrl.Initialize(HitDetector, CircleSpeed, CircleRadius, RightBoundX, Restitution, HorizontalBounceOnly);

            OnSpawn?.Invoke(pos);
        }
    }
}
