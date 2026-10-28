using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 시작 화면 연출: 하늘에서 반짝반짝 빛가루가 살랑살랑 떨어짐.
/// UI 캔버스(Screen Space - Overlay)에서는 파티클 시스템이 안 보여서, UI 이미지로 직접 떨어뜨림.
/// 시작 화면은 Time.timeScale = 0이라 실제 시간 기준으로 움직임.
/// StartPanel 안에 화면 전체 크기의 빈 오브젝트를 만들고 붙이세요.
/// (배경 이미지보다 아래, 버튼/글자보다 위 순서에 두면 예뻐요)
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SparkleRain : MonoBehaviour
{
    [Header("모양")]
    [Tooltip("(선택) 반짝이 이미지. 비우면 코드로 만든 4갈래 별 사용")]
    [SerializeField] private Sprite sparkleSprite;
    [SerializeField]
    private Color[] colors =
    {
        new Color(1f, 1f, 1f, 1f),
        new Color(1f, 0.88f, 0.55f, 1f),
        new Color(1f, 0.75f, 0.9f, 1f),
        new Color(0.75f, 1f, 0.88f, 1f), // 로고의 민트빛과 어울리게
    };
    [Tooltip("반짝이 크기 (픽셀, 최소/최대)")]
    [SerializeField] private Vector2 sizeRange = new Vector2(14f, 42f);

    [Header("양")]
    [Tooltip("화면에 동시에 떠 있는 반짝이 수")]
    [Min(1)][SerializeField] private int count = 40;

    [Header("떨어지는 움직임")]
    [Tooltip("떨어지는 속도 (픽셀/초, 최소/최대). 작을수록 천천히 하늘하늘")]
    [SerializeField] private Vector2 fallSpeed = new Vector2(50f, 130f);
    [Tooltip("좌우로 살랑이는 폭 (픽셀)")]
    [SerializeField] private float swayWidth = 28f;
    [Tooltip("좌우로 살랑이는 속도")]
    [SerializeField] private Vector2 swaySpeed = new Vector2(0.6f, 1.6f);
    [Tooltip("회전 속도 (도/초)")]
    [SerializeField] private float spinSpeed = 60f;

    [Header("반짝임")]
    [Tooltip("깜빡이는 속도")]
    [SerializeField] private Vector2 twinkleSpeed = new Vector2(2f, 5f);
    [Tooltip("가장 어두울 때 밝기 (0~1)")]
    [Range(0f, 1f)][SerializeField] private float minBrightness = 0.25f;

    private class Flake
    {
        public RectTransform rt;
        public Image img;
        public Color color;
        public float x, y, size, speed, swayPhase, swayFreq, spin, twinklePhase, twinkleFreq;
    }

    private static Sprite generatedSprite;
    private RectTransform area;
    private Flake[] flakes;

    private void Awake()
    {
        area = GetComponent<RectTransform>();
        Sprite sprite = sparkleSprite != null ? sparkleSprite : GetGeneratedSprite();

        flakes = new Flake[count];
        for (int i = 0; i < count; i++)
        {
            GameObject go = new GameObject($"Sparkle_{i}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false; // 버튼 터치를 막지 않게
            flakes[i] = new Flake { rt = go.GetComponent<RectTransform>(), img = img };
        }
    }

    private void OnEnable()
    {
        // 화면이 켜지자마자 이미 내리고 있는 것처럼 화면 전체에 흩뿌려둠
        if (flakes == null) return;
        foreach (Flake f in flakes) Respawn(f, true);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float t = Time.unscaledTime;
        Rect r = area.rect;

        foreach (Flake f in flakes)
        {
            f.y -= f.speed * dt;
            if (f.y < r.yMin - f.size) Respawn(f, false);

            float sway = Mathf.Sin(t * f.swayFreq + f.swayPhase) * swayWidth;
            f.rt.anchoredPosition = new Vector2(f.x + sway, f.y);
            f.rt.localRotation = Quaternion.Euler(0f, 0f, f.spin * t);

            // 반짝반짝: 밝아졌다 어두워졌다 + 크기도 살짝
            float tw = (Mathf.Sin(t * f.twinkleFreq + f.twinklePhase) + 1f) * 0.5f;
            float bright = Mathf.Lerp(minBrightness, 1f, tw * tw);
            Color c = f.color;
            c.a *= bright;
            f.img.color = c;
            float s = f.size * Mathf.Lerp(0.75f, 1.1f, tw);
            f.rt.sizeDelta = new Vector2(s, s);
        }
    }

    private void Respawn(Flake f, bool anywhere)
    {
        Rect r = area.rect;
        f.size = Random.Range(sizeRange.x, sizeRange.y);
        f.x = Random.Range(r.xMin, r.xMax);
        f.y = anywhere ? Random.Range(r.yMin, r.yMax) : r.yMax + f.size + Random.Range(0f, 120f);
        // 큰 반짝이는 가까이 있는 느낌으로 조금 더 빨리
        float k = Mathf.InverseLerp(sizeRange.x, sizeRange.y, f.size);
        f.speed = Mathf.Lerp(fallSpeed.x, fallSpeed.y, k * 0.6f + Random.value * 0.4f);
        f.swayFreq = Random.Range(swaySpeed.x, swaySpeed.y);
        f.swayPhase = Random.Range(0f, Mathf.PI * 2f);
        f.spin = Random.Range(-spinSpeed, spinSpeed);
        f.twinkleFreq = Random.Range(twinkleSpeed.x, twinkleSpeed.y);
        f.twinklePhase = Random.Range(0f, Mathf.PI * 2f);
        f.color = colors != null && colors.Length > 0 ? colors[Random.Range(0, colors.Length)] : Color.white;

        f.rt.anchorMin = f.rt.anchorMax = new Vector2(0.5f, 0.5f);
        f.rt.pivot = new Vector2(0.5f, 0.5f);
    }

    /// <summary>순정만화풍 4갈래 반짝이 별을 코드로 생성</summary>
    private static Sprite GetGeneratedSprite()
    {
        if (generatedSprite != null) return generatedSprite;

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "GeneratedSparkle"
        };
        Color32[] px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float star = Mathf.Clamp01((1f - (Mathf.Pow(Mathf.Abs(u), 0.55f) + Mathf.Pow(Mathf.Abs(v), 0.55f))) * 3.2f);
                float glow = Mathf.Exp(-(u * u + v * v) * 10f);
                float a = Mathf.Clamp01(Mathf.Max(star, glow));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        generatedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return generatedSprite;
    }
}