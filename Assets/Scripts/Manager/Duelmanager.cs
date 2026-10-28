using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 연타 교전(대결 모드).
/// 1. 플레이어와 라이벌이 같은 타겟에 눈빛을 쏘면 대결 시작 → 게이지가 중앙(0.5)에서 시작
/// 2. 제한 시간 동안 화면 아무 곳이나 연타 → 게이지를 오른쪽(플레이어 승리)으로 밀어냄
///    라이벌들은 자동으로 연타해서 게이지를 왼쪽(라이벌 승리)으로 밀어냄
/// 3. 근처의 라이벌들이 대결에 합류함 → 라이벌이 많을수록 더 빨리 연타해야 이김
/// 4. 승리: 대결에 참여한 라이벌 전원이 튕겨 나가 기절 + 타겟 즉시 유혹 성공
/// 5. 패배: 플레이어가 튕겨 나가 스턴
/// 피버 중에는 연타 없이 즉시 승리 (근처 라이벌 전부 날려버림).
/// 씬에 하나만 두세요.
/// </summary>
public class DuelManager : MonoBehaviour
{
    public static DuelManager Instance { get; private set; }
    public static bool IsActive => Instance != null && Instance.IsDueling;
    /// <summary>대결 중이거나 대결 직후 → 이동/공격 입력 무시</summary>
    public static bool BlocksInput => Instance != null && (Instance.IsDueling || Instance.inputBlockTimer > 0f);

    /// <summary>대결 시작 게이지 = 화면 상단 슬라이더의 중앙</summary>
    public const float CenterGauge = 0.5f;

    [Header("대결 규칙")]
    [Tooltip("연타 제한 시간(초)")]
    [SerializeField] private float duelDuration = 4f;
    [Tooltip("'대결!' 연출 시간. 이 동안은 연타가 반영되지 않음 (슬라이더가 중앙으로 이동하는 시간)")]
    [SerializeField] private float introDelay = 0.6f;
    [Tooltip("플레이어 탭 1번에 오른쪽으로 미는 양")]
    [SerializeField] private float playerPushPerTap = 0.05f;
    [Tooltip("라이벌 탭 1번에 왼쪽으로 미는 양")]
    [SerializeField] private float rivalPushPerTap = 0.05f;
    [Tooltip("대결이 끝난 뒤 연타가 이동으로 새지 않도록 입력을 막는 시간")]
    [SerializeField] private float postDuelInputBlock = 0.35f;

    [Header("라이벌 합류 (경쟁자가 많을수록 어려워짐)")]
    [Tooltip("대결 중인 타겟에서 이 거리 안에 있는 라이벌은 대결에 합류")]
    [SerializeField] private float joinRange = 4f;
    [Tooltip("대결 도중에도 근처로 온 라이벌이 합류할 수 있음")]
    [SerializeField] private bool allowJoinDuringDuel = true;
    [Tooltip("한 대결에 참여할 수 있는 최대 라이벌 수")]
    [Min(1)][SerializeField] private int maxRivals = 4;
    [Tooltip("라이벌이 1명 합류할 때마다 늘어나는 제한 시간(초). 0이면 시간은 그대로")]
    [Min(0f)][SerializeField] private float extraTimePerRival = 0.5f;

    [Header("승리 시 (라이벌 튕겨 나감 + 기절)")]
    [SerializeField] private float rivalKnockbackDistance = 3f;
    [SerializeField] private float rivalFaintDuration = 3f;

    [Header("패배 시 (플레이어 제자리 정지)")]
    [Tooltip("플레이어가 그 자리에서 멈춰 있는 시간(초). 이 시간 동안 라이벌이 뺏은 타겟을 데리고 화면 밖으로 나가서 사라짐")]
    [SerializeField] private float playerFreezeDuration = 1.5f;

    [Header("공통")]
    [SerializeField] private float knockbackTime = 0.35f;

    public bool IsDueling { get; private set; }
    public bool InIntro => IsDueling && introTimer > 0f;
    /// <summary>0 = 라이벌 승리(왼쪽 끝), 0.5 = 중앙, 1 = 플레이어 승리(오른쪽 끝)</summary>
    public float Gauge { get; private set; } = CenterGauge;
    public float Remaining { get; private set; }
    public float Duration => duelDuration;
    public TargetMover Target { get; private set; }
    public PlayerAttack Player { get; private set; }
    /// <summary>처음 대결을 건 라이벌 (타겟을 노리던 라이벌)</summary>
    public RivalAI Rival => rivals.Count > 0 ? rivals[0] : null;
    public IReadOnlyList<RivalAI> Rivals => rivals;
    public int RivalCount => rivals.Count;

    public event Action OnDuelStarted;
    public event Action OnPlayerTap;
    public event Action OnRivalTap;
    /// <summary>라이벌이 대결 도중 합류했을 때 (현재 라이벌 수)</summary>
    public event Action<int> OnRivalJoined;
    /// <summary>(플레이어 승리 여부, 피버로 즉시 승리했는지)</summary>
    public event Action<bool, bool> OnDuelEnded;
    public event Action OnDuelCancelled;

    private readonly List<RivalAI> rivals = new List<RivalAI>();
    private readonly List<float> rivalTapTimers = new List<float>();
    private RivalAI[] allRivals;
    private float introTimer;
    private float inputBlockTimer;
    private float joinScanTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        allRivals = FindObjectsByType<RivalAI>(FindObjectsSortMode.None);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool StartDuel(PlayerAttack player, RivalAI rival, TargetMover target)
    {
        if (IsDueling || player == null || rival == null || target == null) return false;

        Player = player;
        Target = target;
        rivals.Clear();
        rivalTapTimers.Clear();

        // 먼저 타겟을 노리던 라이벌 + 근처 라이벌들 합류
        AddRival(rival);
        GatherJoiners(enterNow: false);

        // 피버: 연타 없이 한 번에 라이벌들을 날려버림
        if (FeverManager.IsFeverActive)
        {
            Finish(true, true);
            return true;
        }

        IsDueling = true;
        Gauge = CenterGauge;
        Remaining = duelDuration + extraTimePerRival * (rivals.Count - 1);
        introTimer = introDelay;
        joinScanTimer = 0.25f;

        target.SetInDuel(true);
        foreach (RivalAI r in rivals) r.EnterDuel(target);

        OnDuelStarted?.Invoke();
        return true;
    }

    private void Update()
    {
        if (inputBlockTimer > 0f) inputBlockTimer -= Time.deltaTime;
        if (!IsDueling) return;

        RemoveInvalidRivals();
        if (Target == null || Player == null || !Target.IsAvailable || rivals.Count == 0)
        {
            CancelDuel();
            return;
        }

        // 대결 도중 근처로 온 라이벌 합류
        if (allowJoinDuringDuel)
        {
            joinScanTimer -= Time.deltaTime;
            if (joinScanTimer <= 0f)
            {
                joinScanTimer = 0.25f;
                GatherJoiners(enterNow: true);
            }
        }

        // 인트로 동안은 게이지가 중앙에 고정
        if (introTimer > 0f)
        {
            introTimer -= Time.deltaTime;
            return;
        }

        // 플레이어 연타 → 오른쪽으로
        int taps = CountTapsThisFrame();
        if (taps > 0)
        {
            Gauge += taps * playerPushPerTap;
            OnPlayerTap?.Invoke();
        }

        // 라이벌들 연타 (AI) → 왼쪽으로. 라이벌 수만큼 따로 연타하므로 많을수록 강해짐
        for (int i = 0; i < rivals.Count; i++)
        {
            rivalTapTimers[i] -= Time.deltaTime;
            while (rivalTapTimers[i] <= 0f)
            {
                Gauge -= rivalPushPerTap;
                OnRivalTap?.Invoke();
                rivalTapTimers[i] += NextRivalInterval(rivals[i]);
            }
        }

        Gauge = Mathf.Clamp01(Gauge);
        Remaining -= Time.deltaTime;

        if (Gauge >= 1f) Finish(true, false);
        else if (Gauge <= 0f) Finish(false, false);
        else if (Remaining <= 0f)
        {
            Remaining = 0f;
            Finish(Gauge >= CenterGauge, false); // 시간 종료: 중앙보다 오른쪽이면 플레이어 승리
        }
    }

    #region 라이벌 합류

    private void GatherJoiners(bool enterNow)
    {
        if (allRivals == null || Target == null) return;

        foreach (RivalAI r in allRivals)
        {
            if (rivals.Count >= maxRivals) break;
            if (!CanJoin(r)) continue;

            AddRival(r);
            if (enterNow)
            {
                r.EnterDuel(Target);
                Remaining += extraTimePerRival;
                OnRivalJoined?.Invoke(rivals.Count);
            }
        }
    }

    private bool CanJoin(RivalAI r)
    {
        if (r == null || rivals.Contains(r) || !r.CanJoinDuel) return false;
        return Vector2.Distance(r.transform.position, Target.transform.position) <= joinRange;
    }

    private void AddRival(RivalAI r)
    {
        rivals.Add(r);
        rivalTapTimers.Add(NextRivalInterval(r));
    }

    private void RemoveInvalidRivals()
    {
        for (int i = rivals.Count - 1; i >= 0; i--)
        {
            RivalAI r = rivals[i];
            if (r == null || !r.isActiveAndEnabled || r.State != RivalState.Dueling)
            {
                rivals.RemoveAt(i);
                rivalTapTimers.RemoveAt(i);
            }
        }
    }

    #endregion

    private void Finish(bool playerWon, bool instant)
    {
        IsDueling = false;
        introTimer = 0f;
        inputBlockTimer = postDuelInputBlock;

        TargetMover target = Target;
        PlayerAttack player = Player;
        Vector2 pPos = player.transform.position;

        target.SetInDuel(false);

        if (playerWon)
        {
            Gauge = 1f;
            // 대결에 참여한 라이벌 전원 튕겨 나감 + 기절
            foreach (RivalAI r in rivals)
            {
                if (r == null) continue;
                r.ExitDuel(false);
                if (r.Stun != null)
                    r.Stun.Knockback((Vector2)r.transform.position - pPos, rivalKnockbackDistance, knockbackTime, rivalFaintDuration);
            }
            player.CaptureByDuel(target);
        }
        else
        {
            Gauge = 0f;
            // 플레이어는 그 자리에서 멈춤
            player.CancelAttack();
            if (player.Stun != null) player.Stun.Stun(playerFreezeDuration);

            // 합류한 라이벌들은 돌아가고, 처음 노리던 라이벌이 타겟을 즉시 뺏어서
            // 플레이어가 멈춰 있는 시간 안에 화면 밖으로 데리고 나감
            foreach (RivalAI r in rivals)
                if (r != null) r.ExitDuel(false);

            RivalAI winner = rivals.Count > 0 ? rivals[0] : null;
            if (winner != null && target.ForceTaken(winner)) winner.StartLeaving(target, playerFreezeDuration);
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
        foreach (RivalAI r in rivals)
            if (r != null) r.ExitDuel(false);
        ClearRefs();
        OnDuelCancelled?.Invoke();
    }

    /// <summary>새 게임 시작 / 게임 종료 시</summary>
    public void ResetDuel()
    {
        CancelDuel();
        inputBlockTimer = 0f;
        Gauge = CenterGauge;
    }

    private void ClearRefs()
    {
        Target = null;
        Player = null;
        rivals.Clear();
        rivalTapTimers.Clear();
    }

    private static float NextRivalInterval(RivalAI rival)
    {
        float tps = rival != null && rival.Info != null ? rival.Info.tapsPerSecond : 6f;
        float jitter = rival != null && rival.Info != null ? rival.Info.tapJitter : 0.3f;
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