using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using AerialFlickGame.Core;
using AerialFlickGame.TrackedObjects;
using AerialFlickGame.DebugTools;
using AerialFlickGame.Presentation;

namespace AerialFlickGame.EditorTools
{
    /// <summary>
    /// 追跡形状（Cylinder / Box / Ring）をワンクリックで切り替える。
    /// 対象コンポーネントを有効化し他を無効化、PredictiveHitDetector と PredictionGizmo の
    /// 参照を差し替え、見た目（Ring のときは円、それ以外は Sphere）も切り替える。
    /// </summary>
    public static class TrackedShapeMenu
    {
        [MenuItem("AerialFlickGame/Tracked Shape/Use Cylinder")]
        public static void UseCylinder() => Switch<CylinderTracked>();

        [MenuItem("AerialFlickGame/Tracked Shape/Use Box")]
        public static void UseBox() => Switch<BoxTracked>();

        [MenuItem("AerialFlickGame/Tracked Shape/Use Ring")]
        public static void UseRing() => Switch<RingTracked>();

        private static void Switch<T>() where T : TrackedObjectBase
        {
            var any = Object.FindFirstObjectByType<TrackedObjectBase>(FindObjectsInactive.Include);
            if (any == null)
            {
                EditorUtility.DisplayDialog("Not found",
                    "シーンに TrackedObject（CylinderTracked 等）が見つかりません。", "OK");
                return;
            }
            var go = any.gameObject;

            var target = go.GetComponent<T>();
            if (target == null) target = Undo.AddComponent<T>(go);

            // 対象だけ有効化、他の追跡コンポーネントは無効化
            foreach (var comp in go.GetComponents<TrackedObjectBase>())
            {
                Undo.RecordObject(comp, "Switch Tracked Shape");
                comp.enabled = (comp == target);
            }

            // 参照差し替え
            var detector = Object.FindFirstObjectByType<PredictiveHitDetector>(FindObjectsInactive.Include);
            if (detector != null)
            {
                Undo.RecordObject(detector, "Switch Tracked Shape");
                detector.TrackedObject = target;
            }
            var gizmo = go.GetComponent<PredictionGizmo>();
            if (gizmo != null)
            {
                Undo.RecordObject(gizmo, "Switch Tracked Shape");
                gizmo.TrackedObject = target;
            }

            // 見た目
            bool isRing = target is RingTracked;
            var visual = go.transform.Find("Visual");
            if (visual != null) visual.gameObject.SetActive(!isRing);

            var ringVis = go.GetComponent<RingVisual>();
            if (isRing)
            {
                if (ringVis == null) ringVis = Undo.AddComponent<RingVisual>(go);
                ringVis.Ring = target as RingTracked;
                ringVis.enabled = true;
            }
            else if (ringVis != null)
            {
                ringVis.enabled = false;
            }

            EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeGameObject = go;
            Debug.Log($"[TrackedShape] {typeof(T).Name} に切り替えました。");
        }
    }
}
