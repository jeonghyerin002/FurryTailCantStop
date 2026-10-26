using UnityEngine;

/// <summary>
/// 눈빛 빔 연출 (LineRenderer).
/// ※ SpriteRenderer와 LineRenderer는 같은 오브젝트에 함께 붙일 수 없으므로
///   캐릭터의 "자식" 오브젝트를 눈 위치에 만들고 거기에 붙이세요. (자식 위치 = 빔 시작점)
/// 플레이어(고양이)와 라이벌(여우) 모두 사용.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class EyeBeam : MonoBehaviour
{
    [Header("모양")]
    [SerializeField] private Color startColor = new Color(1f, 0.35f, 0.7f, 1f);
    [SerializeField] private Color endColor = new Color(1f, 0.8f, 0.95f, 0.6f);
    [SerializeField] private float baseWidth = 0.12f;
    [SerializeField] private float pulseAmount = 0.05f;
    [SerializeField] private float pulseSpeed = 14f;
    [Tooltip("타겟 피벗(발밑)에서 얼굴 쪽으로 더하는 오프셋")]
    [SerializeField] private Vector2 targetOffset = new Vector2(0f, 0.5f);

    [Header("렌더링")]
    [Tooltip("비워두면 Sprite-Unlit-Default(URP) 또는 Sprites/Default 셰이더로 자동 생성")]
    [SerializeField] private Material beamMaterial;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 50;

    [Header("타겟 위치 이펙트 (선택: 하트 파티클 등)")]
    [SerializeField] private GameObject hitEffect;

    private LineRenderer line;
    private Transform target;

    public bool IsShowing { get; private set; }

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.numCapVertices = 4;

        if (beamMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null) beamMaterial = new Material(shader);
        }
        if (beamMaterial != null) line.sharedMaterial = beamMaterial;

        line.startColor = startColor;
        line.endColor = endColor;
        line.sortingLayerName = sortingLayerName;
        line.sortingOrder = sortingOrder;

        Hide();
    }

    public void Show(Transform newTarget)
    {
        if (newTarget == null)
        {
            Hide();
            return;
        }

        target = newTarget;
        if (!IsShowing)
        {
            IsShowing = true;
            line.enabled = true;
            if (hitEffect != null) hitEffect.SetActive(true);
        }
        UpdateLine();
    }

    public void Hide()
    {
        IsShowing = false;
        target = null;
        if (line != null) line.enabled = false;
        if (hitEffect != null) hitEffect.SetActive(false);
    }

    private void LateUpdate()
    {
        if (!IsShowing) return;
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            Hide();
            return;
        }
        UpdateLine();
    }

    private void UpdateLine()
    {
        Vector3 from = transform.position;
        Vector3 to = target.position + (Vector3)targetOffset;
        to.z = from.z;

        line.SetPosition(0, from);
        line.SetPosition(1, to);

        float w = baseWidth + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        line.startWidth = Mathf.Max(0.01f, w);
        line.endWidth = Mathf.Max(0.01f, w * 0.6f);

        if (hitEffect != null) hitEffect.transform.position = to;
    }
}