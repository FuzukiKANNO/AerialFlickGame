using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using AerialFlickGame.Core;
using AerialFlickGame.TrackedObjects;
using AerialFlickGame.Circle;
using AerialFlickGame.Game;
using AerialFlickGame.DebugTools;
using AerialFlickGame.Recording;
using AerialFlickGame.Config;

namespace AerialFlickGame.EditorTools
{
    /// <summary>
    /// メニュー AerialFlickGame/Build Game Scene から、配線済みのプレイ可能な
    /// シーンをプログラムで自動生成する。手作業の組み立てを不要にする。
    /// </summary>
    public static class SceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/AerialFlickGame.unity";
        private const string PrefabPath = "Assets/Prefabs/FlyingCircle.prefab";

        [MenuItem("AerialFlickGame/Build Game Scene")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- カメラ（XY 平面を正面から見る直交カメラ）----
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.orthographic = true;
            cam.orthographicSize = 0.10f;                 // 表示縦半幅 0.1 m（固定）
            cam.transform.position = new Vector3(0f, 0f, -1f);
            cam.transform.rotation = Quaternion.identity; // +Z を向く → XY 平面を正視
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
            cam.nearClipPlane = 0.01f;
            var orthoLock = camGo.AddComponent<CameraOrthoLock>(); // ビルド/実行時も 0.1 固定
            orthoLock.Size = 0.10f;
            orthoLock.TargetCamera = cam;

            // ---- ライト ----
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // ---- マテリアル（URP Lit）----
            Material trackedMat = CreateMaterial("TrackedObjectMat", new Color(0.2f, 0.7f, 1f));
            Material circleMat = CreateMaterial("CircleMat", new Color(1f, 0.55f, 0.1f));

            // ---- Manager（PredictiveHitDetector + GameManager）----
            var managerGo = new GameObject("Manager");
            var detector = managerGo.AddComponent<PredictiveHitDetector>();
            var gameManager = managerGo.AddComponent<GameManager>();
            detector.Mode = DetectionMode.CompensatedContact; // 論文モデル
            detector.DetectionLeadTime = 0.089f;               // 上限・下限傾きの平均 89ms
            detector.CollisionMargin = 0.000f;                 // 論文の定数オフセット。必要なら ~0.01 に
            detector.CircleRadius = 0.010f;                    // 結像面での直径 2cm
            detector.MaxLookAhead = 0.500f;                    // Predictive モード時のみ使用

            // ---- TrackedObject（Cylinder / Box 両方を持たせ、Cylinder を既定に）----
            var trackedGo = new GameObject("TrackedObject");
            trackedGo.transform.position = new Vector3(0.1f, 0f, 0f);

            var cylinder = trackedGo.AddComponent<CylinderTracked>();
            cylinder.CylinderRadius = 0.03f;
            cylinder.VelocityFrames = 5;
            cylinder.UseMouseWhenUntracked = true;
            cylinder.InputCamera = cam;

            var box = trackedGo.AddComponent<BoxTracked>();
            box.BoxWidth = 0.06f;
            box.BoxHeight = 0.04f;
            box.VelocityFrames = 5;
            box.UseMouseWhenUntracked = true;
            box.InputCamera = cam;
            box.enabled = false; // 既定は Cylinder。Box を使うときは入れ替える。

            // リング型アイテム（重心の下に円）。使うときは enabled にして detector.TrackedObject を差し替え
            var ring = trackedGo.AddComponent<RingTracked>();
            ring.Diameter = 0.12f;
            ring.EdgeToCentroid = 0.01f;
            ring.VelocityFrames = 5;
            ring.UseMouseWhenUntracked = true;
            ring.InputCamera = cam;
            ring.enabled = false;

            detector.TrackedObject = cylinder;

            var gizmo = trackedGo.AddComponent<PredictionGizmo>();
            gizmo.Detector = detector;
            gizmo.TrackedObject = cylinder;

            // 見た目（円柱を正面から見た円 = Sphere を直径にスケール）
            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "Visual";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(trackedGo.transform, false);
            visual.transform.localScale = Vector3.one * (cylinder.CylinderRadius * 2f);
            visual.GetComponent<Renderer>().sharedMaterial = trackedMat;

            // ---- OptiTrack Client（ビルドに含める）----
            var clientPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/OptiTrack/Prefabs/Client - OptiTrack.prefab");
            if (clientPrefab != null)
            {
                var clientGo = (GameObject)PrefabUtility.InstantiatePrefab(clientPrefab);
                var client = clientGo.GetComponentInChildren<OptitrackStreamingClient>();
                cylinder.StreamingClient = client;
                box.StreamingClient = client;
                ring.StreamingClient = client;
            }

            // ---- 実行時設定（fps 固定）----
            var runtimeCfg = new GameObject("RuntimeConfig");
            var fps = runtimeCfg.AddComponent<FrameRateController>();
            fps.TargetFps = 60; // Inspector で変更可

            // ---- 飛来円プレハブ ----
            GameObject circlePrefab = CreateCirclePrefab(circleMat);

            // ---- Spawner ----
            var spawnerGo = new GameObject("Spawner");
            var spawner = spawnerGo.AddComponent<CircleSpawner>();
            spawner.HitDetector = detector;
            spawner.CirclePrefab = circlePrefab;
            spawner.SpawnInterval = 2.0f;
            spawner.SpawnX = -0.3f;
            spawner.YRange = 0.08f;
            spawner.CircleRadius = 0.010f;
            spawner.CircleSpeed = 0.3f;
            spawner.RightBoundX = 0.3f;
            spawner.Restitution = 1.0f; // 1=よく跳ねる。論文相当は 0
            spawner.HorizontalBounceOnly = false; // true=水平のみ(論文1D) / false=角度がつく2D
            spawner.CircleMaterial = circleMat;

            // ---- Recorder ----
            var recorderGo = new GameObject("Recorder");
            var recorder = recorderGo.AddComponent<PositionRecorder>();
            recorder.TrackedObject = cylinder;
            recorder.Detector = detector;

            // ---- UI ----
            BuildUI(gameManager);

            // ---- 保存 ----
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[AerialFlickGame] シーンを生成しました: {ScenePath}\n" +
                      "Play を押すと、OptiTrack 未接続でもマウスで TrackedObject を動かせます。");
        }

        private static Material CreateMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader) { name = name };
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);

            System.IO.Directory.CreateDirectory("Assets/Materials");
            string path = $"Assets/Materials/{name}.mat";
            AssetDatabase.CreateAsset(mat, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static GameObject CreateCirclePrefab(Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "FlyingCircle";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.AddComponent<FlyingCircle>();
            go.transform.localScale = Vector3.one * 0.02f;

            System.IO.Directory.CreateDirectory("Assets/Prefabs");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static void BuildUI(GameManager gm)
        {
            var canvasGo = new GameObject("UI Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            gm.ScoreText = MakeText(canvasGo.transform, "ScoreText", font,
                new Vector2(20, -20), TextAnchor.UpperLeft, "Score: 0");
            gm.LivesText = MakeText(canvasGo.transform, "LivesText", font,
                new Vector2(20, -50), TextAnchor.UpperLeft, "Lives: 5");
            gm.StatusText = MakeText(canvasGo.transform, "StatusText", font,
                new Vector2(20, -80), TextAnchor.UpperLeft, "PLAYING  (R: Restart)");
        }

        private static Text MakeText(Transform parent, string name, Font font,
            Vector2 anchoredPos, TextAnchor anchor, string content)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = 22;
            text.color = Color.white;
            text.alignment = anchor;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var rt = text.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(400f, 30f);
            rt.anchoredPosition = anchoredPos;
            return text;
        }
    }
}
