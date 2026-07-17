using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using AerialFlickGame.Config;
using AerialFlickGame.TrackedObjects;

namespace AerialFlickGame.EditorTools
{
    /// <summary>
    /// 現在のシーンに、fps固定・カメラ正射サイズ固定(0.1)・OptiTrack Client を仕込む（非破壊）。
    /// </summary>
    public static class RuntimeConfigSetup
    {
        private const string ClientPrefabPath = "Assets/OptiTrack/Prefabs/Client - OptiTrack.prefab";

        [MenuItem("AerialFlickGame/Apply Runtime Config (fps, camera 0.1, OptiTrack)")]
        public static void Apply()
        {
            // --- fps コントローラ ---
            var cfg = GameObject.Find("RuntimeConfig");
            if (cfg == null)
            {
                cfg = new GameObject("RuntimeConfig");
                Undo.RegisterCreatedObjectUndo(cfg, "RuntimeConfig");
            }
            if (cfg.GetComponent<FrameRateController>() == null)
                Undo.AddComponent<FrameRateController>(cfg);

            // --- カメラ正射サイズ 0.1 固定 ---
            var cam = Camera.main;
            if (cam != null)
            {
                Undo.RecordObject(cam, "Camera ortho");
                cam.orthographic = true;
                cam.orthographicSize = 0.1f;
                var locker = cam.GetComponent<CameraOrthoLock>();
                if (locker == null) locker = Undo.AddComponent<CameraOrthoLock>(cam.gameObject);
                locker.Size = 0.1f;
                locker.TargetCamera = cam;
            }
            else
            {
                Debug.LogWarning("[RuntimeConfig] Main Camera が見つかりません。");
            }

            // --- OptiTrack Client をシーンに ---
            var client = Object.FindFirstObjectByType<OptitrackStreamingClient>(FindObjectsInactive.Include);
            if (client == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ClientPrefabPath);
                if (prefab != null)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    Undo.RegisterCreatedObjectUndo(go, "Add OptiTrack Client");
                    client = go.GetComponentInChildren<OptitrackStreamingClient>();
                }
                else Debug.LogWarning("[RuntimeConfig] Client プレハブが見つかりません: " + ClientPrefabPath);
            }
            // TrackedObject に配線
            if (client != null)
            {
                foreach (var to in Object.FindObjectsByType<TrackedObjectBase>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    Undo.RecordObject(to, "wire client");
                    to.StreamingClient = client;
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[RuntimeConfig] fps固定(既定60)・カメラ正射0.1固定・OptiTrack Client を適用しました。" +
                      " fps 値は RuntimeConfig の FrameRateController で変更できます。");
        }
    }
}
