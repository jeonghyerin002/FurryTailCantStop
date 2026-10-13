using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PlayerInfo 기반 눈빛 공격.
/// 타겟을 클릭하면 이동하지 않고 오로지 공격으로만 처리.
/// 누르고 있는 동안 LovePercent 상승 → 3초(CharmHoldTime) 유지 시 Love 상태 + 포로 +1.
/// 사거리(AttackRange) 밖이면 게이지가 오르지 않음 (빈 곳을 탭해서 직접 다가가야 함).
/// </summary>
[RequireComponent(typeof(PlayerInfo), typeof(PlayerController))]
public class PlayerAttack : MonoBehaviour
{
    [Tooltip("누르는 손가락이 타겟에서 벗어나면 공격 취소")]
    [SerializeField] private bool cancelWhenPointerLeaves = true;

    private PlayerInfo info;
    private PlayerController controller;
    private TargetMover currentTarget;
    private bool warnedOutOfRange;

    public TargetMover CurrentTarget => currentTarget;
    public bool IsCharming { get; private set; }

    private void Awake()
    {
        info = GetComponent<PlayerInfo>();
        controller = GetComponent<PlayerController>();
    }

    private void Update()
    {
        IsCharming = false;

        if (!info.IsPlaying)
        {
            currentTarget = null;
            return;
        }

        Pointer pointer = Pointer.current;
        if (pointer == null) return;

        Vector2 screenPos = pointer.position.ReadValue();

        // 누르기 시작 → 타겟 선택 + 이동 정지 (타겟 쪽으로 이동하지 않음)
        if (pointer.press.wasPressedThisFrame && controller.TryGetTargetAtScreen(screenPos, out TargetMover hit))
        {
            currentTarget = hit;
            warnedOutOfRange = false;
            controller.Stop();
        }

        if (currentTarget == null) return;

        // 손을 떼거나, 이미 Love거나, 손가락이 타겟을 벗어나면 취소
        if (!pointer.press.isPressed || currentTarget.IsLoved || PointerLeftTarget(screenPos))
        {
            currentTarget = null;
            return;
        }

        // 사거리 밖 → 게이지 상승 없음
        float dist = Vector2.Distance(transform.position, currentTarget.transform.position);
        if (dist > info.AttackRange)
        {
            if (!warnedOutOfRange)
            {
                Debug.Log($"[Attack] {currentTarget.name} 사거리 밖 (거리 {dist:F1} > {info.AttackRange})");
                warnedOutOfRange = true;
            }
            return;
        }

        // 눈빛 보내기
        IsCharming = true;
        float amount = currentTarget.Info.maxLovePercent / info.CharmHoldTime * Time.deltaTime;
        if (currentTarget.AddLove(amount, transform))
        {
            info.AddCaptive();
            currentTarget = null;
        }
    }

    private bool PointerLeftTarget(Vector2 screenPos)
    {
        if (!cancelWhenPointerLeaves) return false;
        return !controller.TryGetTargetAtScreen(screenPos, out TargetMover t) || t != currentTarget;
    }
}