using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 빈 곳을 탭해서 플레이어가 이동할 때, 목적지에 에어브러시 원 이펙트를 띄움.
/// 가운데 원(Dot)이 톡 떴다 사라지고, 링(Ring)이 바깥으로 퍼지며 사라짐.
/// 타겟/UI를 누를 때는 뜨지 않음 (PlayerController가 이동할 때만).
/// GameManager 같은 아무 오브젝트에 하나만 붙이세요.
/// </summary>
public class TouchMarker : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("비워두면 자동으로 찾음")]
    [SerializeField] private PlayerController player;
    [Tooltip("TouchSoftDot 이미지")]
    [SerializeField] private Sprite dotSprite;
    [Tooltip("TouchSoftRing 이미지")]
    [SerializeField] private Sprite ringSprite;

    [Header("크기 (월드 단위 지름)")]
    [SerializeField] private float size = 0.8f;
    [SerializeField] private float dotStartScale = 0.5f;
    [SerializeField] private float dotPeakScale = 1f;
    [SerializeField] private float ringStartScale = 0.4f;
    [SerializeField] private float ringEndScale = 1.6f;

    [Header("시간 (초)")]
    [SerializeField] private float dotDuration = 0.35f;
    [SerializeField] private float ringDuration = 0.5f;

    [Header("색 / 투명도")]
    [SerializeField] private Color tint = Color.white;
    [Range(0f, 1f)][SerializeField] private float dotAlpha = 0.9f;
    [Range(0f, 1f)][SerializeField] private float ringAlpha = 0.8f;

    [Header("렌더링")]
    [Tooltip("배경보다 위, 캐릭터보다 아래에 오도록 설정")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 1;
    [Tooltip("동시에 보일 수 있는 최대 개수 (빠르게 연타할 때)")]
    [Min(1)][SerializeField] private int poolSize = 4;

    private class Marker
    {
        public GameObject root;
        public SpriteRenderer dot;
        public SpriteRenderer ring;
        public float time;
        public bool active;
    }

    private readonly List<Marker> pool = new List<Marker>();

    private void Start()
    {
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (player != null) player.OnTapMove += Show;
        else Debug.LogWarning("[TouchMarker] 씬에 PlayerController가 없어요.", this);

        for (int i = 0; i < poolSize; i++) pool.Add(CreateMarker(i));
    }

    private void OnDestroy()
    {
        if (player != null) player.OnTapMove -= Show;
    }

    /// <summary>이 위치에 이펙트 표시</summary>
    public void Show(Vector2 position)
    {
        // 쉬고 있는 것을 쓰고, 다 쓰는 중이면 가장 오래된 것을 재사용
        Marker m = null;
        foreach (Marker p in pool)
        {
            if (!p.active) { m = p; break; }
            if (m == null || p.time > m.time) m = p;
        }
        if (m == null) return;

        m.root.transform.position = new Vector3(position.x, position.y, 0f);
        m.time = 0f;
        m.active = true;
        m.root.SetActive(true);
        Apply(m);
    }

    private void Update()
    {
        foreach (Marker m in pool)
        {
            if (!m.active) continue;

            m.time += Time.deltaTime;
            if (m.time >= Mathf.Max(dotDuration, ringDuration))
            {
                m.active = false;
                m.root.SetActive(false);
                continue;
            }
            Apply(m);
        }
    }

    private void Apply(Marker m)
    {
        // 가운데 원: 앞쪽 40% 동안 톡 커지고, 그 뒤로 서서히 사라짐
        if (m.dot != null)
        {
            float k = Mathf.Clamp01(m.time / dotDuration);
            float grow = EaseOut(Mathf.Clamp01(k / 0.4f));
            SetScale(m.dot, Mathf.Lerp(dotStartScale, dotPeakScale, grow));
            SetAlpha(m.dot, dotAlpha * (1f - k * k));
        }

        // 링: 바깥으로 퍼지면서 사라짐
        if (m.ring != null)
        {
            float k = Mathf.Clamp01(m.time / ringDuration);
            SetScale(m.ring, Mathf.Lerp(ringStartScale, ringEndScale, EaseOut(k)));
            SetAlpha(m.ring, ringAlpha * (1f - k));
        }
    }

    private Marker CreateMarker(int index)
    {
        GameObject root = new GameObject($"TouchMarker_{index}");
        root.transform.SetParent(transform, false);
        root.SetActive(false);

        return new Marker
        {
            root = root,
            dot = CreateLayer(root.transform, "Dot", dotSprite, sortingOrder + 1),
            ring = CreateLayer(root.transform, "Ring", ringSprite, sortingOrder)
        };
    }

    private SpriteRenderer CreateLayer(Transform parent, string name, Sprite sprite, int order)
    {
        if (sprite == null) return null;
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = order;
        sr.color = tint;
        return sr;
    }

    /// <summary>이미지 원본 크기와 상관없이 size(월드 단위) 기준으로 크기 맞춤</summary>
    private void SetScale(SpriteRenderer sr, float scale)
    {
        float native = sr.sprite.bounds.size.x;
        float s = native > 0f ? size / native * scale : scale;
        sr.transform.localScale = new Vector3(s, s, 1f);
    }

    private void SetAlpha(SpriteRenderer sr, float a)
    {
        Color c = tint;
        c.a = tint.a * a;
        sr.color = c;
    }

    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
}