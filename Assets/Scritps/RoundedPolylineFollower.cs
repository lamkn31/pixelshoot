using UnityEngine;

namespace Wayfu.Lamkn
{
public class RoundedPolylineFollower : MonoBehaviour
{
    [Header("References")]
    public RoundedPolylinePath targetPath;

    [Header("Movement Settings")]
    public float moveSpeed = 3.0f; // Tốc độ di chuyển thực tế ổn định (mét/giây)

    private float currentDistance = 0f; // Khoảng cách vật thể đã đi được trên đường

    private void Start()
    {
        if (targetPath == null)
        {
            targetPath = FindFirstObjectByType<RoundedPolylinePath>();
        }

        if (targetPath == null)
        {
            Debug.LogError("Vui lòng gán RoundedPolylinePath hợp lệ!");
            enabled = false;
            return;
        }

        // Đặt vị trí ban đầu
        transform.position = targetPath.GetPointAtDistance(currentDistance);
    }

    private void Update()
    {
        if (targetPath == null) return;

        // Tăng khoảng cách dựa trên vận tốc m/s thực tế nhân với delta time
        currentDistance += moveSpeed * Time.deltaTime;

        // Cập nhật tọa độ mới bằng hàm nội suy nhị phân
        Vector3 newPosition = targetPath.GetPointAtDistance(currentDistance);
        transform.position = newPosition;

        // Tính hướng đi tiếp theo (nhìn trước 0.05 mét) để xoay mặt gạch/lỗ theo đường ray
        Vector3 lookAheadPos = targetPath.GetPointAtDistance(currentDistance + 0.05f);
        Vector3 direction = (lookAheadPos - transform.position).normalized;

        if (direction != Vector3.zero)
        {
            // Thiết lập góc quay phù hợp cho không gian 2D (Trục Z phẳng)
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
    }
}
}