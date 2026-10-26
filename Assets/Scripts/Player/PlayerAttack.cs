using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PlayerInfo 기반 눈빛 공격.
/// 타겟을 꾹 누르면 이동이 멈추고 눈빛 빔 발사 → 하트 게이지 상승 → 완충 시 유혹 성공.
/// 사거리(AttackRange) 밖이면 게이지가 오르지 않음 (빈 곳을 탭해서 직접 다가가야 함).
/// 라이벌이 같은 타겟에 눈빛을 쏘고 있으면 DuelManager로 연타 대결 시작.
/// 피버 중엔 차징 속도 x3.
/// </summary>
[RequireComponent(typeof(PlayerInfo), typeof(PlayerController))]
public class PlayerAttack : MonoBehaviour
{
    [Tooltip("누르는 손가락이 타겟에서 벗어나면 공격 취소")]
    [SerializeField] private bool cancelWhenPointerLeaves = true;
    [Tooltip("플레이어 자식(눈 위치)에 있는 EyeBeam. 비워두면 자식에서 자동 탐색")]
    [SerializeField] private EyeBeam beam;

    private PlayerInfo info;
    private PlayerController controller;
    private StunController stun;
    private TargetMover currentTarget;
    private bool warnedOutOfRange;

    public TargetMover CurrentTarget => currentTarget;
    public bool IsCharming { get; private set; }
    public StunController Stun => stun;

    /// <summary>타겟을 유혹(포로)하는 데 성공했을 때</summary>
    public event Action<TargetMover> OnCaptured;

    private void Awake()
    {
        info = GetComponent<PlayerInfo>();
        controller = GetComponent<PlayerController>();
        stun = GetComponent<StunController>();
        if (beam == null) beam = GetComponentInChildren<EyeBeam>(true);
    }

    private void Update()
    {
        IsCharming = false;

        if (!info.IsPlaying || (stun != null && stun.IsBusy))
        {
            CancelAttack();
            return;
        }

        // 연타 대결 중: 입력은 DuelManager가 처리, 빔만 유지
        if (DuelManager.IsActive)
        {
            currentTarget = null;
            DuelManager dm = DuelManager.Instance;
            if (dm.Player == this && dm.Target != null) ShowBeam(dm.Target.transform);
            else HideBeam();
            return;
        }

        // 대결 직후 잠깐 입력 무시 (연타가 이동/공격으로 새는 것 방지)
        if (DuelManager.BlocksInput)
        {
            CancelAttack();
            return;
        }

        Pointer pointer = Pointer.current;
        if (pointer == null)
        {
            CancelAttack();
            return;
        }

        Vector2 screenPos = pointer.position.ReadValue();

        // 누르기 시작 → 타겟 선택 + 이동 정지
        if (pointer.press.wasPressedThisFrame && controller.TryGetTargetAtScreen(screenPos, out TargetMover hit))
        {
            currentTarget = hit;
            warnedOutOfRange = false;
            controller.Stop();
        }

        if (currentTarget == null)
        {
            HideBeam();
            return;
        }

        // 손을 떼거나, 이미 Love/Taken이거나, 손가락이 타겟을 벗어나면 취소
        if (!pointer.press.isPressed || !currentTarget.IsAvailable || PointerLeftTarget(screenPos))
        {
            CancelAttack();
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
            HideBeam();
            return;
        }

        // 라이벌이 같은 타겟에 눈빛을 쏘고 있음 → 연타 대결 (피버 중이면 즉시 승리)
        RivalAI rival = currentTarget.ClaimingRival;
        if (rival != null && rival.IsCharming(currentTarget) && DuelManager.Instance != null)
        {
            TargetMover duelTarget = currentTarget;
            currentTarget = null;
            if (DuelManager.Instance.StartDuel(this, rival, duelTarget)) return;
            currentTarget = duelTarget; // 대결 시작 실패 시 그냥 계속 차징
        }

        // 눈빛 보내기
        IsCharming = true;
        ShowBeam(currentTarget.transform);

        float amount = currentTarget.Info.maxLovePercent / info.CharmHoldTime * Time.deltaTime
                       * FeverManager.CharmMultiplier; // 피버: 차징 속도 3배
        TargetMover target = currentTarget;
        if (target.AddLove(amount, transform))
        {
            currentTarget = null;
            HideBeam();
            HandleCaptured(target);
        }
    }

    /// <summary>연타 대결 승리 시 DuelManager가 호출</summary>
    public bool CaptureByDuel(TargetMover target)
    {
        if (target == null || !target.ForceLove(transform)) return false;
        HandleCaptured(target);
        return true;
    }

    public void CancelAttack()
    {
        currentTarget = null;
        HideBeam();
    }

    private void HandleCaptured(TargetMover target)
    {
        info.AddCaptive();
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.AddScore(target.Info.Score); // 일반 3점 / 특별 10점
        if (FeverManager.Instance != null)
            FeverManager.Instance.AddGauge(target.Info.targetType); // 매력 게이지 상승
        OnCaptured?.Invoke(target);
    }

    private bool PointerLeftTarget(Vector2 screenPos)
    {
        if (!cancelWhenPointerLeaves) return false;
        return !controller.TryGetTargetAtScreen(screenPos, out TargetMover t) || t != currentTarget;
    }

    private void ShowBeam(Transform target)
    {
        if (beam != null) beam.Show(target);
    }

    private void HideBeam()
    {
        if (beam != null && beam.IsShowing) beam.Hide();
    }
}