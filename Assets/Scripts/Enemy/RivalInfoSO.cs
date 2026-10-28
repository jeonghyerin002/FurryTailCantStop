using UnityEngine;

/// <summary>
/// 라이벌(여우 영애 퍼리) 기본 데이터. (Project 창 > Create > EyeContact > Rival Info)
/// </summary>
[CreateAssetMenu(fileName = "RivalInfo", menuName = "EyeContact/Rival Info")]
public class RivalInfoSO : ScriptableObject
{
    [Header("이동")]
    public float moveSpeed = 2f;
    [Tooltip("타겟에게 다가갈 때 속도")]
    public float approachSpeed = 3.2f;
    [Tooltip("Hunter 모드 배회 범위 (시작 위치 기준 ±값)")]
    public Vector2 wanderRange = new Vector2(4f, 3f);
    [Tooltip("목적지 도착 후 대기 시간 (최소, 최대)")]
    public Vector2 waitTimeRange = new Vector2(0.5f, 2f);
    public float arriveThreshold = 0.05f;
    public bool clampToCameraView = true;
    public float screenPadding = 0.5f;

    [Header("Guard 모드 (미남 늑대 주변 점유)")]
    [Tooltip("지키는 타겟 주변을 맴도는 반경")]
    public float guardRadius = 1.8f;

    [Header("공격 조건")]
    [Tooltip("켜면 플레이어가 먼저 눈빛을 쏜 타겟에만 끼어듦. 혼자서 타겟을 노리거나 뺏지 않음")]
    public bool attackOnlyAfterPlayer = true;

    [Header("Hunter 모드 (다른 타겟을 노리고 접근)")]
    [Tooltip("이 거리 안의 타겟을 찾아서 노림")]
    public float detectRange = 6f;
    [Tooltip("특별 타겟(늑대)도 직접 사냥할지")]
    public bool canHuntSpecial = false;
    [Tooltip("사냥 성공/포기 후 다음 사냥까지 대기 (최소, 최대)")]
    public Vector2 huntCooldownRange = new Vector2(4f, 8f);
    [Tooltip("이 시간 안에 타겟에게 닿지 못하면 포기")]
    public float giveUpTime = 6f;

    [Header("개입 (플레이어가 눈빛을 쏘는 타겟에 끼어들기)")]
    [Tooltip("Guard: 지키는 타겟 기준 / Hunter: 라이벌 자신 기준 거리")]
    public float interveneRange = 5f;

    [Header("눈빛")]
    [Tooltip("이 거리 안에 들어가야 눈빛을 쏨")]
    public float charmRange = 2.5f;
    [Tooltip("혼자서 타겟을 뺏는 데 걸리는 시간(초)")]
    public float stealTime = 6f;

    [Header("연타 대결")]
    [Tooltip("라이벌의 초당 연타 수 (높을수록 어려움)")]
    public float tapsPerSecond = 6f;
    [Tooltip("연타 간격 랜덤 폭")]
    [Range(0f, 0.9f)] public float tapJitter = 0.35f;

    [Header("타겟을 뺏은 뒤 퇴장")]
    [Tooltip("뺏은 타겟을 데리고 화면 밖으로 걸어나가는 속도 (터벅터벅 느리게)")]
    public float leaveSpeed = 1.6f;
    [Tooltip("화면 가장자리에서 이만큼 더 나가면 사라짐 (월드 단위)")]
    public float leaveMargin = 1.5f;
    [Tooltip("이 시간이 지나면 화면 안이어도 사라짐 (안전장치)")]
    public float leaveTimeout = 12f;

    [Header("기절 후")]
    [Tooltip("기절이 풀린 뒤 다시 개입/사냥하기까지 대기(초)")]
    public float recoverCooldown = 2f;
}