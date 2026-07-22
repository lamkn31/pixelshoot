using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Vẽ đường nối CONNECT giữa 2 gun bằng 1 LineRenderer: cung Bezier vắt LÊN (arcHeight), 2 đầu VUỐT NHỌN
    /// (widthCurve về 0 để cắm vào gun), màu chia 2 KHỐI rõ ràng (gradient ranh giới cứng ở giữa nhờ nhiều
    /// điểm + key 0.499/0.501). Tuỳ chọn outline phía sau. Tự bám theo 2 target mỗi frame.
    /// (Tham khảo YarnSort/ConnectLine.)
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class ConnectLine : MonoBehaviour
    {
        [Tooltip("Độ cao cung vắt lên (từ vị trí gun).")]
        public float arcHeight = 1.5f;
        [Tooltip("Độ rộng tối đa đoạn giữa dây.")]
        public float lineWidth = 0.15f;
        [Range(0.01f, 0.3f)]
        [Tooltip("Tỷ lệ chiều dài vuốt nhọn mỗi đầu (0.15 = 15% đầu và cuối thu về 0).")]
        public float taperPercentage = 0.15f;
        [Min(2)]
        [Tooltip("Số điểm của dây — nhiều thì cung mượt và ranh giới màu sắc nét.")]
        public int resolution = 25;

        [Header("Outline")]
        public bool useOutline = true;
        public float outlineExtraWidth = 0.08f;
        public Color outlineColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        [Tooltip("Material viền. Trống = copy từ dây chính.")]
        public Material outlineMaterial;

        private LineRenderer _lr, _outline;
        private Transform _a, _b;

        private void Awake()
        {
            _lr = GetComponent<LineRenderer>();
            _lr.useWorldSpace = true;
            _lr.positionCount = resolution;
            SetupWidthCurve();
            if (useOutline) SetupOutline();
        }

        public void SetTargets(Transform a, Transform b) { _a = a; _b = b; }

        /// <summary>Đặt 2 nửa màu (ranh giới cứng ở giữa). colorA = phía target A, colorB = phía target B.</summary>
        public void SetColors(Color colorA, Color colorB)
        {
            if (_lr == null) _lr = GetComponent<LineRenderer>();
            var grad = new Gradient();
            grad.SetKeys(
                new[] {
                    new GradientColorKey(colorA, 0f), new GradientColorKey(colorA, 0.499f),
                    new GradientColorKey(colorB, 0.501f), new GradientColorKey(colorB, 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            _lr.colorGradient = grad;
            // Material tô trắng để vertex color của gradient hiện đúng.
            if (_lr.material != null)
            {
                if (_lr.material.HasProperty("_Color")) _lr.material.color = Color.white;
                if (_lr.material.HasProperty("_BaseColor")) _lr.material.SetColor("_BaseColor", Color.white);
            }
        }

        private void SetupWidthCurve()
        {
            var curve = new AnimationCurve();
            curve.AddKey(new Keyframe(0f, 0f));
            curve.AddKey(new Keyframe(taperPercentage, 1f));
            curve.AddKey(new Keyframe(1f - taperPercentage, 1f));
            curve.AddKey(new Keyframe(1f, 0f));
            _lr.widthCurve = curve;
            _lr.widthMultiplier = lineWidth;
        }

        private void SetupOutline()
        {
            var go = new GameObject("Outline");
            go.transform.SetParent(transform, false);
            _outline = go.AddComponent<LineRenderer>();
            _outline.useWorldSpace = true;
            _outline.positionCount = resolution;
            _outline.widthCurve = _lr.widthCurve;
            _outline.widthMultiplier = lineWidth + outlineExtraWidth;
            _outline.material = outlineMaterial != null ? outlineMaterial
                : (_lr.material != null ? new Material(_lr.material) : null);
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(outlineColor, 0f), new GradientColorKey(outlineColor, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            _outline.colorGradient = grad;
            _outline.sortingOrder = _lr.sortingOrder - 1; // render sau dây chính
        }

        private void Update()
        {
            if (_a == null || _b == null) return;
            DrawArc(_a.position, _b.position);
        }

        private void DrawArc(Vector3 pA, Vector3 pB)
        {
            Vector3 cA = pA + Vector3.up * arcHeight;
            Vector3 cB = pB + Vector3.up * arcHeight;
            for (int i = 0; i < resolution; i++)
            {
                float t = i / (float)(resolution - 1);
                Vector3 pos = Bezier(t, pA, cA, cB, pB);
                _lr.SetPosition(i, pos);
                if (_outline != null) _outline.SetPosition(i, pos - Vector3.up * 0.02f);
            }
        }

        // Cubic Bezier 4 điểm.
        private static Vector3 Bezier(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            float u = 1 - t, tt = t * t, uu = u * u, uuu = uu * u, ttt = tt * t;
            return uuu * p0 + 3f * uu * t * p1 + 3f * u * tt * p2 + ttt * p3;
        }
    }
}
