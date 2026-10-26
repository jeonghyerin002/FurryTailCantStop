using System;
using UnityEngine;

/// <summary>
/// 튕겨 나감(넉백) + 기절/스턴 처리. 플레이어와 라이벌 모두에 붙이세요.
/// 기획서: 대결 승리 → 라이벌이 튕겨 나가며 기절 / 패배 → 플레이어가 튕겨 나가며 스턴
/// </summary>
public class StunController : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Tooltip("기절 중 머리 위에 표시할 오브젝트 (빙글빙글 별 이펙트 등)")]
    [SerializeField] private GameObject stunEffect;
    [Tooltip("넉백 이동 곡선 (0→1)")]
    [SerializeField]
    private AnimationCurve knockbackCurve =
        new AnimationCurve(new Keyframe(0f, 0f, 0f, 2.5f), new Keyframe(1f, 1f, 0f, 0f));
    [SerializeField] private bool blinkWhileStunned = true;
    [SerializeField] private float blinkInterval = 0.12f;
    [SerializeField] private bool clampToCamera = true;
    [SerializeField] private float screenPadding = 0.5f;

    public bool IsKnockedBack { get; private set; }
    public bool IsStunned { get; private set; }
    public bool IsBusy => IsKnockedBack || IsStunned;
    public float StunRemaining => stunTimer;
    /// <summary>넉백으로 위치를 바꾼 마지막 프레임 (PlayerController 경고 방지용)</summary>
    public int LastMoveFrame { get; private set; } = -1;

    public event Action<StunController> OnStunStarted;
    public event Action<StunController> OnStunEnded;

    private Vector2 kbFrom;
    private Vector2 kbTo;
    private float kbDuration;
    private float kbTimer;
    private float pendingStun;
    private float stunTimer;
    private float blinkTimer;

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (stunEffect != null) stunEffect.SetActive(false);
    }

    /// <summary>direction 방향으로 distance만큼 duration초 동안 튕겨 나간 뒤 stunDuration초 기절</summary>
    public void Knockback(Vector2 direction, float distance, float duration, float stunDuration)
    {
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;

        bool wasBusy = IsBusy;

        kbFrom = transform.position;
        kbTo = kbFrom + direction.normalized * distance;
        if (clampToCamera) kbTo = CameraBounds.Clamp(kbTo, screenPadding);
        kbDuration = Mathf.Max(0.01f, duration);
        kbTimer = 0f;
        pendingStun = stunDuration;

        IsKnockedBack = true;
        IsStunned = false;
        if (stunEffect != null) stunEffect.SetActive(true);

        if (!wasBusy) OnStunStarted?.Invoke(this);
    }

    private void Update()
    {
        if (IsKnockedBack)
        {
            kbTimer += Time.deltaTime;
            float t = Mathf.Clamp01(kbTimer / kbDuration);
            Vector2 p = Vector2.LerpUnclamped(kbFrom, kbTo, knockbackCurve.Evaluate(t));
            transform.position = new Vector3(p.x, p.y, transform.position.z);
            LastMoveFrame = Time.frameCount;

            if (t >= 1f)
            {
                IsKnockedBack = false;
                if (pendingStun > 0f)
                {
                    IsStunned = true;
                    stunTimer = pendingStun;
                    blinkTimer = 0f;
                }
                else EndStun();
            }
            return;
        }

        if (!IsStunned) return;

        stunTimer -= Time.deltaTime;

        if (blinkWhileStunned && spriteRenderer != null)
        {
            blinkTimer -= Time.deltaTime;
            if (blinkTimer <= 0f)
            {
                blinkTimer = blinkInterval;
                spriteRenderer.enabled = !spriteRenderer.enabled;
            }
        }

        if (stunTimer <= 0f) EndStun();
    }

    private void EndStun()
    {
        IsKnockedBack = false;
        IsStunned = false;
        stunTimer = 0f;
        if (spriteRenderer != null) spriteRenderer.enabled = true;
        if (stunEffect != null) stunEffect.SetActive(false);
        OnStunEnded?.Invoke(this);
    }

    /// <summary>새 게임 시작 시 초기화</summary>
    public void ResetState()
    {
        IsKnockedBack = false;
        IsStunned = false;
        stunTimer = 0f;
        pendingStun = 0f;
        if (spriteRenderer != null) spriteRenderer.enabled = true;
        if (stunEffect != null) stunEffect.SetActive(false);
    }
}