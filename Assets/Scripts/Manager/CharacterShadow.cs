using UnityEngine;

/// <summary>
/// 캐릭터 발밑에 부드러운 타원 그림자를 깔아줌.
/// 그림자 이미지는 코드로 만들어서 따로 준비할 필요 없음.
/// 캐릭터가 떨리거나 통통 튀어도 그림자는 바닥에 그대로 붙어 있음.
/// 캐릭터가 숨겨지면(뺏겨서 사라짐 등) 그림자도 같이 숨겨짐.
/// 플레이어, 타겟, 라이벌 모두에 붙이세요.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)] // 다른 스크립트들이 캐릭터 이미지를 찾은 뒤에 그림자를 만듦 (그림자를 캐릭터로 착각하지 않게)
public class CharacterShadow : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("비워두면 자식에서 자동으로 찾음")]
    [SerializeField] private SpriteRenderer characterSprite;

    [Header("모양")]
    [Tooltip("그림자 가로 길이 (캐릭터 폭 대비)")]
    [Range(0.2f, 1.2f)][SerializeField] private float widthRatio = 0.55f;
    [Tooltip("그림자 세로 / 가로 비율 (작을수록 납작)")]
    [Range(0.1f, 0.8f)][SerializeField] private float flatness = 0.28f;
    [Tooltip("그림자 색 (알파 = 진하기)")]
    [SerializeField] private Color color = new Color(0.25f, 0.12f, 0.1f, 0.32f);

    [Header("위치")]
    [Tooltip("켜면 시작할 때 캐릭터 이미지의 발끝 위치를 자동으로 찾음")]
    [SerializeField] private bool autoFindFeet = true;
    [Tooltip("발끝 기준으로 위아래 미세 조정 (월드 단위)")]
    [SerializeField] private float offsetY = 0.05f;
    [Tooltip("발끝 기준으로 좌우 미세 조정 (월드 단위)")]
    [SerializeField] private float offsetX = 0f;

    private static Sprite shadowSprite;
    private SpriteRenderer shadow;
    private Vector3 footLocal;

    private void Awake()
    {
        if (characterSprite == null) characterSprite = GetComponentInChildren<SpriteRenderer>();
        CreateShadow();
    }

    private void Start()
    {
        RecalculateFeet();
    }

    private void LateUpdate()
    {
        if (shadow == null || characterSprite == null) return;

        // 캐릭터가 안 보이면 그림자도 숨김
        shadow.enabled = characterSprite.enabled && characterSprite.gameObject.activeInHierarchy;
        if (!shadow.enabled) return;

        // 항상 캐릭터 이미지보다 아래에 그려지게
        shadow.sortingLayerID = characterSprite.sortingLayerID;
        shadow.sortingOrder = characterSprite.sortingOrder - 2;

        // 좌우 반전돼도 발 위치가 맞게
        float x = characterSprite.flipX ? -footLocal.x : footLocal.x;
        shadow.transform.localPosition = new Vector3(x + offsetX, footLocal.y + offsetY, 0f);
    }

    /// <summary>캐릭터 이미지 기준으로 발끝 위치와 그림자 크기를 다시 계산</summary>
    [ContextMenu("발끝 위치 다시 계산")]
    public void RecalculateFeet()
    {
        if (characterSprite == null || characterSprite.sprite == null || shadow == null) return;

        // 떨림/통통 효과가 없는 원래 자세 기준 (본체 기준 위치)
        Bounds b = characterSprite.bounds;
        Vector3 bottomCenterWorld = new Vector3(b.center.x, b.min.y, transform.position.z);
        footLocal = autoFindFeet ? transform.InverseTransformPoint(bottomCenterWorld) : Vector3.zero;
        footLocal.z = 0f;

        // 크기: 캐릭터 폭에 맞춰서 (그림자 이미지는 1 x 1 월드 단위)
        Vector3 lossy = transform.lossyScale;
        float width = b.size.x * widthRatio / Mathf.Max(0.0001f, Mathf.Abs(lossy.x));
        float height = width * flatness * Mathf.Abs(lossy.x) / Mathf.Max(0.0001f, Mathf.Abs(lossy.y));
        shadow.transform.localScale = new Vector3(width, height, 1f);
    }

    private void CreateShadow()
    {
        GameObject go = new GameObject("Shadow");
        go.transform.SetParent(transform, false);
        shadow = go.AddComponent<SpriteRenderer>();
        shadow.sprite = GetShadowSprite();
        shadow.color = color;
    }

    private void OnValidate()
    {
        if (shadow != null)
        {
            shadow.color = color;
            if (Application.isPlaying) RecalculateFeet();
        }
    }

    /// <summary>가운데가 진하고 가장자리로 부드럽게 사라지는 원 (코드로 생성)</summary>
    private static Sprite GetShadowSprite()
    {
        if (shadowSprite != null) return shadowSprite;

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "GeneratedShadow"
        };

        Color32[] px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                // 가운데는 거의 꽉 차고 바깥 40%에서 부드럽게 사라짐
                float a = Mathf.Clamp01((1f - r) / 0.4f);
                a = a * a * (3f - 2f * a);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();

        shadowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return shadowSprite;
    }
}