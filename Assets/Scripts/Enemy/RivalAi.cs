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
    Stunned,  // 튕겨 나가 기절
    Leaving,  // 뺏은 타겟을 데리고 화면 밖으로 퇴장 중
    Gone      // 퇴장 완료 (새 게임 전까지 사라짐)
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
    [Tooltip("퇴장할 때 걸음마다 통통 튀는 높이 (스프라이트가 자식 오브젝트일 때만)")]
    [SerializeField] private float walkBobHeight = 0.08f;
    [SerializeField] private float walkBobSpeed = 9f;

    public RivalInfoSO Info => info;
    public RivalMode Mode => mode;
    public RivalState State { get; private set; } = RivalState.Wander;
    public TargetMover Target => target;
    public StunController Stun => stun;

    /// <summary>지금 이 타겟에 눈빛을 쏘고 있는지 (대결 시작 조건)</summary>
    public bool IsCharming(TargetMover t) => State == RivalState.Charming && target != null && target == t;

    /// <summary>근처에서 벌어진 대결에 합류할 수 있는 상태인지 (기절/대결 중이 아님)</summary>
    public bool CanJoinDuel =>
        isActiveAndEnabled && stun != null && !stun.IsBusy &&
        (State == RivalState.Wander || State == RivalState.Approach || State == RivalState.Charming);

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
    private TargetMover stolenTarget;
    private Vector2 exitPoint;
    private float leaveTimer;
    private float leaveSpeed;
    private float leaveDeadline;

    /// <summary>지금 퇴장하는 속도 (뺏긴 타겟이 따라올 때 사용)</summary>
    public float CurrentLeaveSpeed => leaveSpeed;
    private Vector3 spriteBaseLocalPos;

    public bool IsLeaving => State == RivalState.Leaving;
    public bool IsGone => State == RivalState.Gone;

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
        if (spriteRenderer != null) spriteBaseLocalPos = spriteRenderer.transform.localPosition;
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
        // 뺏은 타겟을 데리고 퇴장 중
        if (State == RivalState.Leaving)
        {
            Leave();
            return;
        }
        if (State == RivalState.Gone) return;

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
            // 플레이어가 먼저 공격할 때만 움직이는 설정이면 혼자 사냥하지 않음
            if (State == RivalState.Wander && mode == RivalMode.Hunter && !info.attackOnlyAfterPlayer) TryHunt();
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

    /// <summary>플레이어가 먼저 공격할 때만 움직이는 설정에서, 플레이어가 이 타겟에서 손을 뗐는지</summary>
    private bool PlayerStoppedAttacking() =>
        info.attackOnlyAfterPlayer && (player == null || player.CurrentTarget != target);

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
        if (!ValidTarget() || PlayerStoppedAttacking())
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
        if (!ValidTarget() || PlayerStoppedAttacking())
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
            // 뺏기 성공 → 타겟을 데리고 화면 밖으로 퇴장
            TargetMover stolen = target;
            target = null;
            StartLeaving(stolen);
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
        // 다른 타겟을 노리던 중이었다면 그 타겟은 놓아줌 (대결 합류)
        if (target != null && target != t) target.ReleaseClaim(this);
        target = t;
        State = RivalState.Dueling;
        ShowBeam();
    }

    /// <param name="rivalWon">true면 계속 눈빛을 쏴서 타겟을 뺏으려 함 (타겟을 먼저 노리던 라이벌만)</param>
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
        if (!gameObject.activeSelf) gameObject.SetActive(true); // 퇴장해서 꺼졌던 라이벌 복귀
        stolenTarget = null;
        if (spriteRenderer != null) spriteRenderer.transform.localPosition = spriteBaseLocalPos;
        stun.ResetState();
        ReleaseTarget();
        transform.position = new Vector3(originPos.x, originPos.y, transform.position.z);
        State = RivalState.Wander;
        waitTimer = 0f;
        scanTimer = 0f;
        cooldownTimer = InitialCooldown();
        PickNewDestination();
    }

    #region 퇴장 (타겟을 뺏은 뒤)

    /// <summary>
    /// 뺏은 타겟을 데리고 화면 밖으로 터벅터벅 걸어나감.
    /// duration을 주면 그 시간 안에 화면 밖에 도착하도록 속도를 맞춤 (플레이어가 멈춰 있는 동안).
    /// </summary>
    public void StartLeaving(TargetMover stolen, float duration = 0f)
    {
        ReleaseTarget();
        stolenTarget = stolen;
        exitPoint = ComputeExitPoint();
        leaveTimer = 0f;

        float dist = Vector2.Distance(transform.position, exitPoint);
        if (duration > 0f)
        {
            leaveSpeed = Mathf.Max(info.leaveSpeed, dist / duration);
            leaveDeadline = duration + 0.3f;
        }
        else
        {
            leaveSpeed = info.leaveSpeed;
            leaveDeadline = info.leaveTimeout;
        }
        State = RivalState.Leaving;
    }

    private void Leave()
    {
        HideBeam();
        leaveTimer += Time.deltaTime;
        MoveTowards(exitPoint, leaveSpeed);

        // 터벅터벅: 걸음마다 살짝 통통
        if (spriteRenderer != null && spriteRenderer.transform != transform)
        {
            float bob = Mathf.Abs(Mathf.Sin(leaveTimer * walkBobSpeed)) * walkBobHeight;
            spriteRenderer.transform.localPosition = spriteBaseLocalPos + new Vector3(0f, bob, 0f);
        }

        bool arrived = Vector2.Distance(transform.position, exitPoint) < 0.05f;
        if (arrived || IsOutsideCamera(transform.position, info.leaveMargin) || leaveTimer > leaveDeadline)
            Vanish();
    }

    private void Vanish()
    {
        State = RivalState.Gone;
        if (spriteRenderer != null) spriteRenderer.transform.localPosition = spriteBaseLocalPos;
        if (stolenTarget != null) stolenTarget.HideTaken();
        stolenTarget = null;
        gameObject.SetActive(false);
    }

    /// <summary>플레이어 반대쪽, 가까운 화면 옆쪽 가장자리 바깥 지점</summary>
    private Vector2 ComputeExitPoint()
    {
        Vector2 pos = transform.position;
        Vector2 dir = player != null ? pos - (Vector2)player.transform.position : Vector2.right;
        if (dir.sqrMagnitude < 0.0001f) dir = Random.value < 0.5f ? Vector2.left : Vector2.right;
        // 세로 화면이라 옆으로 나가는 게 가까움 → 가로 방향 위주로
        dir = new Vector2(Mathf.Sign(dir.x == 0f ? 1f : dir.x), dir.normalized.y * 0.3f).normalized;

        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return pos + dir * 15f;

        float m = info.leaveMargin + 1f; // 캐릭터 몸 크기만큼 더 나가야 완전히 안 보임
        float halfH = cam.orthographicSize + m;
        float halfW = cam.orthographicSize * cam.aspect + m;
        Vector2 c = cam.transform.position;

        float tx = dir.x > 0f ? (c.x + halfW - pos.x) / dir.x : dir.x < 0f ? (c.x - halfW - pos.x) / dir.x : float.PositiveInfinity;
        float ty = dir.y > 0f ? (c.y + halfH - pos.y) / dir.y : dir.y < 0f ? (c.y - halfH - pos.y) / dir.y : float.PositiveInfinity;
        float t = Mathf.Max(0f, Mathf.Min(tx, ty));
        return pos + dir * t;
    }

    private static bool IsOutsideCamera(Vector2 pos, float margin)
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return false;
        float halfH = cam.orthographicSize + margin;
        float halfW = cam.orthographicSize * cam.aspect + margin;
        Vector2 c = cam.transform.position;
        return pos.x < c.x - halfW || pos.x > c.x + halfW || pos.y < c.y - halfH || pos.y > c.y + halfH;
    }

    #endregion

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