using UnityEngine;

public enum RivalMode
{
    Guard,  // 미남 늑대 주변을 점유. 플레이어가 근처 타겟을 노리면 개입
    Hunter  // 맵을 돌아다니며 다른 타겟을 노리고 접근 (뺏어감)
}

public enum RivalState
{
    Wander,   // 배회 / 점유
    Approach, // 타겟에게 접근
    Charming, // 타겟에게 눈빛 발사 (뺏는 중)
    Dueling,  // 플레이어와 연타 대결
    Stunned   // 튕겨 나가 기절
}

/// <summary>
/// 라이벌(여우 영애 퍼리) AI.
/// 플레이어와 같은 타겟에 눈빛을 쏘게 되면 PlayerAttack이 DuelManager로 대결을 시작함.
/// </summary>
[RequireComponent(typeof(StunController))]
public class RivalAI : MonoBehaviour
{
    [SerializeField] private RivalInfoSO info;
    [Tooltip("Guard: 지정한 타겟(늑대) 주변을 점유하다가 플레이어가 노리면 개입\nHunter: 맵을 돌아다니며 타겟을 직접 노림")]
    [SerializeField] private RivalMode mode = RivalMode.Hunter;
    [Tooltip("Guard 모드에서 지킬 타겟 (VIP 테라스의 늑대)")]
    [SerializeField] private TargetMover guardTarget;
    [Tooltip("자식(눈 위치)에 있는 EyeBeam. 비워두면 자식에서 자동 탐색")]
    [SerializeField] private EyeBeam beam;
    [SerializeField] private SpriteRenderer spriteRenderer;

    public RivalInfoSO Info => info;
    public RivalMode Mode => mode;
    public RivalState State { get; private set; } = RivalState.Wander;
    public TargetMover Target => target;
    public StunController Stun => stun;

    /// <summary>지금 이 타겟에 눈빛을 쏘고 있는지 (대결 시작 조건)</summary>
    public bool IsCharming(TargetMover t) => State == RivalState.Charming && target != null && target == t;

    private TargetMover[] targets;
    private PlayerAttack player;
    private StunController stun;
    private TargetMover target;

    private Vector2 originPos;
    private Vector2 destination;
    private float waitTimer;
    private float cooldownTimer;
    private float scanTimer;
    private float approachTimer;

    private Vector2 GuardCenter =>
        mode == RivalMode.Guard && guardTarget != null && guardTarget.IsAvailable
            ? (Vector2)guardTarget.transform.position
            : originPos;

    private void Awake()
    {
        originPos = transform.position;
        stun = GetComponent<StunController>();
        if (beam == null) beam = GetComponentInChildren<EyeBeam>(true);
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Start()
    {
        player = FindFirstObjectByType<PlayerAttack>();
        targets = FindObjectsByType<TargetMover>(FindObjectsSortMode.None);
        cooldownTimer = InitialCooldown();
        PickNewDestination();
    }

    private void OnDisable()
    {
        ReleaseTarget();
    }

    private void Update()
    {
        // 튕겨 나가 기절 중
        if (stun.IsBusy)
        {
            if (State != RivalState.Stunned)
            {
                ReleaseTarget();
                State = RivalState.Stunned;
            }
            return;
        }
        if (State == RivalState.Stunned)
        {
            State = RivalState.Wander;
            cooldownTimer = info.recoverCooldown;
            PickNewDestination();
        }

        // 대결 중엔 제자리에서 눈빛만 유지 (결과는 DuelManager가 처리)
        if (State == RivalState.Dueling)
        {
            if (target != null)
            {
                Face(target.transform.position);
                ShowBeam();
            }
            return;
        }

        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

        if (State == RivalState.Wander)
        {
            TryIntervene();
            if (State == RivalState.Wander && mode == RivalMode.Hunter) TryHunt();
        }

        switch (State)
        {
            case RivalState.Wander:
                HideBeam();
                Wander();
                break;
            case RivalState.Approach:
                Approach();
                break;
            case RivalState.Charming:
                Charm();
                break;
        }
    }

    #region 판단

    /// <summary>플레이어가 근처 타겟에 눈빛을 쏘고 있으면 끼어들기</summary>
    private void TryIntervene()
    {
        if (cooldownTimer > 0f || player == null || !player.IsCharming) return;

        TargetMover t = player.CurrentTarget;
        if (t == null || !t.IsAvailable || t.ClaimingRival != null) return;

        Vector2 refPos = mode == RivalMode.Guard ? GuardCenter : (Vector2)transform.position;
        if (Vector2.Distance(refPos, t.transform.position) > info.interveneRange) return;

        Claim(t);
    }

    /// <summary>Hunter: 가장 가까운 빈 타겟을 노림</summary>
    private void TryHunt()
    {
        if (cooldownTimer > 0f || targets == null) return;

        scanTimer -= Time.deltaTime;
        if (scanTimer > 0f) return;
        scanTimer = 0.5f;

        TargetMover best = null;
        float bestDist = info.detectRange;
        foreach (TargetMover t in targets)
        {
            if (t == null || !t.isActiveAndEnabled || !t.IsAvailable || t.InDuel || t.ClaimingRival != null) continue;
            if (!info.canHuntSpecial && t.Info.targetType == TargetType.Special) continue;

            float d = Vector2.Distance(transform.position, t.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = t;
            }
        }

        if (best != null) Claim(best);
    }

    private void Claim(TargetMover t)
    {
        if (!t.TryClaim(this)) return;
        target = t;
        approachTimer = 0f;
        State = RivalState.Approach;
    }

    private bool ValidTarget() => target != null && target.IsAvailable && target.ClaimingRival == this;

    private void GiveUp()
    {
        ReleaseTarget();
        State = RivalState.Wander;
        cooldownTimer = mode == RivalMode.Hunter ? RandomHuntCooldown() * 0.5f : 1f;
        PickNewDestination();
    }

    #endregion

    #region 행동

    private void Approach()
    {
        if (!ValidTarget())
        {
            GiveUp();
            return;
        }

        approachTimer += Time.deltaTime;
        if (approachTimer > info.giveUpTime)
        {
            GiveUp();
            return;
        }

        HideBeam();
        Vector2 tp = target.transform.position;
        if (Vector2.Distance(transform.position, tp) <= info.charmRange * 0.85f)
        {
            State = RivalState.Charming;
            return;
        }
        MoveTowards(tp, info.approachSpeed);
    }

    private void Charm()
    {
        if (!ValidTarget())
        {
            GiveUp();
            return;
        }

        Vector2 tp = target.transform.position;
        if (Vector2.Distance(transform.position, tp) > info.charmRange)
        {
            State = RivalState.Approach;
            approachTimer = 0f;
            HideBeam();
            return;
        }

        Face(tp);
        ShowBeam();

        float amount = target.Info.maxLovePercent / Mathf.Max(0.1f, info.stealTime) * Time.deltaTime;
        if (target.AddRivalLove(amount, this))
        {
            // 뺏기 성공 → 타겟은 이 라이벌을 따라다님
            target = null;
            HideBeam();
            State = RivalState.Wander;
            cooldownTimer = RandomHuntCooldown();
            PickNewDestination();
        }
    }

    private void Wander()
    {
        if (waitTimer > 0f)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f) PickNewDestination();
            return;
        }

        MoveTowards(destination, info.moveSpeed);

        if (Vector2.Distance(transform.position, destination) <= info.arriveThreshold)
            waitTimer = Random.Range(info.waitTimeRange.x, info.waitTimeRange.y);
    }

    private void PickNewDestination()
    {
        Vector2 center;
        Vector2 range;
        if (mode == RivalMode.Guard)
        {
            center = GuardCenter;
            range = new Vector2(info.guardRadius, info.guardRadius);
        }
        else
        {
            center = originPos;
            range = info.wanderRange;
        }

        destination = center + new Vector2(Random.Range(-range.x, range.x), Random.Range(-range.y, range.y));
        if (info.clampToCameraView) destination = CameraBounds.Clamp(destination, info.screenPadding);
    }

    private void MoveTowards(Vector2 to, float speed)
    {
        Vector2 current = transform.position;
        Vector2 next = Vector2.MoveTowards(current, to, speed * Time.deltaTime);
        transform.position = new Vector3(next.x, next.y, transform.position.z);
        Face(to);
    }

    private void Face(Vector2 to)
    {
        if (spriteRenderer != null && Mathf.Abs(to.x - transform.position.x) > 0.01f)
            spriteRenderer.flipX = to.x < transform.position.x;
    }

    #endregion

    #region 대결 (DuelManager가 호출)

    public void EnterDuel(TargetMover t)
    {
        target = t;
        State = RivalState.Dueling;
        ShowBeam();
    }

    /// <param name="rivalWon">true면 계속 눈빛을 쏴서 타겟을 뺏으려 함</param>
    public void ExitDuel(bool rivalWon)
    {
        if (rivalWon && target != null && target.IsAvailable)
        {
            State = RivalState.Charming;
            return;
        }
        ReleaseTarget();
        State = RivalState.Wander;
        PickNewDestination();
    }

    #endregion

    /// <summary>새 게임 시작 시 초기화</summary>
    public void ResetRival()
    {
        stun.ResetState();
        ReleaseTarget();
        transform.position = new Vector3(originPos.x, originPos.y, transform.position.z);
        State = RivalState.Wander;
        waitTimer = 0f;
        scanTimer = 0f;
        cooldownTimer = InitialCooldown();
        PickNewDestination();
    }

    private void ReleaseTarget()
    {
        if (target != null) target.ReleaseClaim(this);
        target = null;
        HideBeam();
    }

    private float InitialCooldown() => mode == RivalMode.Hunter ? RandomHuntCooldown() : 0f;
    private float RandomHuntCooldown() => Random.Range(info.huntCooldownRange.x, info.huntCooldownRange.y);

    private void ShowBeam()
    {
        if (beam != null && target != null) beam.Show(target.transform);
    }

    private void HideBeam()
    {
        if (beam != null && beam.IsShowing) beam.Hide();
    }

    private void OnDrawGizmosSelected()
    {
        if (info == null) return;
        Vector3 c = mode == RivalMode.Guard && guardTarget != null ? guardTarget.transform.position : transform.position;
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(c, info.interveneRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, info.charmRange);
        if (mode == RivalMode.Hunter)
        {
            Gizmos.color = Color.gray;
            Gizmos.DrawWireSphere(transform.position, info.detectRange);
        }
    }
}