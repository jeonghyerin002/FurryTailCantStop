using UnityEngine;

/// <summary>
/// 이동 방향에 따라 스프라이트 좌우 반전.
/// x축 + (오른쪽)으로 이동 → 원본 이미지
/// x축 - (왼쪽)으로 이동 → 좌우 반전
/// 멈춰 있을 때는 마지막으로 바라본 방향 유지.
/// 플레이어, 타겟(강아지/늑대), 라이벌(여우) 모두에 붙이세요.
/// ※ 원본 이미지는 오른쪽을 보고 있어야 합니다.
/// </summary>
[DisallowMultipleComponent]
public class SpriteFacing : MonoBehaviour
{
    [Tooltip("비워두면 자식에서 자동으로 찾음")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Tooltip("이 값보다 적게 움직이면 방향을 바꾸지 않음 (제자리 떨림 방지)")]
    [SerializeField] private float minMoveX = 0.001f;
    [Tooltip("원본 이미지가 왼쪽을 보고 있다면 체크")]
    [SerializeField] private bool originalFacesLeft = false;
    [Tooltip("튕겨 나가는 중(넉백/기절)에는 방향을 바꾸지 않음")]
    [SerializeField] private bool ignoreWhileStunned = true;

    /// <summary>지금 오른쪽을 보고 있는지</summary>
    public bool IsFacingRight { get; private set; } = true;

    private StunController stun;
    private float lastX;

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        stun = GetComponent<StunController>();
        lastX = transform.position.x;
    }

    private void OnEnable()
    {
        lastX = transform.position.x;
    }

    // 모든 이동이 끝난 뒤(LateUpdate)에 이번 프레임 실제로 움직인 방향을 확인
    private void LateUpdate()
    {
        float x = transform.position.x;
        float dx = x - lastX;
        lastX = x;

        if (spriteRenderer == null) return;
        if (ignoreWhileStunned && stun != null && stun.IsBusy) return;

        if (dx > minMoveX) SetFacing(true);        // 오른쪽 이동 → 원본
        else if (dx < -minMoveX) SetFacing(false); // 왼쪽 이동 → 반전
    }

    /// <summary>방향을 직접 지정 (true = 오른쪽)</summary>
    public void SetFacing(bool faceRight)
    {
        IsFacingRight = faceRight;
        spriteRenderer.flipX = faceRight ? originalFacesLeft : !originalFacesLeft;
    }
}