using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 연타 교전(대결 모드).
/// 1. 플레이어와 라이벌이 같은 타겟에 눈빛을 쏘면 대결 시작
/// 2. 제한 시간 동안 화면 아무 곳이나 연타 → 게이지를 라이벌 쪽으로 밀어냄
/// 3. 승리: 라이벌이 튕겨 나가 기절 + 타겟 즉시 유혹 성공
/// 4. 패배: 플레이어가 튕겨 나가 스턴
/// 피버 중에는 연타 없이 즉시 승리.
/// 씬에 하나만 두세요.
/// </summary>
public class DuelManager : MonoBehaviour
{
    public static DuelManager Instance { get; private set; }
    public static bool IsActive => Instance != null && Instance.IsDueling;
    /// <summary>대결 중이거나 대결 직후 → 이동/공격 입력 무시</summary>
    public static bool BlocksInput => Instance != null && (Instance.IsDueling || Instance.inputBlockTimer > 0f);

    [Header("대결 규칙")]
    [Tooltip("연타 제한 시간(초)")]
    [SerializeField] private float duelDuration = 4f;
    [Tooltip("'대결!' 연출 시간. 이 동안은 연타가 반영되지 않음")]
    [SerializeField] private float introDelay = 0.6f;
    [Tooltip("시작 게이지 (0 = 라이벌 승, 1 = 플레이어 승)")]
    [Range(0.1f, 0.9f)][SerializeField] private float startGauge = 0.5f;
    [SerializeField] private float playerPushPerTap = 0.05f;
    [SerializeField] private float rivalPushPerTap = 0.05f;
    [Tooltip("대결이 끝난 뒤 연타가 이동으로 새지 않도록 입력을 막는 시간")]
    [SerializeField] private float postDuelInputBlock = 0.35f;

    [Header("승리 시 (라이벌 튕겨 나감 + 기절)")]
    [SerializeField] private float rivalKnockbackDistance = 3f;
    [SerializeField] private float rivalFaintDuration = 3f;

    [Header("패배 시 (플레이어 튕겨 나감 + 스턴)")]
    [SerializeField] private float playerKnockbackDistance = 2.5f;
    [SerializeField] private float playerStunDuration = 2f;

    [Header("공통")]
    [SerializeField] private float knockbackTime = 0.35f;

    public bool IsDueling { get; private set; }
    public bool InIntro => IsDueling && introTimer > 0f;
    /// <summary>0 = 라이벌 승리, 1 = 플레이어 승리</summary>
    public float Gauge { get; private set; }
    public float Remaining { get; private set; }
    public float Duration => duelDuration;
    public TargetMover Target { get; private set; }
    public RivalAI Rival { get; private set; }
    public PlayerAttack Player { get; private set; }

    public event Action OnDuelStarted;
    public event Action OnPlayerTap;
    public event Action OnRivalTap;
    /// <summary>(플레이어 승리 여부, 피버로 즉시 승리했는지)</summary>
    public event Action<bool, bool> OnDuelEnded;
    public event Action OnDuelCancelled;

    private float introTimer;
    private float rivalTapTimer;
    private float inputBlockTimer;

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

    public bool StartDuel(PlayerAttack player, RivalAI rival, TargetMover target)
    {
        if (IsDueling || player == null || rival == null || target == null) return false;

        Player = player;
        Rival = rival;
        Target = target;

        // 피버: 연타 없이 한 번에 라이벌을 날려버림
        if (FeverManager.IsFeverActive)
        {
            Finish(true, true);
            return true;
        }

        IsDueling = true;
        Gauge = startGauge;
        Remaining = duelDuration;
        introTimer = introDelay;
        rivalTapTimer = NextRivalInterval();

        target.SetInDuel(true);
        rival.EnterDuel(target);
        OnDuelStarted?.Invoke();
        return true;
    }

    private void Update()
    {
        if (inputBlockTimer > 0f) inputBlockTimer -= Time.deltaTime;
        if (!IsDueling) return;

        if (Target == null || Rival == null || Player == null || !Target.IsAvailable)
        {
            CancelDuel();
            return;
        }

        if (introTimer > 0f)
        {
            introTimer -= Time.deltaTime;
            return;
        }

        // 플레이어 연타
        int taps = CountTapsThisFrame();
        if (taps > 0)
        {
            Gauge += taps * playerPushPerTap;
            OnPlayerTap?.Invoke();
        }

        // 라이벌 연타 (AI)
        rivalTapTimer -= Time.deltaTime;
        while (rivalTapTimer <= 0f)
        {
            Gauge -= rivalPushPerTap;
            OnRivalTap?.Invoke();
            rivalTapTimer += NextRivalInterval();
        }

        Gauge = Mathf.Clamp01(Gauge);
        Remaining -= Time.deltaTime;

        if (Gauge >= 1f) Finish(true, false);
        else if (Gauge <= 0f) Finish(false, false);
        else if (Remaining <= 0f)
        {
            Remaining = 0f;
            Finish(Gauge >= startGauge, false); // 시간 종료: 게이지를 더 밀어낸 쪽 승리 (동점은 플레이어)
        }
    }

    private void Finish(bool playerWon, bool instant)
    {
        IsDueling = false;
        introTimer = 0f;
        inputBlockTimer = postDuelInputBlock;

        TargetMover target = Target;
        RivalAI rival = Rival;
        PlayerAttack player = Player;

        target.SetInDuel(false);
        Vector2 pPos = player.transform.position;
        Vector2 rPos = rival.transform.position;

        if (playerWon)
        {
            rival.ExitDuel(false);
            if (rival.Stun != null)
                rival.Stun.Knockback(rPos - pPos, rivalKnockbackDistance, knockbackTime, rivalFaintDuration);
            player.CaptureByDuel(target);
        }
        else
        {
            rival.ExitDuel(true);
            player.CancelAttack();
            if (player.Stun != null)
                player.Stun.Knockback(pPos - rPos, playerKnockbackDistance, knockbackTime, playerStunDuration);
        }

        ClearRefs();
        OnDuelEnded?.Invoke(playerWon, instant);
    }

    /// <summary>결과 없이 대결 취소 (게임 종료 등)</summary>
    public void CancelDuel()
    {
        if (!IsDueling) return;
        IsDueling = false;
        introTimer = 0f;
        if (Target != null) Target.SetInDuel(false);
        if (Rival != null) Rival.ExitDuel(false);
        ClearRefs();
        OnDuelCancelled?.Invoke();
    }

    /// <summary>새 게임 시작 / 게임 종료 시</summary>
    public void ResetDuel()
    {
        CancelDuel();
        inputBlockTimer = 0f;
    }

    private void ClearRefs()
    {
        Target = null;
        Rival = null;
        Player = null;
    }

    private float NextRivalInterval()
    {
        float tps = Rival != null && Rival.Info != null ? Rival.Info.tapsPerSecond : 6f;
        float jitter = Rival != null && Rival.Info != null ? Rival.Info.tapJitter : 0.3f;
        tps = Mathf.Max(0.5f, tps);
        return 1f / tps * UnityEngine.Random.Range(1f - jitter, 1f + jitter);
    }

    /// <summary>이번 프레임의 탭 수 (멀티터치 연타 지원, 에디터에선 마우스)</summary>
    private static int CountTapsThisFrame()
    {
        int count = 0;

        Touchscreen ts = Touchscreen.current;
        if (ts != null)
        {
            foreach (var touch in ts.touches)
                if (touch.press.wasPressedThisFrame) count++;
        }

        if (count == 0)
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) count++;
        }

        return count;
    }
}