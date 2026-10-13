using System;
using UnityEngine;
using UnityEngine.UI;

public enum TargetState
{
    Wander,   // 범위 안에서 자유롭게 배회
    Charming, // 플레이어가 누르고 있는 중 (멈춰 서서 LovePercent 상승)
    Love      // 포로가 됨
}
[RequireComponent(typeof(Collider2D))]
public class TargetMover : MonoBehaviour
{
    [SerializeField] private TargetInfoSO info;
    [Tooltip("타겟 머리 위 World Space Canvas의 Slider")]
    [SerializeField] private Slider lovePercentSlider;
    [SerializeField] private SpriteRenderer spriteRenderer;

    public TargetInfoSO Info => info;
    public TargetState State { get; private set; } = TargetState.Wander;
    public float LovePercent { get; private set; }
    public bool IsLoved => State == TargetState.Love;

    public event Action<TargetMover> OnLoved;

    private Vector2 originPos;
    private Vector2 destination;
    private float waitTimer;
    private bool charmedThisFrame;
    private Transform followTarget;

    private void Awake()
    {
        originPos = transform.position;
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (lovePercentSlider != null)
        {
            lovePercentSlider.minValue = 0f;
            lovePercentSlider.maxValue = info.maxLovePercent;
            lovePercentSlider.interactable = false;
        }
        RefreshSlider();
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
                FollowPlayer();
                break;
        }
    }

    private void LateUpdate()
    {
        // 이번 프레임에 PlayerAttack이 AddLove를 호출하지 않았다면 배회로 복귀
        if (State == TargetState.Charming && !charmedThisFrame)
        {
            State = TargetState.Wander;
            PickNewDestination();
        }
        charmedThisFrame = false;
    }

    #region LovePercent

    /// <summary>
    /// PlayerAttack에서 매 프레임 호출. Love 상태가 된 프레임에 true 반환.
    /// </summary>
    public bool AddLove(float amount, Transform charmer)
    {
        if (IsLoved) return false;

        State = TargetState.Charming;
        charmedThisFrame = true;

        LovePercent = Mathf.Min(LovePercent + amount / info.loveResistance, info.maxLovePercent);
        RefreshSlider();

        if (LovePercent >= info.maxLovePercent)
        {
            BecomeLove(charmer);
            return true;
        }
        return false;
    }

    private void BecomeLove(Transform charmer)
    {
        State = TargetState.Love;
        followTarget = charmer;
        OnLoved?.Invoke(this);
    }

    private void DecayLove()
    {
        if (LovePercent <= 0f || info.loveDecayPerSecond <= 0f) return;
        LovePercent = Mathf.Max(0f, LovePercent - info.loveDecayPerSecond * Time.deltaTime);
        RefreshSlider();
    }

    private void RefreshSlider()
    {
        if (lovePercentSlider != null) lovePercentSlider.value = LovePercent;
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
    }

    private void FollowPlayer()
    {
        if (!info.followPlayerOnLove || followTarget == null) return;

        if (Vector2.Distance(transform.position, followTarget.position) > info.followDistance)
            MoveTowards(followTarget.position, info.followSpeed);
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
