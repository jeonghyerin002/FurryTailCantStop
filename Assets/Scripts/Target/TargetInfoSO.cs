using UnityEngine;

[CreateAssetMenu(fileName = "TargetInfoSO", menuName = "Target/TargetInfoSO")]
public class TargetInfoSO : ScriptableObject
{
    [Header("이동 범위")]
    public Vector2 moveRange = new Vector2(5f, 5f);

    [Header("배회 이동")]
    public float moveSpeed = 2f;
    public Vector2 waitTimeRange = new Vector2(0.5f, 2f);
    public float arriveThreshold = 0.05f;

    [Header("호감도")]
    public float maxLovePercent = 100f;
    [Min(0.1f)] public float loveResistance = 1f;
    [Tooltip("누르기를 멈췄을 때 초당 감소량(%). 0이면 감소하지 않음")]
    [Min(0f)] public float loveDecayPerSecond = 20f;

    [Header("Love 상태 (포로)")]
    [Tooltip("Love 상태가 되면 플레이어를 따라다님")]
    public bool followPlayerOnLove = true;
    public float followDistance = 1.2f;
    public float followSpeed = 4f;
}
