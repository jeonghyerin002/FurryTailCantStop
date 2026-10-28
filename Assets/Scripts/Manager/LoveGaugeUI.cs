using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 상단의 하트 게이지 슬라이더 하나로 모든 게이지를 표시.
/// 평소: 플레이어가 눈빛을 쏘는 타겟의 LovePercent (왼쪽 → 오른쪽으로 차오름)
/// 대결: 슬라이더가 중앙으로 이동 → 플레이어 우세 시 오른쪽, 라이벌 우세 시 왼쪽으로 이동
/// Slider의 Direction은 Left To Right로 두세요.
/// </summary>
public class LoveGaugeUI : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("비워두면 씬에서 자동으로 찾음")]
    [SerializeField] private PlayerAttack player;
    [SerializeField] private Slider slider;
    [Tooltip("Slider > Fill Area > Fill 이미지 (플레이어 쪽 색)")]
    [SerializeField] private Image fillImage;
    [Tooltip("Slider > Background 이미지 (대결 중엔 라이벌 쪽 색)")]
    [SerializeField] private Image backgroundImage;
    [Tooltip("(선택) 게이지 옆 글자 (LOVE 72% / DUEL!)")]
    [SerializeField] private TMP_Text label;
    [Tooltip("(선택) 연타할 때 통통 튀는 효과를 줄 대상. 비워두면 슬라이더 자체")]
    [SerializeField] private RectTransform punchTarget;

    [Tooltip("시작할 때 채움 영역과 핸들 영역의 여백을 똑같이 맞춰서, 핸들이 정확히 게이지 끝에 오게 함")]
    [SerializeField] private bool autoAlignHandle = true;

    [Header("색")]
    [SerializeField] private Color loveFillColor = new Color(1f, 0.4f, 0.7f);
    [SerializeField] private Color loveBackgroundColor = new Color(0.2f, 0.2f, 0.25f, 0.8f);
    [Tooltip("대결 중 플레이어 쪽 (왼쪽부터 채워지는 부분)")]
    [SerializeField] private Color duelPlayerColor = new Color(1f, 0.4f, 0.7f);
    [Tooltip("대결 중 라이벌 쪽 (오른쪽 남은 부분)")]
    [SerializeField] private Color duelRivalColor = new Color(1f, 0.55f, 0.15f);

    [Header("문구")]
    [Tooltip("아무 타겟도 노리지 않을 때")]
    [SerializeField] private string idleText = "";
    [Tooltip("{0} = 퍼센트")]
    [SerializeField] private string loveFormat = "LOVE {0}%";
    [Tooltip("{0} = 라이벌 수")]
    [SerializeField] private string duelFormat = "DUEL! x{0}";

    [Header("움직임")]
    [Tooltip("평소 게이지가 따라가는 속도")]
    [SerializeField] private float loveSmoothSpeed = 12f;
    [Tooltip("대결 중 게이지가 따라가는 속도 (대결 시작 시 중앙으로 이동하는 속도이기도 함)")]
    [SerializeField] private float duelSmoothSpeed = 10f;
    [Tooltip("유혹 성공 후 가득 찬 게이지를 보여주는 시간")]
    [SerializeField] private float holdAfterCapture = 0.6f;
    [SerializeField] private float tapPunch = 0.08f;
    [SerializeField] private float punchRecover = 0.6f;

    private TargetMover shownTarget;
    private float holdTimer;
    private float punch;
    private bool wasDuel;
    private Vector3 punchBaseScale = Vector3.one; // 에디터에서 맞춰둔 원래 크기

    private void Start()
    {
        if (player == null) player = FindFirstObjectByType<PlayerAttack>();
        if (punchTarget == null && slider != null) punchTarget = slider.GetComponent<RectTransform>();
        if (punchTarget != null) punchBaseScale = punchTarget.localScale;

        if (slider != null && autoAlignHandle) AlignFillAndHandle();

        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.interactable = false;
            slider.value = 0f;
        }

        if (DuelManager.Instance != null) DuelManager.Instance.OnPlayerTap += HandlePlayerTap;

        ApplyColors(false);
        if (label != null) label.text = idleText;
    }

    private void OnDestroy()
    {
        if (DuelManager.Instance != null) DuelManager.Instance.OnPlayerTap -= HandlePlayerTap;
    }

    private void Update()
    {
        if (slider == null) return;

        float dt = Time.unscaledDeltaTime;
        DuelManager dm = DuelManager.Instance;
        bool duel = dm != null && dm.IsDueling && (player == null || dm.Player == player);

        // 대결이 막 끝났을 때: 이겼으면 가득 찬 게이지 보여주고, 졌으면 비움
        if (wasDuel && !duel && (shownTarget == null || !shownTarget.IsLoved)) shownTarget = null;
        if (duel != wasDuel)
        {
            ApplyColors(duel);
            wasDuel = duel;
        }

        float goal;
        float speed;
        if (duel)
        {
            // 0 = 라이벌 승리(왼쪽 끝), 0.5 = 중앙, 1 = 플레이어 승리(오른쪽 끝)
            goal = dm.Gauge;
            speed = duelSmoothSpeed;
            shownTarget = dm.Target;
            holdTimer = holdAfterCapture;
            if (label != null) label.text = string.Format(duelFormat, dm.RivalCount);
        }
        else
        {
            UpdateShownTarget(dt);
            goal = shownTarget != null && shownTarget.Info != null
                ? Mathf.Clamp01(shownTarget.LovePercent / shownTarget.Info.maxLovePercent)
                : 0f;
            speed = loveSmoothSpeed;
            if (label != null)
                label.text = shownTarget != null ? string.Format(loveFormat, Mathf.RoundToInt(goal * 100f)) : idleText;
        }

        slider.value = Mathf.Lerp(slider.value, goal, 1f - Mathf.Exp(-speed * dt));

        // 연타 펀치 효과
        punch = Mathf.MoveTowards(punch, 0f, punchRecover * dt);
        if (punchTarget != null)
        {
            float s = 1f + punch;
            punchTarget.localScale = new Vector3(punchBaseScale.x * s, punchBaseScale.y * s, punchBaseScale.z);
        }
    }

    /// <summary>게이지에 보여줄 타겟 결정</summary>
    private void UpdateShownTarget(float dt)
    {
        TargetMover current = player != null ? player.CurrentTarget : null;
        if (current != null)
        {
            shownTarget = current;
            holdTimer = holdAfterCapture;
            return;
        }

        if (shownTarget == null) return;

        // 유혹 성공 → 잠깐 가득 찬 상태로 보여준 뒤 비움
        if (shownTarget.IsLoved)
        {
            holdTimer -= dt;
            if (holdTimer <= 0f) shownTarget = null;
            return;
        }

        // 손을 뗀 뒤 게이지가 줄어드는 모습은 계속 보여주고, 0이 되거나 뺏기면 비움
        if (!shownTarget.IsAvailable || shownTarget.LovePercent <= 0f) shownTarget = null;
    }

    /// <summary>
    /// 유니티 기본 슬라이더는 Fill Area(왼5/오15)와 Handle Slide Area(왼10/오10)의 여백이 달라서
    /// 핸들과 게이지 끝이 어긋남 → 둘 다 슬라이더 전체 크기로 맞춤
    /// </summary>
    private void AlignFillAndHandle()
    {
        RectTransform fill = slider.fillRect;
        RectTransform handle = slider.handleRect;
        if (fill == null || handle == null) return;

        StretchFull(fill.parent as RectTransform);
        StretchFull(handle.parent as RectTransform);

        // 채움은 값 위치에서 정확히 끝나게 (기본 슬라이더는 Fill 폭이 10만큼 더 길게 잡혀 있음)
        fill.offsetMin = new Vector2(0f, fill.offsetMin.y);
        fill.offsetMax = new Vector2(0f, fill.offsetMax.y);

        // 핸들 중심이 값 위치에 오게
        handle.pivot = new Vector2(0.5f, handle.pivot.y);
        handle.anchoredPosition = new Vector2(0f, handle.anchoredPosition.y);
    }

    private void StretchFull(RectTransform area)
    {
        if (area == null || area == slider.transform) return;
        area.anchorMin = new Vector2(0f, area.anchorMin.y);
        area.anchorMax = new Vector2(1f, area.anchorMax.y);
        area.offsetMin = new Vector2(0f, area.offsetMin.y);
        area.offsetMax = new Vector2(0f, area.offsetMax.y);
    }

    private void ApplyColors(bool duel)
    {
        if (fillImage != null) fillImage.color = duel ? duelPlayerColor : loveFillColor;
        if (backgroundImage != null) backgroundImage.color = duel ? duelRivalColor : loveBackgroundColor;
    }

    private void HandlePlayerTap()
    {
        punch = tapPunch;
    }
}