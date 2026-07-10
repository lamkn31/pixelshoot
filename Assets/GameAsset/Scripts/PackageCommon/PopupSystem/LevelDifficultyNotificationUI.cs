using Spine.Unity; // Thư viện Spine
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wayfu.Lamkn;

/// <summary>
/// Quản lý thông báo độ khó (Hard/Very Hard) khi bắt đầu màn chơi.
/// </summary>
public class LevelDifficultyNotificationUI : BasePopup
{
    [Header("UI References")]
    [Tooltip("GameObject cha chứa toàn bộ thông báo (để bật/tắt)")]
    public GameObject notificationPanel;

    [Tooltip("CanvasGroup để điều khiển Fade In/Out")]
    public CanvasGroup panelCanvasGroup;

    [Tooltip("Ảnh RawImage cho thanh cảnh báo chạy (Scrolling Bar)")]
    public RawImage warningBarImage;

    [Tooltip("Sprite BG cho thanh cảnh báo khi HARD")]
    public Sprite hardBarSprite;

    [Tooltip("Sprite BG cho thanh cảnh báo khi VERY HARD")]
    public Sprite veryHardBarSprite;

    [Header("Spine Components")]
    [Tooltip("SkeletonGraphic icon đầu lâu cho HARD")]
    public SkeletonGraphic iconHardSkeleton;

    [Tooltip("SkeletonGraphic icon đầu lâu cho VERY HARD")]
    public SkeletonGraphic iconVeryHardSkeleton;

    [Tooltip("SkeletonGraphic của chữ thông báo (Text)")]
    public SkeletonGraphic textSkeleton;
    [SerializeField] private Transform posHardText;
    [SerializeField] private Transform posVeryHardText;
    [SerializeField] private GameObject textDesLevelHard;
    [SerializeField] private GameObject textDesLevelVeryHard;

    [Header("Spine Data Assets")]
    [Tooltip("SkeletonDataAsset cho chữ 'HARD'")]
    public SkeletonDataAsset hardTextData;

    [Tooltip("SkeletonDataAsset cho chữ 'VERY HARD'")]
    public SkeletonDataAsset veryHardTextData;

    [Header("Icon Animation Names")]
    [SpineAnimation] public string iconAnimStart = "start"; // Tên anim xuất hiện cho Icon
    [SpineAnimation] public string iconAnimIdle = "idle";   // Tên anim lặp lại cho Icon

    [Header("Text Animation Names")]
    [SpineAnimation("","hardTextData")] public string textAnimStart = "start"; // Tên anim xuất hiện cho Text
    [SpineAnimation("","hardTextData")] public string textAnimIdle = "idle";   // Tên anim lặp lại cho Text

    [Header("Settings")]
    [Tooltip("Tốc độ cuộn của thanh cảnh báo theo trục X")]
    public float scrollSpeed = 0.5f;

    [Tooltip("Thời gian hiển thị thông báo trước khi tự tắt")]
    public float displayDuration = 3.0f;

    [Header("Effect Settings")]
    [Tooltip("Thời gian thu nhỏ các phần tử")]
    public float scaleOutDuration = 0.4f;
    [Tooltip("Thời gian mờ dần (bắt đầu từ giữa lúc thu nhỏ)")]
    public float iconScaleDuration = 0.4f;
    public float textScaleDuration = 0.4f;

    private Coroutine _hideCoroutine;
    private Coroutine _showSequenceCoroutine;
    private Coroutine _showDesLevelHard;
    // [THÊM MỚI] Cờ báo hiệu UI đang hiển thị (che IsShowing của BasePopup vì lifecycle hiển thị do panel con quản lý)
    public new bool IsShowing { get; private set; } = false;

    // Icon đang dùng cho lần Show hiện tại (Hard / VeryHard)
    private SkeletonGraphic _activeIcon;

    // Object des (textDesLevelHard / textDesLevelVeryHard) đang hiện — để scale-out cùng chuỗi tắt.
    private Transform _activeDes;
    private Coroutine _desScaleCoroutine;


    private SkeletonGraphic GetIconFor(LevelDifficulty difficulty)
    {
        if (difficulty == LevelDifficulty.VeryHard) return iconVeryHardSkeleton;
        if (difficulty == LevelDifficulty.Hard) return iconHardSkeleton;
        return null;
    }
    protected override void Awake()
    {
        // KHÔNG gọi base.Awake() vì BasePopup sẽ set CanvasGroup root về alpha=0,
        // trong khi popup này quản lý hiển thị qua notificationPanel + panelCanvasGroup riêng.
        EnsureInitialized();
        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;

        // Ẩn lúc đầu
        HideImmediate();

        // Tự động tìm CanvasGroup nếu chưa gán
        if (panelCanvasGroup == null && notificationPanel != null)
        {
            panelCanvasGroup = notificationPanel.GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null) panelCanvasGroup = notificationPanel.AddComponent<CanvasGroup>();
        }
    }

    private void Update()
    {
        // Logic cuộn UV cho thanh cảnh báo (chỉ chạy khi panel đang bật)
        if (notificationPanel.activeSelf && warningBarImage != null)
        {
            Rect uvRect = warningBarImage.uvRect;
            uvRect.x += scrollSpeed * Time.deltaTime;
            warningBarImage.uvRect = uvRect;
        }
    }
    private void HideDesLevelHard()
    {
        if (_desScaleCoroutine != null) { StopCoroutine(_desScaleCoroutine); _desScaleCoroutine = null; }
        _activeDes = null;
        if (textDesLevelHard != null)
        {
            textDesLevelHard.transform.localScale = Vector3.one; // trả scale gốc cho lần show sau
            textDesLevelHard.SetActive(false);
        }
        if (textDesLevelVeryHard != null)
        {
            textDesLevelVeryHard.transform.localScale = Vector3.one;
            textDesLevelVeryHard.SetActive(false);
        }
    }

    public void Show(LevelDifficulty difficulty)
    {
        HideDesLevelHard();
        // Chỉ hiện nếu là Hard hoặc Very Hard
        if (difficulty == LevelDifficulty.Easy) return;
        // Active panel TRƯỚC khi reset / start coroutine - tránh lỗi
        // "Coroutine couldn't be started because the game object is inactive"
        if (notificationPanel != null) notificationPanel.SetActive(true);

        // Nếu parent chain vẫn chưa active (panel SetActive(true) nhưng activeInHierarchy=false)
        // thì không thể StartCoroutine - bỏ qua kèm cảnh báo để dev biết.

        // Reset trạng thái trước khi show
        HideImmediate();
        IsShowing = true;

        if (notificationPanel != null) notificationPanel.SetActive(true);

        // 1. Setup Text Spine (Thay đổi SkeletonData)
        if (textSkeleton != null)
        {
            if (difficulty == LevelDifficulty.Hard)
            {
                if (hardTextData != null)
                {
                    if(posHardText != null)
                        textSkeleton.transform.position = posHardText.position; // Đặt vị trí cho chữ Hard
                    textSkeleton.skeletonDataAsset = hardTextData;
                }
            }
            else if (difficulty == LevelDifficulty.VeryHard)
            {
                if (veryHardTextData != null)
                {
                    if(posVeryHardText != null)
                        textSkeleton.transform.position = posVeryHardText.position; // Đặt vị trí cho chữ Very Hard
                    textSkeleton.skeletonDataAsset = veryHardTextData;
                }
            }

            // Quan trọng: Phải khởi tạo lại Skeleton sau khi thay đổi DataAsset
            textSkeleton.Initialize(true);
            
        }

        // 1b. Setup Sprite BG cho thanh cảnh báo theo difficulty
        if (warningBarImage != null)
        {
            Sprite barSprite = (difficulty == LevelDifficulty.VeryHard) ? veryHardBarSprite : hardBarSprite;
            if (barSprite != null) warningBarImage.texture = barSprite.texture;
        }

        // 2. Setup Icon Spine - chọn đúng icon theo difficulty, ẩn icon kia
        _activeIcon = GetIconFor(difficulty);
        if (iconHardSkeleton != null) iconHardSkeleton.gameObject.SetActive(_activeIcon == iconHardSkeleton);
        if (iconVeryHardSkeleton != null) iconVeryHardSkeleton.gameObject.SetActive(_activeIcon == iconVeryHardSkeleton);
        if (_activeIcon != null) _activeIcon.Initialize(true);

        // 3. Bắt đầu chuỗi hiệu ứng xuất hiện
        if (_showSequenceCoroutine != null) StopCoroutine(_showSequenceCoroutine);
        _showSequenceCoroutine = StartCoroutine(ShowSequenceRoutine());

        // 4. Tự động tắt sau thời gian quy định (tính từ lúc bắt đầu show)
        if (_hideCoroutine != null) StopCoroutine(_hideCoroutine);
        _hideCoroutine = StartCoroutine(HideRoutine());

        // 5. Hiện textDesLevelHard sau 0.5 giây
        if (_showDesLevelHard != null) StopCoroutine(_showDesLevelHard);
        _showDesLevelHard = StartCoroutine(DelayShowDesLevelHard(difficulty));

    }
    private IEnumerator DelayShowDesLevelHard(LevelDifficulty difficulty)
    {
        yield return new WaitForSeconds(0.5f); // Delay 0.5 giây trước khi hiện textDesLevelHard

        GameObject des = difficulty == LevelDifficulty.Hard ? textDesLevelHard
                       : difficulty == LevelDifficulty.VeryHard ? textDesLevelVeryHard : null;
        if (textDesLevelHard != null) textDesLevelHard.SetActive(des == textDesLevelHard);
        if (textDesLevelVeryHard != null) textDesLevelVeryHard.SetActive(des == textDesLevelVeryHard);
        if (des == null) yield break;

        // Anim SHOW: scale 0 → 1 kiểu OutBack (nảy nhẹ) — đồng bộ phong cách với icon/text spine.
        _activeDes = des.transform;
        if (_desScaleCoroutine != null) StopCoroutine(_desScaleCoroutine);
        _desScaleCoroutine = StartCoroutine(ScaleDesIn(_activeDes));
    }

    private IEnumerator ScaleDesIn(Transform des)
    {
        float dur = Mathf.Max(0.01f, textScaleDuration);
        float timer = 0f;
        des.localScale = Vector3.zero;
        while (timer < dur)
        {
            timer += Time.deltaTime;
            des.localScale = Vector3.one * EaseOutBack(Mathf.Clamp01(timer / dur));
            yield return null;
        }
        des.localScale = Vector3.one;
        _desScaleCoroutine = null;
    }

    private IEnumerator ShowSequenceRoutine()
    {
        // --- BƯỚC 1: FADE IN PANEL ---
        float timer = 0f;
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
            while (timer < fadeInDuration)
            {
                timer += Time.deltaTime;
                panelCanvasGroup.alpha = Mathf.Lerp(0f, 1f, timer / fadeInDuration);
                yield return null;
            }
            panelCanvasGroup.alpha = 1f;
        }

        // --- BƯỚC 2: SHOW ICON (SCALE OUT BACK) & TEXT (SCALE OUT QUAD) ---
        // Đặt scale ban đầu về 0
        if (_activeIcon != null) _activeIcon.transform.localScale = Vector3.zero;
        if (textSkeleton != null) textSkeleton.transform.localScale = Vector3.zero;
        if (warningBarImage != null) warningBarImage.transform.localScale = Vector3.one; // Reset Bar Scale khi hiện lại

        // Chạy animation Spine Start ngay lúc này để khớp với việc hiện lên
        if (textSkeleton != null) PlaySpineSequence(textSkeleton, textAnimStart, textAnimIdle);
        if (_activeIcon != null) PlaySpineSequence(_activeIcon, iconAnimStart, iconAnimIdle);

        timer = 0f;
        // Dùng max duration để đảm bảo loop chạy đủ
        float maxDuration = Mathf.Max(iconScaleDuration, textScaleDuration);

        while (timer < maxDuration)
        {
            timer += Time.deltaTime;

            // Hiệu ứng cho Icon (OutBack)
            if (_activeIcon != null && timer <= iconScaleDuration)
            {
                float t = timer / iconScaleDuration;
                float scale = EaseOutBack(t);
                _activeIcon.transform.localScale = Vector3.one * scale;
            }
            else if (_activeIcon != null)
            {
                _activeIcon.transform.localScale = Vector3.one;
            }

            // Hiệu ứng cho Text (OutQuad)
            if (textSkeleton != null && timer <= textScaleDuration)
            {
                float t = timer / textScaleDuration;
                float scale = EaseOutQuad(t);
                textSkeleton.transform.localScale = Vector3.one * scale;
            }
            else if (textSkeleton != null)
            {
                textSkeleton.transform.localScale = Vector3.one;
            }

            yield return null;
        }

        // Đảm bảo scale cuối cùng là 1
        if (_activeIcon != null) _activeIcon.transform.localScale = Vector3.one;
        if (textSkeleton != null) textSkeleton.transform.localScale = Vector3.one;
    }

    /// <summary>
    /// Helper để chạy chuỗi animation: Start (1 lần) -> Idle (Lặp).
    /// Bỏ qua animation không tồn tại trong SkeletonData (icon và text có bộ animation khác nhau).
    /// </summary>
    private void PlaySpineSequence(SkeletonGraphic skeleton, string startAnimName, string idleAnimName)
    {
        if (skeleton == null || skeleton.AnimationState == null) return;

        var skeletonData = skeleton.SkeletonData;
        if (skeletonData == null) return;

        skeleton.AnimationState.ClearTracks();

        bool hasStart = !string.IsNullOrEmpty(startAnimName) && skeletonData.FindAnimation(startAnimName) != null;
        bool hasIdle = !string.IsNullOrEmpty(idleAnimName) && skeletonData.FindAnimation(idleAnimName) != null;

        if (hasStart)
        {
            skeleton.AnimationState.SetAnimation(0, startAnimName, !hasIdle);
        }

        if (hasIdle)
        {
            if (hasStart) skeleton.AnimationState.AddAnimation(0, idleAnimName, true, 0);
            else skeleton.AnimationState.SetAnimation(0, idleAnimName, true);
        }
    }

    private IEnumerator HideRoutine()
    {
        // Đợi thêm thời gian hiệu ứng hiển thị để đảm bảo người chơi nhìn thấy đủ lâu
        yield return new WaitForSeconds(displayDuration + fadeInDuration);

        // --- TÍNH TOÁN THỜI GIAN ---
        // Scale Out bắt đầu từ t = 0
        // Fade Out bắt đầu từ t = scaleOutDuration * 0.5f

        float fadeStartTime = scaleOutDuration * 0.8f;

        // Tổng thời gian hiệu ứng tắt = Max(thời gian kết thúc scale, thời gian kết thúc fade)
        float totalHideTime = Mathf.Max(scaleOutDuration, fadeStartTime + fadeOutDuration);

        float timer = 0f;
        float startAlpha = (panelCanvasGroup != null) ? panelCanvasGroup.alpha : 1f;
        Vector3 startScale = Vector3.one;
        Vector3 targetBarScale = new Vector3(1f, 0.5f, 1f); // Thu nhỏ Bar theo trục Y

        while (timer < totalHideTime)
        {
            timer += Time.deltaTime;

            // --- 1. HIỆU ỨNG SCALE OUT (Chạy từ 0 -> scaleOutDuration) ---
            if (timer <= scaleOutDuration)
            {
                float tScale = Mathf.Clamp01(timer / scaleOutDuration);

                // Scale InBack cho Icon và Text
                float backVal = EaseInBack(tScale);
                float scaleVal = 1f - backVal; // 1 -> 0

                if (_activeIcon != null) _activeIcon.transform.localScale = Vector3.one * scaleVal;
                if (textSkeleton != null) textSkeleton.transform.localScale = Vector3.one * scaleVal;
                if (_activeDes != null) _activeDes.localScale = Vector3.one * scaleVal; // des tắt cùng nhịp icon/text

                // Thu nhỏ Warning Bar theo chiều dọc (Linear)
                //if (warningBarImage != null)
                //{
                //    warningBarImage.transform.localScale = Vector3.Lerp(startScale, targetBarScale, tScale);
                //}
            }
            else
            {
                // Đảm bảo Scale về đích nếu đã hết thời gian scale
                if (_activeIcon != null) _activeIcon.transform.localScale = Vector3.zero;
                if (textSkeleton != null) textSkeleton.transform.localScale = Vector3.zero;
                if (_activeDes != null) _activeDes.localScale = Vector3.zero;
                //if (warningBarImage != null) warningBarImage.transform.localScale = targetBarScale;
            }

            // --- 2. HIỆU ỨNG FADE OUT (Chạy từ fadeStartTime -> fadeStartTime + fadeOutDuration) ---
            if (timer >= fadeStartTime)
            {
                float tFade = Mathf.Clamp01((timer - fadeStartTime) / fadeOutDuration);

                if (panelCanvasGroup != null)
                {
                    panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, tFade);
                }
            }

            yield return null;
        }

        // Đảm bảo mọi thứ về đích
        HideImmediate();
    }

    public override void Hide()
    {
        if (notificationPanel != null) notificationPanel.SetActive(false);
        IsShowing = false;
    }

    private void HideImmediate()
    {
        if (panelCanvasGroup != null) panelCanvasGroup.alpha = 0f;
        if (iconHardSkeleton != null) iconHardSkeleton.transform.localScale = Vector3.zero;
        if (iconVeryHardSkeleton != null) iconVeryHardSkeleton.transform.localScale = Vector3.zero;
        if (textSkeleton != null) textSkeleton.transform.localScale = Vector3.zero;
        if (warningBarImage != null) warningBarImage.transform.localScale = Vector3.one;
        if (notificationPanel != null) notificationPanel.SetActive(false);

        // [THÊM MỚI] Đánh dấu đã tắt
        IsShowing = false;
    }

    // --- Easing Functions ---

    // OutBack: Nảy ra một chút rồi thu về (overshoot)
    private float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1;
        return 1 + c3 * Mathf.Pow(x - 1, 3) + c1 * Mathf.Pow(x - 1, 2);
    }

    // InBack: Lùi lại một chút rồi phóng đi (anticipation)
    private float EaseInBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1;
        return c3 * x * x * x - c1 * x * x;
    }

    // OutQuad: Chậm dần đều ở cuối
    private float EaseOutQuad(float x)
    {
        return 1 - (1 - x) * (1 - x);
    }
}