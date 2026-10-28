using UnityEngine;

/// <summary>
/// 게임 제목(로고)을 둥실둥실 떠다니게 하는 연출.
/// 위아래로 천천히 떠오르고, 살짝 좌우로 기울고, 숨쉬듯 커졌다 작아짐.
/// 화면이 켜질 때는 톡 튀어나오며 등장.
/// 시작 화면은 Time.timeScale = 0이라 실제 시간 기준으로 움직임.
/// Title_Logo 이미지(UI Image)에 붙이세요.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class TitleFloat : MonoBehaviour
{
    [Header("둥실둥실 (위아래)")]
    [Tooltip("위아래로 움직이는 높이 (픽셀)")]
    [SerializeField] private float floatHeight = 18f;
    [Tooltip("위아래로 움직이는 속도")]
    [SerializeField] private float floatSpeed = 1.2f;

    [Header("살랑살랑 (기울기)")]
    [Tooltip("좌우로 기우는 각도 (도)")]
    [SerializeField] private float swayAngle = 1.5f;
    [SerializeField] private float swaySpeed = 0.8f;

    [Header("숨쉬기 (크기)")]
    [Tooltip("커졌다 작아지는 정도 (0.02 = 2%)")]
    [SerializeField] private float breatheAmount = 0.02f;
    [SerializeField] private float breatheSpeed = 1.6f;

    [Header("등장 연출")]
    [Tooltip("화면이 켜질 때 톡 튀어나오며 등장 (0이면 등장 연출 없음)")]
    [SerializeField] private float introDuration = 0.7f;
    [Tooltip("등장할 때 위에서 내려오는 거리 (픽셀)")]
    [SerializeField] private float introDrop = 60f;

    private RectTransform rect;
    private Vector2 basePos;
    private Vector3 baseScale;
    private Quaternion baseRot;
    private float startTime;
    private bool initialized;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        basePos = rect.anchoredPosition;
        baseScale = rect.localScale;
        baseRot = rect.localRotation;
        initialized = true;
    }

    private void OnEnable()
    {
        if (!initialized) return;
        startTime = Time.unscaledTime;
        Apply(0f);
    }

    private void OnDisable()
    {
        // 원래 자리로 (에디터에서 위치가 틀어지지 않게)
        if (rect == null) return;
        rect.anchoredPosition = basePos;
        rect.localScale = baseScale;
        rect.localRotation = baseRot;
    }

    private void Update()
    {
        Apply(Time.unscaledTime - startTime);
    }

    private void Apply(float t)
    {
        // 등장: 위에서 살짝 내려오며 0.8배 → 1배로 톡 (살짝 튕기는 느낌)
        float intro = introDuration > 0f ? Mathf.Clamp01(t / introDuration) : 1f;
        float pop = EaseOutBack(intro);
        float introScale = Mathf.LerpUnclamped(0.8f, 1f, pop);
        float introOffset = (1f - EaseOutCubic(intro)) * introDrop;

        // 둥실둥실: 사인파 두 개를 섞어서 기계적이지 않게
        float bob = (Mathf.Sin(t * floatSpeed) * 0.8f + Mathf.Sin(t * floatSpeed * 0.53f + 1.3f) * 0.2f) * floatHeight;
        float sway = Mathf.Sin(t * swaySpeed + 0.7f) * swayAngle;
        float breathe = 1f + Mathf.Sin(t * breatheSpeed) * breatheAmount;

        rect.anchoredPosition = basePos + new Vector2(0f, bob + introOffset);
        rect.localRotation = baseRot * Quaternion.Euler(0f, 0f, sway);
        rect.localScale = baseScale * (introScale * breathe);
    }

    private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}