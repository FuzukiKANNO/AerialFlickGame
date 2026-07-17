using UnityEngine;

namespace AerialFlickGame.Presentation
{
    /// <summary>
    /// ペンギン（グレースケールのパレットテクスチャ）に色を掛けて染める。
    /// MaterialPropertyBlock で _BaseColor を上書きするので、共有マテリアルは変更しない（非破壊）。
    /// Inspector で色を変えるとエディタ上でも即プレビューされる。
    /// </summary>
    [ExecuteAlways]
    public class PenguinTint : MonoBehaviour
    {
        [Tooltip("掛ける色。白=元の見た目のまま。グレースケールなのでこの色の濃淡に染まる")]
        public Color Color = Color.white;

        [Tooltip("設定すると _BaseMap をこのパレットに差し替える（体だけ青くした版など）。共有マテリアルは非破壊")]
        public Texture2D PaletteOverride;

        [Tooltip("対象レンダラ（空なら子から自動取得）")]
        public Renderer[] Renderers;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private MaterialPropertyBlock _mpb;

        private void Awake() => Apply();
        private void OnEnable() => Apply();
        private void OnValidate() => Apply();

        public void Apply()
        {
            var rends = (Renderers != null && Renderers.Length > 0)
                ? Renderers
                : GetComponentsInChildren<Renderer>(true);
            if (rends == null || rends.Length == 0) return;

            _mpb ??= new MaterialPropertyBlock();
            foreach (var r in rends)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, Color); // URP Lit
                _mpb.SetColor(ColorId, Color);     // Built-in 系の保険
                if (PaletteOverride != null)
                {
                    _mpb.SetTexture(BaseMapId, PaletteOverride);
                    _mpb.SetTexture(MainTexId, PaletteOverride);
                }
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
