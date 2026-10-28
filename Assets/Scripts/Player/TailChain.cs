using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 뒤로 유혹한 캐릭터들이 한 줄로 따라오게 하는 "꼬리 줄".
/// 플레이어가 지나간 길을 기록하고, 각 캐릭터는 그 길 위의 자기 자리(간격만큼 뒤)를 따라감.
/// 먼저 유혹한 캐릭터가 앞, 나중에 유혹한 캐릭터가 그 뒤.
/// 캐릭터끼리 너무 가까우면 서로 살짝 밀어내서 겹치지 않음.
/// Player에 붙이세요. (없으면 첫 유혹 때 자동으로 붙음)
/// </summary>
[DisallowMultipleComponent]
public class TailChain : MonoBehaviour
{
    [Header("간격")]
    [Tooltip("플레이어와 첫 번째 캐릭터 사이 간격")]
    [SerializeField] private float firstSpacing = 1.2f;
    [Tooltip("따라오는 캐릭터끼리의 간격")]
    [SerializeField] private float spacing = 1.1f;

    [Header("따라오는 속도")]
    [Tooltip("자기 자리에서 멀어질수록 이 배율만큼 빨라짐 (피버로 빨라진 플레이어도 따라잡음)")]
    [SerializeField] private float catchUpFactor = 6f;

    [Header("겹침 방지")]
    [Tooltip("캐릭터끼리(플레이어 포함) 이 거리보다 가까우면 서로 밀어냄")]
    [SerializeField] private float minSeparation = 0.8f;
    [SerializeField] private float separationStrength = 4f;

    [Header("경로 기록")]
    [Tooltip("플레이어가 이만큼 움직일 때마다 경로에 점을 찍음")]
    [SerializeField] private float recordStep = 0.05f;

    private readonly List<TargetMover> followers = new List<TargetMover>();
    // [0] = 가장 최근 지점(플레이어 쪽), 뒤로 갈수록 오래된 지점
    private readonly List<Vector2> path = new List<Vector2>();

    public IReadOnlyList<TargetMover> Followers => followers;
    public int Count => followers.Count;
    public float CatchUpFactor => catchUpFactor;

    private void Awake()
    {
        path.Add(transform.position);
    }

    private void LateUpdate()
    {
        RecordPath();
        TrimPath();
    }

    #region 줄 관리 (TargetMover가 호출)

    /// <summary>유혹 성공 → 줄 맨 뒤에 추가</summary>
    public void Add(TargetMover target)
    {
        if (target == null || followers.Contains(target)) return;
        followers.Add(target);

        // 아직 경로가 짧으면, 새 캐릭터가 서 있는 곳까지 경로를 이어 붙여서 거기서부터 자연스럽게 따라오게 함
        if (PathLength() < SlotDistance(followers.Count - 1))
            path.Add(target.transform.position);
    }

    /// <summary>새 게임 시작 등으로 줄에서 빠짐</summary>
    public void Remove(TargetMover target)
    {
        followers.Remove(target);
        if (followers.Count == 0)
        {
            path.Clear();
            path.Add(transform.position);
        }
    }

    /// <summary>이 캐릭터가 서 있어야 할 자리 (플레이어가 지나간 길 위)</summary>
    public Vector2 GetSlotPosition(TargetMover target)
    {
        int index = followers.IndexOf(target);
        if (index < 0) return target.transform.position;
        return SampleAtDistance(SlotDistance(index));
    }

    /// <summary>주변 캐릭터(플레이어 포함)와 겹치지 않게 밀어내는 힘</summary>
    public Vector2 GetSeparation(TargetMover target)
    {
        Vector2 pos = target.transform.position;
        Vector2 push = Push(pos, transform.position);
        foreach (TargetMover other in followers)
        {
            if (other == null || other == target) continue;
            push += Push(pos, other.transform.position);
        }
        return push;
    }

    #endregion

    #region 경로

    private float SlotDistance(int index) => firstSpacing + spacing * index;

    private void RecordPath()
    {
        Vector2 p = transform.position;
        if (path.Count == 0 || (p - path[0]).sqrMagnitude >= recordStep * recordStep)
            path.Insert(0, p);
    }

    /// <summary>줄 길이보다 너무 오래된 경로는 버림</summary>
    private void TrimPath()
    {
        float keep = SlotDistance(Mathf.Max(0, followers.Count)) + 2f;
        Vector2 prev = transform.position;
        float total = 0f;
        for (int i = 0; i < path.Count; i++)
        {
            total += Vector2.Distance(prev, path[i]);
            prev = path[i];
            if (total > keep && i + 1 < path.Count)
            {
                path.RemoveRange(i + 1, path.Count - (i + 1));
                break;
            }
        }
    }

    private float PathLength()
    {
        float total = 0f;
        Vector2 prev = transform.position;
        foreach (Vector2 p in path)
        {
            total += Vector2.Distance(prev, p);
            prev = p;
        }
        return total;
    }

    /// <summary>플레이어 위치에서 경로를 따라 distance만큼 뒤로 간 지점</summary>
    private Vector2 SampleAtDistance(float distance)
    {
        Vector2 prev = transform.position;
        float remaining = distance;
        foreach (Vector2 p in path)
        {
            float seg = Vector2.Distance(prev, p);
            if (seg > 0f && remaining <= seg) return Vector2.Lerp(prev, p, remaining / seg);
            remaining -= seg;
            prev = p;
        }
        return prev; // 경로가 짧으면 가장 오래된 지점
    }

    private Vector2 Push(Vector2 self, Vector2 other)
    {
        Vector2 d = self - other;
        float m = d.magnitude;
        if (m >= minSeparation) return Vector2.zero;
        Vector2 dir = m > 0.001f ? d / m : Random.insideUnitCircle.normalized;
        return dir * ((minSeparation - m) / minSeparation) * separationStrength;
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        if (path.Count == 0) return;
        Gizmos.color = Color.magenta;
        Vector2 prev = transform.position;
        foreach (Vector2 p in path)
        {
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
        for (int i = 0; i < followers.Count; i++)
            Gizmos.DrawWireSphere(SampleAtDistance(SlotDistance(i)), 0.15f);
    }
}