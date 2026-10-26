using System;
using UnityEngine;

/// <summary>
/// 매력 게이지 + 피버 모드.
/// 유혹 성공 시 매력 게이지 상승 → 가득 차면 10초간 피버.
/// 피버: 이동 속도 x2 / 눈빛 차징 속도 x3 / 라이벌과 교전 시 연타 없이 즉시 승리.
/// 씬에 하나만 두세요.
/// </summary>
public class FeverManager : MonoBehaviour
{
    public static FeverManager Instance { get; private set; }

    [Header("매력 게이지")]
    [SerializeField] private float maxGauge = 100f;
    [Tooltip("일반 타겟(강아지) 유혹 시 증가량")]
    [SerializeField] private float normalGain = 20f;
    [Tooltip("미남 타겟(늑대) 유혹 시 증가량")]
    [SerializeField] private float specialGain = 50f;

    [Header("피버 모드")]
    [SerializeField] private float feverDuration = 10f;
    [SerializeField] private float moveSpeedMultiplier = 2f;
    [SerializeField] private float charmSpeedMultiplier = 3f;

    public float Gauge { get; private set; }
    public float GaugeNormalized => maxGauge > 0f ? Gauge / maxGauge : 0f;
    public bool IsFever { get; private set; }
    public float Remaining { get; private set; }
    public float FeverDuration => feverDuration;

    /// <summary>게이지 비율(0~1). 피버 중엔 남은 시간 비율</summary>
    public event Action<float> OnGaugeChanged;
    public event Action OnFeverStarted;
    public event Action OnFeverEnded;

    public static bool IsFeverActive => Instance != null && Instance.IsFever;
    public static float MoveMultiplier => IsFeverActive ? Instance.moveSpeedMultiplier : 1f;
    public static float CharmMultiplier => IsFeverActive ? Instance.charmSpeedMultiplier : 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void AddGauge(TargetType type)
    {
        AddGauge(type == TargetType.Special ? specialGain : normalGain);
    }

    public void AddGauge(float amount)
    {
        if (IsFever || amount <= 0f) return; // 피버 중엔 충전 안 됨

        Gauge = Mathf.Min(Gauge + amount, maxGauge);
        OnGaugeChanged?.Invoke(GaugeNormalized);
        if (Gauge >= maxGauge) StartFever();
    }

    [ContextMenu("피버 즉시 발동 (테스트)")]
    public void StartFever()
    {
        IsFever = true;
        Remaining = feverDuration;
        Gauge = maxGauge;
        OnGaugeChanged?.Invoke(1f);
        OnFeverStarted?.Invoke();
    }

    private void Update()
    {
        if (!IsFever) return;

        Remaining -= Time.deltaTime;
        Gauge = maxGauge * Mathf.Clamp01(Remaining / feverDuration); // 게이지가 줄어드는 연출
        OnGaugeChanged?.Invoke(GaugeNormalized);

        if (Remaining <= 0f) EndFever();
    }

    private void EndFever()
    {
        IsFever = false;
        Remaining = 0f;
        Gauge = 0f;
        OnGaugeChanged?.Invoke(0f);
        OnFeverEnded?.Invoke();
    }

    /// <summary>새 게임 시작 / 게임 종료 시</summary>
    public void ResetFever()
    {
        bool wasFever = IsFever;
        IsFever = false;
        Remaining = 0f;
        Gauge = 0f;
        OnGaugeChanged?.Invoke(0f);
        if (wasFever) OnFeverEnded?.Invoke();
    }
}