using UnityEngine;
using AerialFlickGame.TrackedObjects;

namespace AerialFlickGame.Presentation
{
    /// <summary>
    /// RingTracked の円中心・半径に合わせて、LineRenderer で円（リング）を描く見た目。
    /// エディタ上でもプレビューできるよう ExecuteAlways。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(LineRenderer))]
    public class RingVisual : MonoBehaviour
    {
        [Tooltip("描画対象のリング。null なら同じ GameObject から取得")]
        public RingTracked Ring;

        [Range(8, 128)] public int Segments = 48;
        [Tooltip("線の太さ [m]")]
        public float LineWidth = 0.004f;
        public Color Color = Color.white;

        private LineRenderer _lr;

        private void OnEnable()
        {
            if (Ring == null) Ring = GetComponent<RingTracked>();
            _lr = GetComponent<LineRenderer>();
            SetupLine();
            _lr.enabled = true;
        }

        private void OnDisable()
        {
            if (_lr != null) _lr.enabled = false;
        }

        private void SetupLine()
        {
            _lr.useWorldSpace = true;
            _lr.loop = true;
            _lr.widthMultiplier = LineWidth;
            _lr.numCapVertices = 2;
            _lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _lr.receiveShadows = false;
            if (_lr.sharedMaterial == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                _lr.sharedMaterial = new Material(sh) { name = "RingVisualMat" };
            }
            ApplyColor();
        }

        private void ApplyColor()
        {
            _lr.startColor = _lr.endColor = Color;
            var m = _lr.sharedMaterial;
            if (m != null)
            {
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color);
                m.color = Color;
            }
        }

        private void LateUpdate()
        {
            if (Ring == null || _lr == null) return;
            _lr.widthMultiplier = LineWidth;
            _lr.positionCount = Segments;
            ApplyColor();

            Vector3 c = Ring.CenterWorld;
            float r = Ring.Radius;
            Ring.GetPlaneBasis(out Vector3 u, out Vector3 v); // 円が乗る平面（既定 ZY）
            for (int i = 0; i < Segments; i++)
            {
                float a = (i / (float)Segments) * Mathf.PI * 2f;
                _lr.SetPosition(i, c + (Mathf.Cos(a) * u + Mathf.Sin(a) * v) * r);
            }
        }
    }
}
