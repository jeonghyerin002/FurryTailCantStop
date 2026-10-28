using System;
using UnityEngine;

public enum TargetState
{
    Wander,   // 범위 안에서 자유롭게 배회
    Charming, // 플레이어 또는 라이벌이 눈빛을 쏘는 중 (멈춰 섬)
    Love,     // 플레이어의 포로가 됨
    Taken     // 라이벌에게 뺏김 (라이벌을 따라 화면 밖으로 퇴장 → 사라짐)
}

/// <summary>
/// TargetInfoSO 기반 타겟 이동 + LovePercent 관리.
/// 플레이어 호감도(LovePercent)와 라이벌 호감도(RivalLovePercent)를 따로 관리.
/// 게이지 표시는 화면 상단의 LoveGaugeUI 하나가 담당 (타겟 머리 위 슬라이더 없음).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TargetMover : MonoBehaviour
{
    [SerializeField] private TargetInfoSO info;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Tooltip("뺏긴 뒤 라이벌 뒤를 따라갈 때 간격")]
    [SerializeField] private float takenFollowGap = 0.9f;

    public TargetInfoSO Info => info;
    public TargetState State { get; private set; } = TargetState.Wander;
    public float LovePercent { get; private set; }
    public float RivalLovePercent { get; private set; }
    public bool IsLoved => State == TargetState.Love;
    public bool IsTaken => State == TargetState.Taken;
    /// <summary>플레이어/라이벌이 눈빛을 쏠 수 있는 상태인지</summary>
    public bool IsAvailable => State == TargetState.Wander || State == TargetState.Charming;
    public bool InDuel { get; private set; }
    /// <summary>이 타겟을 노리고 있는 라이벌 (한 번에 한 명만)</summary>
    public RivalAI ClaimingRival { get; private set; }

    /// <summary>플레이어의 Love 상태가 되는 순간 호출</summary>
    public event Action<TargetMover> OnLoved;
    /// <summary>라이벌에게 뺏기는 순간 호출</summary>
    public event Action<TargetMover, RivalAI> OnTaken;

    private Vector2 originPos;
    private Vector2 destination;
    private float waitTimer;
    private bool charmedThisFrame;
    private TailChain chain;
    private RivalAI takenBy;
    private SpriteRenderer[] renderers;
    private Collider2D[] colliders;

    private void Awake()
    {
        originPos = transform.position;
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        colliders = GetComponentsInChildren<Collider2D>(true);
    }

    private void Start()
    {
        PickNewDestination();
    }

    private void Update()
    {
        switch (State)
        {
            case TargetState.Wander:
                Wander();
                DecayLove();
                break;
            case TargetState.Charming:
                // 제자리에 멈춤
                break;
            case TargetState.Love:
                FollowChain(); // 유혹된 뒤엔 플레이어 꼬리 줄을 계속 따라감 (떠나지 않음)
                break;
            case TargetState.Taken:
                FollowTaker(); // 뺏은 라이벌을 따라 화면 밖으로
                break;
        }
    }

    private void LateUpdate()
    {
        // 이번 프레임에 아무도 눈빛을 쏘지 않았다면 배회로 복귀 (대결 중엔 유지)
        if (State == TargetState.Charming && !charmedThisFrame && !InDuel)
        {
            State = TargetState.Wander;
            PickNewDestination();
        }
        charmedThisFrame = false;
    }

    /// <summary>새 게임 시작 시: 처음 위치, LovePercent 0, 배회 상태로 초기화</summary>
    public void ResetTarget()
    {
        State = TargetState.Wander;
        LovePercent = 0f;
        RivalLovePercent = 0f;
        SetVisible(true);
        takenBy = null;
        if (chain != null) chain.Remove(this);
        chain = null;
        ClaimingRival = null;
        InDuel = false;
        charmedThisFrame = false;
        waitTimer = 0f;
        transform.position = new Vector3(originPos.x, originPos.y, transform.position.z);
        PickNewDestination();
    }

    #region 플레이어 / 라이벌 호감도

    /// <summary>PlayerAttack에서 매 프레임 호출. Love 상태가 된 프레임에 true 반환.</summary>
    public bool AddLove(float amount, Transform charmer)
    {
        if (!IsAvailable) return false;

        State = TargetState.Charming;
        charmedThisFrame = true;

        LovePercent = Mathf.Min(LovePercent + amount / info.loveResistance, info.maxLovePercent);

        if (LovePercent >= info.maxLovePercent)
        {
            BecomeLove(charmer);
            return true;
        }
        return false;
    }

    /// <summary>RivalAI에서 매 프레임 호출. 라이벌에게 뺏긴 프레임에 true 반환.</summary>
    public bool AddRivalLove(float amount, RivalAI rival)
    {
        if (!IsAvailable || InDuel || rival == null) return false;

        State = TargetState.Charming;
        charmedThisFrame = true;

        RivalLovePercent = Mathf.Min(RivalLovePercent + amount / info.rivalResistance, info.maxLovePercent);

        if (RivalLovePercent >= info.maxLovePercent)
        {
            BecomeTaken(rival);
            return true;
        }
        return false;
    }

    /// <summary>연타 대결 승리 시 즉시 Love 상태로</summary>
    public bool ForceLove(Transform charmer)
    {
        if (!IsAvailable) return false;
        LovePercent = info.maxLovePercent;
        BecomeLove(charmer);
        return true;
    }

    /// <summary>라이벌이 이 타겟을 노리기 시작할 때. 다른 라이벌이 이미 노리고 있으면 false</summary>
    public bool TryClaim(RivalAI rival)
    {
        if (!IsAvailable || rival == null) return false;
        if (ClaimingRival != null && ClaimingRival != rival) return false;
        ClaimingRival = rival;
        return true;
    }

    public void ReleaseClaim(RivalAI rival)
    {
        if (ClaimingRival == rival) ClaimingRival = null;
    }

    /// <summary>DuelManager가 호출. 대결 중엔 제자리에 멈춰 있음</summary>
    public void SetInDuel(bool inDuel)
    {
        InDuel = inDuel;
        if (IsAvailable)
        {
            State = TargetState.Charming;
            charmedThisFrame = true;
        }
    }

    private void BecomeLove(Transform charmer)
    {
        State = TargetState.Love;

        // 플레이어 꼬리 줄 맨 뒤에 합류
        chain = null;
        if (charmer != null)
        {
            chain = charmer.GetComponent<TailChain>();
            if (chain == null) chain = charmer.gameObject.AddComponent<TailChain>();
            chain.Add(this);
        }
        RivalLovePercent = 0f;
        ClaimingRival = null;
        InDuel = false;
        OnLoved?.Invoke(this);
    }

    private void BecomeTaken(RivalAI rival)
    {
        State = TargetState.Taken;
        LovePercent = 0f;
        ClaimingRival = null;
        InDuel = false;
        takenBy = rival;
        OnTaken?.Invoke(this, rival);
    }

    /// <summary>대결에서 플레이어가 지면 즉시 라이벌에게 뺏김</summary>
    public bool ForceTaken(RivalAI rival)
    {
        if (!IsAvailable || rival == null) return false;
        RivalLovePercent = info.maxLovePercent;
        BecomeTaken(rival);
        return true;
    }

    /// <summary>라이벌과 함께 화면 밖으로 나가 사라짐 (RivalAI가 호출)</summary>
    public void HideTaken()
    {
        SetVisible(false);
        takenBy = null;
    }

    private void FollowTaker()
    {
        if (takenBy == null) return;
        if (!takenBy.gameObject.activeInHierarchy)
        {
            HideTaken();
            return;
        }
        float speed = takenBy.CurrentLeaveSpeed > 0f ? takenBy.CurrentLeaveSpeed * 1.15f : info.followSpeed;
        if (Vector2.Distance(transform.position, takenBy.transform.position) > takenFollowGap)
            MoveTowards(takenBy.transform.position, speed);
    }

    /// <summary>보이기/숨기기 (숨기면 터치도 안 됨)</summary>
    private void SetVisible(bool visible)
    {
        if (renderers != null)
            foreach (SpriteRenderer r in renderers)
                if (r != null && r.gameObject.name != "Aura") r.enabled = visible; // 아우라/번개는 TargetVisual이 관리
        if (colliders != null)
            foreach (Collider2D c in colliders)
                if (c != null) c.enabled = visible;
    }

    private void DecayLove()
    {
        if (info.loveDecayPerSecond <= 0f) return;
        if (LovePercent <= 0f && RivalLovePercent <= 0f) return;

        float d = info.loveDecayPerSecond * Time.deltaTime;
        LovePercent = Mathf.Max(0f, LovePercent - d);
        RivalLovePercent = Mathf.Max(0f, RivalLovePercent - d);
    }


    #endregion

    #region Movement

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
            waitTimer = UnityEngine.Random.Range(info.waitTimeRange.x, info.waitTimeRange.y);
    }

    private void PickNewDestination()
    {
        destination = originPos + new Vector2(
            UnityEngine.Random.Range(-info.moveRange.x, info.moveRange.x),
            UnityEngine.Random.Range(-info.moveRange.y, info.moveRange.y));

        if (info.clampToCameraView) destination = CameraBounds.Clamp(destination, info.screenPadding);
    }

    /// <summary>꼬리 줄의 자기 자리로 이동 + 다른 캐릭터와 겹치지 않게 밀어냄</summary>
    private void FollowChain()
    {
        if (chain == null) return;

        Vector2 current = transform.position;
        Vector2 slot = chain.GetSlotPosition(this);
        float dist = Vector2.Distance(current, slot);
        float speed = Mathf.Max(info.followSpeed, dist * chain.CatchUpFactor);

        Vector2 next = Vector2.MoveTowards(current, slot, speed * Time.deltaTime);
        next += chain.GetSeparation(this) * Time.deltaTime;
        transform.position = new Vector3(next.x, next.y, transform.position.z);

        if (spriteRenderer != null && Mathf.Abs(next.x - current.x) > 0.001f)
            spriteRenderer.flipX = next.x < current.x;
    }

    private void MoveTowards(Vector2 target, float speed)
    {
        Vector2 current = transform.position;
        Vector2 next = Vector2.MoveTowards(current, target, speed * Time.deltaTime);
        transform.position = new Vector3(next.x, next.y, transform.position.z);

        if (spriteRenderer != null && Mathf.Abs(target.x - current.x) > 0.01f)
            spriteRenderer.flipX = target.x < current.x;
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        if (info == null) return;
        Vector3 center = Application.isPlaying ? (Vector3)originPos : transform.position;
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(center, new Vector3(info.moveRange.x * 2f, info.moveRange.y * 2f, 0f));
    }
}