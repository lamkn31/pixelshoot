using UnityEngine;
using Spine.Unity;
using System.Collections;

/// <summary>
/// Điều khiển bàn tay Spine skeleton: vị trí + animation
/// Gắn vào GameObject chứa SkeletonGraphic (UI) hoặc SkeletonAnimation (World)
/// </summary>
public class TutorialHandController : MonoBehaviour
{
    // ── Spine references ──────────────────────────────────────────────
    [Header("Spine Component (chọn 1 trong 2)")]
    [SerializeField] private SkeletonGraphic skeletonGraphic;   // Dùng cho UI (Canvas)
    [SerializeField] private SkeletonAnimation skeletonAnimation; // Dùng cho World Space

    [Header("Animation Track")]
    [SerializeField] private int trackIndex = 0;

    [Header("Transition")]
    [SerializeField] private float fadeDuration = 0.25f;
    [SerializeField] private float moveDuration = 0.2f;

    [Header("Fingertip Offset")]
    [Tooltip("Offset (pixel) cộng vào target position để đầu ngón tay nằm đúng vị trí. " +
             "Spine bàn tay vẽ đầu ngón ở góc trên-trái nên cần shift xuống-phải để bù.")]
    [SerializeField] private Vector2 fingerOffset = new Vector2(60f, -60f);

    // ── Private state ─────────────────────────────────────────────────
    private Spine.AnimationState SpineAnimState =>
        skeletonGraphic != null ? skeletonGraphic.AnimationState : skeletonAnimation?.AnimationState;

    private Coroutine _moveCoroutine;
    private CanvasGroup _canvasGroup;
    private Canvas _canvas;

    /// <summary>Camera để chuyển screen-point → điểm trên mặt phẳng canvas. Phải đọc từ
    /// ROOT canvas: canvas gần nhất có thể là nested canvas báo render mode/camera sai → truyền
    /// nhầm null vào ScreenPointToLocalPointInRectangle khiến anchoredPosition nổ rất lớn.
    /// Overlay → null; Screen Space - Camera / World → worldCamera của root canvas.</summary>
    private Camera UICamera
    {
        get
        {
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            if (_canvas == null) return null;
            var root = _canvas.rootCanvas != null ? _canvas.rootCanvas : _canvas;
            return root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null && skeletonGraphic != null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        Hide(instant: true);
    }

    // ─────────────────────────────────────────────────────────────────
    /// <summary>Hiển thị bàn tay tại vị trí và play animation chỉ định</summary>
    public void Show(Vector3 position, string animationName, bool loop = true)
    {
        gameObject.SetActive(true);
        MoveTo(position, instant: true);
        PlayAnimation(animationName, loop);
        FadeIn();
    }

    /// <summary>Di chuyển đến vị trí mới (có tweening). Offset đầu ngón tay được áp dụng tự động.</summary>
    public void MoveTo(Vector3 position, bool instant = false)
    {
        MoveToWorld(position + (Vector3)fingerOffset, instant);
    }

    /// <summary>
    /// Hiển thị bàn tay tại MỘT TOẠ ĐỘ MÀN HÌNH (pixel). Tự quy đổi sang mặt phẳng canvas
    /// theo render mode (Overlay / Screen Space - Camera / World), nên bàn tay luôn nằm đúng
    /// chỗ con trỏ chỉ tới bất kể canvas dùng camera hay không.
    /// </summary>
    public void ShowAtScreen(Vector2 screenPoint, string animationName, bool loop = true)
    {
        gameObject.SetActive(true);
        MoveToScreen(screenPoint, instant: true);
        PlayAnimation(animationName, loop);
        FadeIn();
    }

    /// <summary>Di chuyển bàn tay tới một toạ độ màn hình (pixel). Quy đổi screen → WORLD point
    /// trên mặt phẳng của parent rect rồi set transform.position. Cách này KHÔNG phụ thuộc
    /// pivot/anchor/scale của rect (khác với anchoredPosition), và dùng camera lấy từ root canvas
    /// nên đúng cho mọi render mode. fingerOffset cộng ở không gian pixel màn hình trước khi quy đổi.</summary>
    public void MoveToScreen(Vector2 screenPoint, bool instant = false)
    {
        Vector2 sp = screenPoint + fingerOffset;
        var self = transform as RectTransform;
        var plane = self != null ? (self.parent as RectTransform) ?? self : null;
        if (plane != null &&
            RectTransformUtility.ScreenPointToWorldPointInRectangle(plane, sp, UICamera, out var world))
        {
            MoveToWorld(world, instant);
            return;
        }

        // Non-UI (SkeletonAnimation in world space) hoặc quy đổi thất bại: set world thẳng.
        MoveToWorld(new Vector3(sp.x, sp.y, 0f), instant);
    }

    /// <summary>Raw mover — đặt transform tới world position đã tính sẵn, không cộng thêm offset.</summary>
    private void MoveToWorld(Vector3 worldPos, bool instant)
    {
        if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);

        if (instant)
            transform.position = worldPos;
        else
            _moveCoroutine = StartCoroutine(MoveRoutine(worldPos));
    }

    /// <summary>Đổi animation (ví dụ từ idle sang click)</summary>
    public void PlayAnimation(string animationName, bool loop = true)
    {
        if (string.IsNullOrEmpty(animationName)) return;

        var anim = SpineAnimState;
        if (anim == null) return;

        // Kiểm tra animation có tồn tại không
        var skelData = skeletonGraphic != null
            ? skeletonGraphic.SkeletonData
            : skeletonAnimation?.Skeleton.Data;

        if (skelData?.FindAnimation(animationName) == null)
        {
            Debug.LogWarning($"[TutorialHand] Animation '{animationName}' không tìm thấy trong skeleton!");
            return;
        }

        anim.SetAnimation(trackIndex, animationName, loop);
    }

    /// <summary>Ẩn bàn tay với fade out</summary>
    public void Hide(bool instant = false)
    {
        // Không thể chạy coroutine khi GameObject đang inactive → ẩn tức thì luôn.
        if (instant || !gameObject.activeInHierarchy)
        {
            SetAlpha(0f);
            gameObject.SetActive(false);
        }
        else
        {
            StartCoroutine(FadeOutAndDisable());
        }
    }

    // ── Internal helpers ──────────────────────────────────────────────
    private void FadeIn()
    {
        if (_canvasGroup == null) return;
        StopCoroutine(nameof(FadeRoutine));
        StartCoroutine(FadeRoutine(0f, 1f, fadeDuration));
    }

    private IEnumerator FadeOutAndDisable()
    {
        if (_canvasGroup != null)
            yield return FadeRoutine(1f, 0f, fadeDuration);

        gameObject.SetActive(false);
    }

    private IEnumerator FadeRoutine(float from, float to, float duration)
    {
        if (_canvasGroup == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        _canvasGroup.alpha = to;
    }

    private IEnumerator MoveRoutine(Vector3 target)
    {
        Vector3 start = transform.position;
        float elapsed = 0f;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(start, target, elapsed / moveDuration);
            yield return null;
        }
        transform.position = target;
    }

    private void SetAlpha(float alpha)
    {
        if (_canvasGroup != null) _canvasGroup.alpha = alpha;
    }
}
