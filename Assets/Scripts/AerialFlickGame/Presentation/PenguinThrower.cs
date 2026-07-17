using System.Collections;
using UnityEngine;

namespace AerialFlickGame.Presentation
{
    /// <summary>
    /// 地面（休止位置）から球の高さ(targetY)までジャンプし、頂点で投げ（リリース）、着地する。
    /// 球のスポーンは CircleSpawner がこのリリースに同期して行う。
    /// Jump アニメ（Animator の Trigger "Jump"）を併用。
    /// </summary>
    public class PenguinThrower : MonoBehaviour
    {
        [Header("アニメーション")]
        [Tooltip("ジャンプ時に再生する Animator ステート名（遷移に依存せず直接再生）")]
        public string JumpState = "Jump_Emperor";
        [Tooltip("待機/着地後に戻す Animator ステート名（空なら何もしない）")]
        public string IdleState = "Idle2_Emperor";
        [Tooltip("クロスフェード時間 [s]")]
        public float AnimFade = 0.05f;

        [Header("ジャンプ")]
        [Tooltip("上昇にかける時間 [s]")]
        public float RiseTime = 0.20f;
        [Tooltip("頂点で静止する時間 [s]")]
        public float HangTime = 0.05f;
        [Tooltip("下降にかける時間 [s]")]
        public float FallTime = 0.20f;
        [Tooltip("頂点で +X 方向へ踏み込む距離 [m]")]
        public float ForwardLunge = 0.02f;
        [Tooltip("球を離す位置の +X オフセット（手の前）[m]")]
        public float LaunchForwardX = 0.02f;
        [Tooltip("ピボット(足元)から投げる高さ(体の中心)までの上方向オフセット [m]。" +
                 "この分だけ足元を下げてジャンプし、球は中心の高さから出す")]
        public float LaunchUpY = 0.08f;

        private Animator _animator;
        private Vector3 _homePos;   // 休止（地面）位置。Awake 時の配置を基準にする
        private Coroutine _co;

        private void Awake()
        {
            _homePos = transform.position;
            _animator = GetComponentInChildren<Animator>();
            if (_animator != null) _animator.applyRootMotion = false; // 手続き移動と競合させない
        }

        private void Start()
        {
            // 起動時は待機ステートに（デフォルトステートが Dead 等でも上書き）
            if (_animator != null && !string.IsNullOrEmpty(IdleState))
                _animator.Play(IdleState, 0, 0f);
        }

        /// <summary>
        /// targetY まで跳んで頂点でリリースする。onRelease には球を離すワールド位置が渡る。
        /// </summary>
        public void JumpAndThrow(float targetY, System.Action<Vector3> onRelease)
        {
            if (!isActiveAndEnabled)
            {
                onRelease?.Invoke(new Vector3(_homePos.x + LaunchForwardX, targetY, _homePos.z));
                return;
            }
            if (_co != null) { StopCoroutine(_co); transform.position = _homePos; }
            _co = StartCoroutine(JumpRoutine(targetY, onRelease));
        }

        private IEnumerator JumpRoutine(float targetY, System.Action<Vector3> onRelease)
        {
            if (_animator != null && !string.IsNullOrEmpty(JumpState))
                _animator.CrossFadeInFixedTime(JumpState, AnimFade, 0);

            float startY = _homePos.y;
            // 体の中心が targetY に来るように、足元(ピボット)は LaunchUpY だけ下げた高さまで跳ぶ
            float apexY = targetY - LaunchUpY;

            // 上昇（ease-out）＋前方へ踏み込み
            float t = 0f;
            while (t < RiseTime)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / RiseTime);
                float e = 1f - (1f - u) * (1f - u);
                SetPose(Mathf.Lerp(startY, apexY, e), ForwardLunge * e);
                yield return null;
            }
            SetPose(apexY, ForwardLunge);

            // 頂点でリリース → 球は体の中心の高さ(targetY)から発生
            onRelease?.Invoke(new Vector3(_homePos.x + LaunchForwardX, targetY, _homePos.z));

            if (HangTime > 0f) yield return new WaitForSeconds(HangTime);

            // 下降（ease-in）
            t = 0f;
            while (t < FallTime)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / FallTime);
                float e = u * u;
                SetPose(Mathf.Lerp(apexY, startY, e), ForwardLunge * (1f - e));
                yield return null;
            }
            SetPose(startY, 0f);

            if (_animator != null && !string.IsNullOrEmpty(IdleState))
                _animator.CrossFadeInFixedTime(IdleState, AnimFade, 0);

            _co = null;
        }

        private void SetPose(float y, float forwardX)
        {
            transform.position = new Vector3(_homePos.x + forwardX, y, _homePos.z);
        }
    }
}
