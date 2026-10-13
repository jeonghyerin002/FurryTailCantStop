using System;
using UnityEngine;

/// <summary>
/// 플레이어 데이터 (이동/공격 수치, 포로 수). 점수는 ScoreManager가 관리.
/// PlayerController, PlayerAttack이 이 값을 참조함.
/// </summary>
public class PlayerInfo : MonoBehaviour
{
    [Header("이동")]
    [SerializeField] private float walkSpeed = 5f;
    [Tooltip("먼 거리일 때 walkSpeed에 더해지는 속도")]
    [SerializeField] private float runBonusSpeed = 2f;
    [Tooltip("이 거리 이하면 걷기, 초과면 달리기")]
    [SerializeField] private float walkDistance = 5f;

    [Header("공격 (눈빛 보내기)")]
    [Tooltip("타겟을 Love 상태로 만드는 데 필요한 누르기 시간(초)")]
    [SerializeField] private float charmHoldTime = 3f;
    [Tooltip("이 거리 안에 있어야 LovePercent가 오름")]
    [SerializeField] private float attackRange = 3f;
    [SerializeField] private LayerMask targetLayer = ~0;

    public float WalkSpeed => walkSpeed;
    public float RunSpeed => walkSpeed + runBonusSpeed;
    public float WalkDistance => walkDistance;
    public float CharmHoldTime => charmHoldTime;
    public float AttackRange => attackRange;
    public LayerMask TargetLayer => targetLayer;

    /// <summary>현재 판에서 잡은 포로 수</summary>
    public int Captives { get; private set; }

    /// <summary>false면 입력/이동/공격 모두 정지 (UIManager가 게임 시작/종료 시 설정)</summary>
    public bool IsPlaying { get; private set; } = true;

    public event Action<int> OnCaptiveChanged;

    /// <summary>거리에 따라 걷기(5) / 달리기(5+2) 속도 반환</summary>
    public float GetMoveSpeed(float distance)
    {
        return distance <= walkDistance ? WalkSpeed : RunSpeed;
    }

    public void AddCaptive()
    {
        Captives++;
        OnCaptiveChanged?.Invoke(Captives);
    }

    public void SetPlaying(bool playing)
    {
        IsPlaying = playing;
    }

    public void ResetCaptives()
    {
        Captives = 0;
        OnCaptiveChanged?.Invoke(Captives);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, walkDistance);
    }
}