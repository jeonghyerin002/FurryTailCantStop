using UnityEngine;

// ─────────────────────────────────────────────────────────────
// 카메라 관련 코드 모음 (이 파일 하나만 있으면 됨)
//  - CameraManager : 플레이어 따라가기 + 벽 안쪽으로 제한 (Main Camera에 붙임)
//  - CameraBounds  : 다른 스크립트들이 "맵 밖으로 못 나가게" 할 때 쓰는 도우미
// ─────────────────────────────────────────────────────────────

/// <summary>
/// 플레이어를 부드럽게 따라가는 카메라.
/// 지정한 벽(BoxCollider2D 4개)에 카메라 화면 끝이 닿으면 더 이상 밖으로 나가지 않고,
/// 플레이어가 다시 안쪽으로 이동하면 원래 위치(플레이어 중심)로 자연스럽게 돌아옴.
/// Main Camera에 붙이세요.
/// </summary>
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(-50)] // 다른 스크립트가 CameraBounds를 쓰기 전에 벽 영역을 먼저 계산
public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    [Header("따라갈 대상")]
    [Tooltip("비워두면 씬의 Player(PlayerController)를 자동으로 찾음")]
    [SerializeField] private Transform target;
    [Tooltip("시작할 때 Player 자식에서 빼서 독립적으로 움직이게 함 (부드러운 이동에 필요)")]
    [SerializeField] private bool detachFromParent = true;
    [Tooltip("끄면 시작 시 '카메라 위치 - 플레이어 위치'를 기본 위치로 사용")]
    [SerializeField] private bool useCustomOffset = false;
    [SerializeField] private Vector3 customOffset = new Vector3(0f, 0f, -10f);

    [Header("부드러움")]
    [Tooltip("작을수록 빠르게 따라감")]
    [SerializeField] private float smoothTime = 0.2f;
    [Tooltip("이 거리 이상 떨어지면 부드럽게 따라가지 않고 바로 이동 (게임 재시작 등)")]
    [SerializeField] private float snapDistance = 10f;

    [Header("벽 (카메라가 넘지 못하는 박스 콜라이더 4개)")]
    [SerializeField] private BoxCollider2D[] walls;
    [Tooltip("벽 안쪽으로 더 띄울 여백")]
    [SerializeField] private float edgePadding = 0f;

    /// <summary>벽이 하나라도 지정되어 있는지</summary>
    public bool HasBounds { get; private set; }

    private Camera cam;
    private Vector3 offset;
    private Vector3 velocity;
    private float minX = float.NegativeInfinity;
    private float maxX = float.PositiveInfinity;
    private float minY = float.NegativeInfinity;
    private float maxY = float.PositiveInfinity;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        cam = GetComponent<Camera>();

        if (target == null)
        {
            PlayerController p = FindFirstObjectByType<PlayerController>();
            if (p != null) target = p.transform;
        }

        if (target != null)
            offset = useCustomOffset ? customOffset : transform.position - target.position;

        if (detachFromParent && transform.parent != null)
            transform.SetParent(null, true);

        RecalculateBounds();
    }

    private void Start()
    {
        SnapToTarget();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // 원래 있어야 할 위치(플레이어 + 기본 오프셋)를 벽 안쪽으로 제한
        Vector3 desired = ClampCamera(target.position + offset);

        // 너무 멀면 순간이동 (재시작 등)
        if ((desired - transform.position).sqrMagnitude > snapDistance * snapDistance)
        {
            transform.position = desired;
            velocity = Vector3.zero;
            return;
        }

        // 부드럽게 이동: 벽에 닿으면 자연스럽게 멈추고, 벗어나면 다시 플레이어 중심으로 돌아옴
        Vector3 next = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
        next = ClampCamera(next);
        next.z = desired.z;
        transform.position = next;
    }

    /// <summary>카메라를 즉시 플레이어 위치로 (새 게임 시작 시)</summary>
    public void SnapToTarget()
    {
        if (target == null) return;
        transform.position = ClampCamera(target.position + offset);
        velocity = Vector3.zero;
    }

    #region 벽 영역

    /// <summary>
    /// 벽 4개로 둘러싸인 안쪽 영역 계산.
    /// 세로로 긴 벽 = 왼쪽/오른쪽 벽, 가로로 긴 벽 = 위/아래 벽으로 자동 판단.
    /// 플레이 중에 벽을 옮겼다면 컴포넌트 ⋮ 메뉴에서 다시 실행.
    /// </summary>
    [ContextMenu("벽 영역 다시 계산")]
    public void RecalculateBounds()
    {
        minX = minY = float.NegativeInfinity;
        maxX = maxY = float.PositiveInfinity;
        HasBounds = false;
        if (walls == null) return;

        // 벽들의 중심점 (각 벽이 어느 쪽인지 판단하는 기준)
        Vector2 mid = Vector2.zero;
        int count = 0;
        foreach (BoxCollider2D w in walls)
        {
            if (w == null) continue;
            mid += GetWorldRect(w).center;
            count++;
        }
        if (count == 0) return;
        mid /= count;

        foreach (BoxCollider2D w in walls)
        {
            if (w == null) continue;
            Rect r = GetWorldRect(w);
            bool vertical = r.height >= r.width;

            if (vertical)
            {
                if (r.center.x < mid.x) minX = Mathf.Max(minX, r.xMax); // 왼쪽 벽
                else maxX = Mathf.Min(maxX, r.xMin);                     // 오른쪽 벽
            }
            else
            {
                if (r.center.y < mid.y) minY = Mathf.Max(minY, r.yMax); // 아래 벽
                else maxY = Mathf.Min(maxY, r.yMin);                     // 위 벽
            }
        }

        minX += edgePadding;
        maxX -= edgePadding;
        minY += edgePadding;
        maxY -= edgePadding;
        HasBounds = true;
    }

    /// <summary>
    /// BoxCollider2D의 월드 영역 (물리 초기화 전에도 정확하도록 직접 계산)
    /// </summary>
    private static Rect GetWorldRect(BoxCollider2D box)
    {
        Transform t = box.transform;
        Vector2 half = box.size * 0.5f;
        Vector2 o = box.offset;

        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < 4; i++)
        {
            Vector2 local = o + new Vector2(i % 2 == 0 ? -half.x : half.x, i < 2 ? -half.y : half.y);
            Vector2 world = t.TransformPoint(local);
            min = Vector2.Min(min, world);
            max = Vector2.Max(max, world);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    /// <summary>카메라 중심 좌표를 '화면 끝이 벽을 넘지 않는' 위치로 제한</summary>
    private Vector3 ClampCamera(Vector3 pos)
    {
        if (!HasBounds || cam == null || !cam.orthographic) return pos;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        pos.x = ClampAxis(pos.x, minX + halfW, maxX - halfW);
        pos.y = ClampAxis(pos.y, minY + halfH, maxY - halfH);
        return pos;
    }

    /// <summary>
    /// 월드 좌표를 벽 안쪽(맵 영역)으로 제한.
    /// CameraBounds.Clamp가 이걸 사용 → 타겟/라이벌/플레이어/넉백이 맵 밖으로 나가지 않음.
    /// </summary>
    public Vector2 ClampToPlayArea(Vector2 pos, float padding)
    {
        if (!HasBounds) return pos;
        pos.x = ClampAxis(pos.x, minX + padding, maxX - padding);
        pos.y = ClampAxis(pos.y, minY + padding, maxY - padding);
        return pos;
    }

    /// <summary>영역이 화면보다 좁으면 가운데에 고정</summary>
    private static float ClampAxis(float v, float min, float max)
    {
        if (min > max) return (min + max) * 0.5f;
        return Mathf.Clamp(v, min, max);
    }

    #endregion

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) RecalculateBounds();
        if (!HasBounds) return;
        if (float.IsInfinity(minX) || float.IsInfinity(maxX) || float.IsInfinity(minY) || float.IsInfinity(maxY)) return;

        // 초록 = 플레이 영역(벽 안쪽)
        Gizmos.color = Color.green;
        Vector3 c = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);
        Gizmos.DrawWireCube(c, new Vector3(maxX - minX, maxY - minY, 0f));

        // 노랑 = 카메라가 실제로 이동할 수 있는 중심 범위
        Camera c2 = cam != null ? cam : GetComponent<Camera>();
        if (c2 == null || !c2.orthographic) return;
        float halfH = c2.orthographicSize;
        float halfW = halfH * c2.aspect;
        float w = Mathf.Max(0f, (maxX - minX) - halfW * 2f);
        float h = Mathf.Max(0f, (maxY - minY) - halfH * 2f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(c, new Vector3(w, h, 0f));
    }
}

/// <summary>
/// 좌표를 맵 영역 안으로 제한하는 도우미. (컴포넌트로 붙이는 스크립트 아님)
/// CameraManager에 벽이 지정되어 있으면 → 벽 안쪽(맵 전체)으로 제한
/// 없으면 → 지금 카메라에 보이는 화면 안으로 제한
/// TargetMover, RivalAI, StunController, PlayerController가 사용.
/// </summary>
public static class CameraBounds
{
    public static Vector2 Clamp(Vector2 pos, float padding)
    {
        CameraManager follow = CameraManager.Instance;
        if (follow != null && follow.HasBounds) return follow.ClampToPlayArea(pos, padding);

        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return pos;

        float halfH = Mathf.Max(0f, cam.orthographicSize - padding);
        float halfW = Mathf.Max(0f, cam.orthographicSize * cam.aspect - padding);
        Vector2 c = cam.transform.position;
        return new Vector2(
            Mathf.Clamp(pos.x, c.x - halfW, c.x + halfW),
            Mathf.Clamp(pos.y, c.y - halfH, c.y + halfH));
    }
}