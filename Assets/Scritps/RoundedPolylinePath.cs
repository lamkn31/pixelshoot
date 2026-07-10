using UnityEngine;
using System.Collections.Generic;

namespace Wayfu.Lamkn
{
public class RoundedPolylinePath : MonoBehaviour
{
    [Header("Path Settings")]
    public List<Transform> waypoints = new List<Transform>();
    public bool isClosed = true;
    public float cornerRadius = 1.0f;

    [Header("Resolution Settings")]
    [Tooltip("Số điểm tạo thêm tại mỗi góc cua để làm mượt")]
    public int curveSamples = 8;

    [HideInInspector] public Vector3[] samples;
    [HideInInspector] public float[] sampleArc;
    public float TotalLength { get; private set; }

    private void Awake()
    {
        GeneratePath();
    }

    // Hàm khởi tạo và tính toán toàn bộ đường đi (Port từ logic của bạn)
    public void GeneratePath()
    {
        if (waypoints == null || waypoints.Count < 2) return;

        int n = waypoints.Count;
        Vector3[] wpPositions = new Vector3[n];
        for (int i = 0; i < n; i++) wpPositions[i] = waypoints[i].position;

        var pts = new List<Vector3>(n * (curveSamples + 2));

        int cornerStart = isClosed ? 0 : 1;
        int cornerEnd = isClosed ? n : n - 1;
        if (!isClosed) pts.Add(wpPositions[0]);

        for (int i = cornerStart; i < cornerEnd; i++)
        {
            Vector3 prev = wpPositions[(i - 1 + n) % n];
            Vector3 cur = wpPositions[i];
            Vector3 next = wpPositions[(i + 1) % n];

            Vector3 inDir = cur - prev;
            Vector3 outDir = next - cur;
            float inLen = inDir.magnitude;
            float outLen = outDir.magnitude;
            if (inLen < 1e-5f || outLen < 1e-5f) { pts.Add(cur); continue; }

            Vector3 inN = inDir / inLen;
            Vector3 outN = outDir / outLen;
            if (Vector3.Cross(inN, outN).sqrMagnitude < 1e-6f) { pts.Add(cur); continue; }

            float r = Mathf.Min(cornerRadius, 0.5f * Mathf.Min(inLen, outLen));
            Vector3 a = cur - inN * r; // Bắt đầu đoạn cong
            Vector3 b = cur + outN * r; // Kết thúc đoạn cong
            pts.Add(a);

            float cosA = Mathf.Clamp(Vector3.Dot(inN, outN), -1f, 1f);
            float alpha = Mathf.Acos(cosA);
            float tanHalf = Mathf.Tan(alpha * 0.5f);
            Vector3 bis = outN - inN;
            bool arcOk = false;

            if (bis.sqrMagnitude > 1e-10f && tanHalf > 1e-4f)
            {
                bis.Normalize();
                float R = r / tanHalf;
                Vector3 O = cur + bis * Mathf.Sqrt(r * r + R * R); // Tâm cung tròn
                Vector3 va = a - O;
                Vector3 vb = b - O;
                Vector3 axis = Vector3.Cross(va, vb);

                if (axis.sqrMagnitude > 1e-12f)
                {
                    axis.Normalize();
                    float sweepDeg = Vector3.Angle(va, vb);
                    for (int k = 1; k <= curveSamples; k++)
                    {
                        float ang = sweepDeg * (k / (float)(curveSamples + 1));
                        pts.Add(O + Quaternion.AngleAxis(ang, axis) * va);
                    }
                    arcOk = true;
                }
            }

            if (!arcOk)
            {
                // Fallback Bezier bậc 2 nếu cung tròn bị lỗi
                for (int k = 1; k <= curveSamples; k++)
                {
                    float t = k / (float)(curveSamples + 1);
                    float u = 1f - t;
                    pts.Add(u * u * a + 2f * u * t * cur + t * t * b);
                }
            }
            pts.Add(b);
        }

        if (isClosed) pts.Add(pts[0]); else pts.Add(wpPositions[n - 1]);

        samples = pts.ToArray();
        sampleArc = new float[samples.Length];
        float acc = 0f;
        for (int i = 1; i < samples.Length; i++)
        {
            acc += Vector3.Distance(samples[i - 1], samples[i]);
            sampleArc[i] = acc;
        }
        TotalLength = acc;
    }

    // Hàm lấy vị trí dựa trên khoảng cách tuyệt đối (distance) chạy trên đường thay vì dùng biến t (0->1) chung chung
    public Vector3 GetPointAtDistance(float distance)
    {
        if (samples == null || samples.Length == 0) GeneratePath();
        if (samples == null || samples.Length == 0) return Vector3.zero;

        // Xử lý Loop khoảng cách
        if (isClosed)
        {
            distance = Mathf.Repeat(distance, TotalLength);
        }
        else
        {
            distance = Mathf.Clamp(distance, 0f, TotalLength);
        }

        // Tìm kiếm nhị phân (Binary Search) trên mảng _sampleArc để tìm phân đoạn chứa khoảng cách này
        int low = 0;
        int high = sampleArc.Length - 1;
        while (low < high - 1)
        {
            int mid = (low + high) / 2;
            if (sampleArc[mid] <= distance) low = mid;
            else high = mid;
        }

        // Nội suy tuyến tính chính xác giữa 2 điểm mẫu kế cận
        float d0 = sampleArc[low];
        float d1 = sampleArc[high];
        if (Mathf.Approximately(d0, d1)) return samples[low];

        float t = (distance - d0) / (d1 - d0);
        return Vector3.Lerp(samples[low], samples[high], t);
    }

    private void OnDrawGizmos()
    {
        // Tự động lấy các con làm waypoint trong Editor để dễ thao tác
        if (waypoints.Count == 0 && transform.childCount > 0)
        {
            foreach (Transform child in transform) waypoints.Add(child);
        }

        GeneratePath();

        if (samples == null || samples.Length < 2) return;

        // Vẽ đường bo góc (Màu xanh lá cây đặc trưng của đường thẳng bo cua)
        Gizmos.color = Color.green;
        for (int i = 1; i < samples.Length; i++)
        {
            Gizmos.DrawLine(samples[i - 1], samples[i]);
        }

        // Vẽ các đỉnh thô ban đầu để trực quan hóa
        Gizmos.color = Color.red;
        foreach (var wp in waypoints)
        {
            if (wp != null) Gizmos.DrawWireSphere(wp.position, 0.1f);
        }
    }
}
}