using UnityEngine;

namespace AerialFlickGame.Config
{
    /// <summary>
    /// カメラの正射投影サイズを設定する。起動時に一度だけ適用（毎フレーム処理はしない）。
    /// エディタでは OnValidate でプレビュー反映（編集時のみ）。値は Inspector で可変。
    /// </summary>
    [ExecuteAlways]
    public class CameraOrthoLock : MonoBehaviour
    {
        [Tooltip("設定する Orthographic Size")]
        public float Size = 0.1f;

        [Tooltip("対象カメラ。null なら自身の Camera → Camera.main")]
        public Camera TargetCamera;

        private void OnEnable() => Apply();        // 起動時に一度
        private void OnValidate() => Apply();      // 編集時のプレビューのみ

        public void Apply()
        {
            Camera cam = TargetCamera != null ? TargetCamera : (GetComponent<Camera>() ?? Camera.main);
            if (cam == null) return;
            cam.orthographic = true;
            cam.orthographicSize = Size;
        }
    }
}
