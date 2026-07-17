using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using AerialFlickGame.Circle;
using AerialFlickGame.Presentation;

namespace AerialFlickGame.EditorTools
{
    /// <summary>
    /// 現在のシーンに、左側で球を投げるペンギンを配置・配線する（シーン再生成なし）。
    /// </summary>
    public static class PenguinSetup
    {
        private const string PenguinPrefabPath =
            "Assets/Hosh/Stylized Penguin (Free)/Prefabs/URP/Penguen_Emperor_Mesh_Lite.prefab";
        private const string ControllerPath =
            "Assets/Hosh/Stylized Penguin (Free)/Art/Animations/Penguen_Emperor_Lite.controller";
        private const string FbxPath =
            "Assets/Hosh/Stylized Penguin (Free)/Art/Models/Penguen_Emperor_Mesh_Lite.fbx";

        [MenuItem("AerialFlickGame/Add Penguin Thrower (left)")]
        public static void AddPenguin()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PenguinPrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Penguin not found",
                    "ペンギンのプレハブが見つかりません:\n" + PenguinPrefabPath, "OK");
                return;
            }

            var spawner = Object.FindFirstObjectByType<CircleSpawner>();

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = "Penguin (Thrower)";

            // 位置: スポーン位置の少し左、判定面のわずかに奥（円が手前を通る）。
            // Y は球の流れ(±YRange)より下＝地面。ここから球の高さまでジャンプする。
            float x = spawner != null ? spawner.SpawnX - 0.03f : -0.33f;
            float z = (spawner != null ? spawner.PlaneZ : 0f) + 0.05f;
            float groundY = spawner != null ? -(spawner.YRange + 0.12f) : -0.20f;
            go.transform.position = new Vector3(x, groundY, z);

            // 右(+X)を向く。逆を向いたら Y を -90 に。
            go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            // シーンは m 単位（表示は縦 ±0.3m 程度）。既定はかなり縮小。要調整。
            go.transform.localScale = Vector3.one * 0.15f;

            // プレハブには Animator が無いので追加し、コントローラと Avatar を割り当てる
            var animator = go.GetComponentInChildren<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (controller != null) animator.runtimeAnimatorController = controller;
            else Debug.LogWarning("[PenguinSetup] コントローラが見つかりません: " + ControllerPath);

            // FBX 内の Avatar を割り当て（generic アニメの再生に必要な場合がある）
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
            {
                if (a is Avatar av) { animator.avatar = av; break; }
            }

            // 色を変えられるように tint コンポーネントを付ける（既定は白＝元の見た目）
            if (go.GetComponent<PenguinTint>() == null) go.AddComponent<PenguinTint>();

            var thrower = go.GetComponent<PenguinThrower>();
            if (thrower == null) thrower = go.AddComponent<PenguinThrower>();

            // スポナー → ペンギン を配線（球のリリース同期）
            if (spawner != null) spawner.Thrower = thrower;

            Undo.RegisterCreatedObjectUndo(go, "Add Penguin Thrower");
            EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeGameObject = go;

            Debug.Log("[PenguinSetup] ペンギンを配置しました。位置(Y)・スケール・向き(Y回転)は " +
                      "見た目に合わせて Inspector で調整してください。" +
                      (spawner == null ? "\n※ CircleSpawner が見つからず未配線です。手動で Spawner を設定してください。" : ""));
        }

        [MenuItem("AerialFlickGame/Recolor Penguin (blue body)")]
        public static void RecolorBlue()
        {
            const string bluePalette =
                "Assets/Hosh/Stylized Penguin (Free)/Art/Textures/HoshPalette_lite_blue.png";

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(bluePalette);
            if (tex == null)
            {
                EditorUtility.DisplayDialog("Palette not found",
                    "青パレットが見つかりません:\n" + bluePalette, "OK");
                return;
            }

            // シーン内のペンギン（PenguinTint / PenguinThrower）を探す
            var tint = Object.FindFirstObjectByType<PenguinTint>();
            if (tint == null)
            {
                var thrower = Object.FindFirstObjectByType<PenguinThrower>();
                if (thrower != null) tint = thrower.gameObject.AddComponent<PenguinTint>();
            }
            if (tint == null)
            {
                EditorUtility.DisplayDialog("Penguin not found",
                    "シーンにペンギン（PenguinThrower/PenguinTint）が見つかりません。\n" +
                    "先に Add Penguin Thrower を実行してください。", "OK");
                return;
            }

            Undo.RecordObject(tint, "Recolor Penguin");
            tint.PaletteOverride = tex;
            tint.Color = Color.white; // tint は白（パレットで色付け済みのため）
            tint.Apply();
            EditorUtility.SetDirty(tint);
            Selection.activeGameObject = tint.gameObject;

            Debug.Log("[PenguinSetup] 体を青くしたパレットを適用しました（くちばし/足は据え置き）。");
        }
    }
}
