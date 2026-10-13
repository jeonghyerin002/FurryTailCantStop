using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// PlayerInfo 기반 탭 이동.
/// 탭한 지점까지의 거리가 5 이하면 걷기(5), 초과면 달리기(5+2).
/// 타겟을 탭한 경우는 PlayerAttack이 처리하므로 이동하지 않음.
/// </summary>
[RequireComponent(typeof(PlayerInfo))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float arriveThreshold = 0.05f;
    [Tooltip("타겟 판정 여유 반경 (손가락으로 정확히 누르기 어려운 것 보정)")]
    [SerializeField] private float tapRadius = 0.3f;
    [Tooltip("탭 판정 결과(타겟/빈 곳)를 Console에 출력")]
    [SerializeField] private bool debugLog = true;

    private PlayerInfo info;
    private PlayerAttack attack;
    private Vector3 expectedPos;
    private float lastWarnTime = -10f;
    private Vector2 destination;
    private float currentSpeed;
    private bool isMoving;

    public bool IsMoving => isMoving;
    public bool IsRunning => isMoving && currentSpeed > info.WalkSpeed;

    private void Awake()
    {
        info = GetComponent<PlayerInfo>();
        attack = GetComponent<PlayerAttack>();
        if (cam == null) cam = Camera.main;
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        destination = transform.position;
        expectedPos = transform.position;
    }

    private void Update()
    {
        if (!info.IsPlaying)
        {
            isMoving = false;
            return;
        }

        HandleTap();

        // 타겟을 공격(차징) 중이면 절대 이동하지 않음
        if (attack != null && attack.CurrentTarget != null) Stop();

        Move();
        expectedPos = transform.position;
    }

    private void LateUpdate()
    {
        // 이 스크립트 말고 다른 무언가가 플레이어를 움직이는지 감지
        if (debugLog && (transform.position - expectedPos).sqrMagnitude > 0.000001f && Time.time - lastWarnTime > 1f)
        {
            lastWarnTime = Time.time;
            Debug.LogWarning("[PlayerController] 다른 스크립트(또는 Rigidbody/Animator)가 플레이어를 움직이고 있어요! " +
                             "TapArea, Target에 붙은 예전 스크립트, Animator의 Apply Root Motion 등을 확인하세요.", this);
        }
        expectedPos = transform.position;
    }

    private void HandleTap()
    {
        Pointer pointer = Pointer.current; // 마우스/터치 공통
        if (pointer == null || !pointer.press.wasPressedThisFrame) return;

        Vector2 screenPos = pointer.position.ReadValue();

        // 타겟을 탭했다면 이동하지 않음 (PlayerAttack 담당)
        if (TryGetTargetAtScreen(screenPos, out _, logResult: true))
        {
            Stop();
            return;
        }

        // UI(버튼 등) 위를 눌렀으면 이동하지 않음
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        MoveTo(ScreenToWorld(screenPos));
    }

    /// <summary>목적지 설정 + 거리에 따라 속도 결정</summary>
    public void MoveTo(Vector2 targetPos)
    {
        destination = targetPos;
        float distance = Vector2.Distance(transform.position, targetPos);
        currentSpeed = info.GetMoveSpeed(distance);
        isMoving = distance > arriveThreshold;
    }

    /// <summary>속도는 유지한 채 목적지만 갱신 (움직이는 타겟 추적용)</summary>
    public void UpdateDestination(Vector2 targetPos)
    {
        if (!isMoving)
        {
            MoveTo(targetPos);
            return;
        }
        destination = targetPos;
    }

    public void Stop()
    {
        isMoving = false;
        destination = transform.position;
    }

    private void Move()
    {
        if (!isMoving) return;

        Vector2 current = transform.position;
        Vector2 next = Vector2.MoveTowards(current, destination, currentSpeed * Time.deltaTime);
        transform.position = new Vector3(next.x, next.y, transform.position.z);

        if (spriteRenderer != null && Mathf.Abs(destination.x - current.x) > 0.01f)
            spriteRenderer.flipX = destination.x < current.x;

        if (Vector2.Distance(next, destination) <= arriveThreshold)
            isMoving = false;
    }

    public Vector2 ScreenToWorld(Vector2 screenPos)
    {
        float depth = Mathf.Abs(cam.transform.position.z - transform.position.z);
        return cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, depth));
    }

    /// <summary>
    /// 화면 좌표에 아직 Love 상태가 아닌 타겟이 있는지 검사.
    /// 레이어와 상관없이 모든 콜라이더를 검사하고 TargetMover가 있는 것만 타겟으로 인정.
    /// (콜라이더가 자식에 있어도 부모의 TargetMover를 찾음, 2D/3D 콜라이더 모두 지원)
    /// </summary>
    public bool TryGetTargetAtScreen(Vector2 screenPos, out TargetMover target, bool logResult = false)
    {
        Vector2 worldPos = ScreenToWorld(screenPos);

        // 1) 2D 콜라이더
        Collider2D[] hits2D = Physics2D.OverlapCircleAll(worldPos, tapRadius);
        foreach (Collider2D hit in hits2D)
        {
            target = hit.GetComponentInParent<TargetMover>();
            if (target != null && !target.IsLoved)
            if (target != null && !target.IsLoved)
            {
                if (debugLog && logResult) Debug.Log($"[Tap] 타겟 클릭: {target.name}");
                return true;
            }
        }

        // 2) 3D 콜라이더
        RaycastHit[] hits3D = Physics.RaycastAll(cam.ScreenPointToRay(screenPos), 1000f);
        foreach (RaycastHit hit in hits3D)
        {
            target = hit.collider.GetComponentInParent<TargetMover>();
            if (target != null && !target.IsLoved)
            {
                if (debugLog && logResult) Debug.Log($"[Tap] 타겟 클릭: {target.name}");
                return true;
            }
        }

        if (debugLog && logResult)
            Debug.Log($"[Tap] 빈 곳 클릭 world={worldPos} (2D 충돌 {hits2D.Length}개, 3D 충돌 {hits3D.Length}개)");

        target = null;
        return false;
    }
}