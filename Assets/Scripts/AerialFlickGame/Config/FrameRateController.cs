using UnityEngine;

namespace AerialFlickGame.Config
{
    /// <summary>
    /// 実行時（エディタ Play・ビルド）に fps を固定する。値は Inspector で可変、実行中の変更も反映。
    /// </summary>
    public class FrameRateController : MonoBehaviour
    {
        [Tooltip("目標fps。0以下で無制限")]
        public int TargetFps = 60;

        [Tooltip("VSync を使う（オンだとモニタ同期・TargetFps は無視される）")]
        public bool VSync = false;

        private void OnEnable() => Apply();        // 起動時に一度
        private void OnValidate() => Apply();      // Inspector で値を変えたとき（実行中なら反映）

        public void Apply()
        {
            if (!Application.isPlaying) return;    // 実行時のみ適用（毎フレーム処理なし）
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            // VSync 有効時は targetFrameRate を無効化(-1)。無制限指定(0以下)も -1。
            Application.targetFrameRate = VSync ? -1 : (TargetFps > 0 ? TargetFps : -1);
        }
    }
}
