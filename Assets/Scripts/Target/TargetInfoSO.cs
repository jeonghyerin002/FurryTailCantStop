using UnityEngine;

public enum TargetType
{
    Normal,  // 일반 타겟 (강아지 퍼리) : 3점
    Special  // 미남 타겟 (늑대 퍼리) : 10점
}

/// <summary>
/// 타겟의 기본 데이터. (Project 창 > Create > EyeContact > Target Info)
/// 기획서: 강아지 3종 = Normal SO 3개 / 늑대 = Special SO 1개
/// </summary>
[CreateAssetMenu(fileName = "TargetInfo", menuName = "EyeContact/Target Info")]
public class TargetInfoSO : ScriptableObject
{
    public const int NormalScore = 3;
    public const int SpecialScore = 10;

    [Header("타겟 종류")]
    public TargetType targetType = TargetType.Normal;

    /// <summary>Love 상태로 만들었을 때 얻는 점수 (일반 3점 / 특별 10점)</summary>
    public int Score => targetType == TargetType.Special ? SpecialScore : NormalScore;

    [Header("이동 범위 (시작 위치 기준 ±값)")]
    [Tooltip("x: -5 ~ 5, y: -5 ~ 5")]
    public Vector2 moveRange = new Vector2(5f, 5f);

    [Tooltip("이동 목적지를 카메라 화면 안으로 제한 (화면 밖으로 나가는 것 방지)")]
    public bool clampToCameraView = true;
    [Tooltip("화면 가장자리 여백 (월드 단위)")]
    public float screenPadding = 0.5f;

    [Header("배회 이동")]
    public float moveSpeed = 2f;
    [Tooltip("목적지 도착 후 대기 시간 (최소, 최대)")]
    public Vector2 waitTimeRange = new Vector2(0.5f, 2f);
    public float arriveThreshold = 0.05f;

    [Header("호감도 (LovePercent)")]
    public float maxLovePercent = 100f;
    [Tooltip("1 = 플레이어 기본 시간(3초) 그대로, 2 = 두 배 오래 걸림")]
    [Min(0.1f)] public float loveResistance = 1f;
    [Tooltip("누르기를 멈췄을 때 초당 감소량(%). 0이면 감소하지 않음")]
    [Min(0f)] public float loveDecayPerSecond = 20f;

    [Header("Love 상태 (포로)")]
    [Tooltip("Love 상태가 되면 플레이어를 따라다님 (끄면 그 자리에 멈춤)")]
    public bool followPlayerOnLove = false;
    public float followDistance = 1.2f;
    public float followSpeed = 4f;

    [Header("라이벌 (여우 영애)")]
    [Tooltip("라이벌이 이 타겟을 뺏는 데 걸리는 시간 배율. 1 = RivalInfo의 stealTime 그대로")]
    [Min(0.1f)] public float rivalResistance = 1f;
    [Tooltip("라이벌에게 뺏긴 뒤 라이벌을 따라다니는 시간(초). 끝나면 처음 위치에서 다시 등장")]
    [Min(0f)] public float takenDuration = 6f;

    [Header("리스폰 (5분 동안 타겟이 고갈되지 않도록)")]
    [Tooltip("Love 상태가 된 뒤 이 시간(초)이 지나면 처음 위치에서 다시 등장. 0이면 리스폰 안 함")]
    [Min(0f)] public float respawnDelay = 12f;
}