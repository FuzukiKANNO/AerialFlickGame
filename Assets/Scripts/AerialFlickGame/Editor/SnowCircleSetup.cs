using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using AerialFlickGame.Circle;

namespace AerialFlickGame.EditorTools
{
    /// <summary>
    /// スノーのテクスチャから URP マテリアルを作り、飛来球（FlyingCircle プレハブ / Spawner）に割り当てる。
    /// Built-in シェーダのままだと URP でピンクになるので URP/Lit で作り直す。
    /// </summary>
    public static class SnowCircleSetup
    {
        private const string TexDir = "Assets/Stylize Snow Texture/Textures/";
        private const string BasePath = TexDir + "Vol_22_4_Base_Color.png";
        private const string NormalPath = TexDir + "Vol_22_4_Normal.png";
        private const string PrefabPath = "Assets/Prefabs/FlyingCircle.prefab";
        private const string MatPath = "Assets/Materials/SnowCircleMat.mat";

        [MenuItem("AerialFlickGame/Apply Snow Material to Circle")]
        public static void Apply()
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) { EditorUtility.DisplayDialog("URP not found", "URP/Lit シェーダが見つかりません。", "OK"); return; }

            var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(BasePath);
            var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
            if (baseTex == null)
            {
                EditorUtility.DisplayDialog("Texture not found", "Base Color が見つかりません:\n" + BasePath, "OK");
                return;
            }

            // 法線テクスチャのインポート種別を NormalMap に
            if (normalTex != null)
            {
                string np = AssetDatabase.GetAssetPath(normalTex);
                var imp = AssetImporter.GetAtPath(np) as TextureImporter;
                if (imp != null && imp.textureType != TextureImporterType.NormalMap)
                {
                    imp.textureType = TextureImporterType.NormalMap;
                    imp.SaveAndReimport();
                }
            }

            // URP/Lit マテリアル作成
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null) { mat = new Material(sh) { name = "SnowCircleMat" }; AssetDatabase.CreateAsset(mat, MatPath); }
            mat.shader = sh;
            mat.SetTexture("_BaseMap", baseTex);
            mat.SetColor("_BaseColor", Color.white);
            if (normalTex != null)
            {
                mat.SetTexture("_BumpMap", normalTex);
                mat.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(mat);

            // FlyingCircle プレハブへ割り当て
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null)
            {
                var rend = prefab.GetComponentInChildren<Renderer>();
                if (rend != null) { rend.sharedMaterial = mat; EditorUtility.SetDirty(prefab); }
            }

            // シーンの Spawner にも設定（プリミティブ生成時の保険）
            var spawner = Object.FindFirstObjectByType<CircleSpawner>(FindObjectsInactive.Include);
            if (spawner != null)
            {
                Undo.RecordObject(spawner, "Snow material");
                spawner.CircleMaterial = mat;
                EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[SnowCircleSetup] URP スノーマテリアルを作成し、飛来球に割り当てました: " + MatPath);
        }
    }
}
