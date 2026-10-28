using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 타겟 연출.
/// 1. 마우스가 타겟 콜라이더 위에 올라가면 스프라이트 테두리를 따라 흐린 분홍 아우라가 생김
///    (모바일에선 손가락으로 누르고 있는 동안)
/// 2. 눈빛 공격을 받는 동안(차징/대결):
///    캐릭터 색이 어두운 보라 실루엣처럼 물들고, 진한 보라 아우라 + 주변에 파지직 튀는 번개 + 부들부들 떨림
/// 3. 유혹에 성공하면 타겟 색이 살짝 연한 분홍빛으로 바뀜
/// 4. 스페셜 타겟(늑대)은 주변에 반짝이가 항상 반짝임 (순정만화 미남 효과)
/// 5. 라이벌에게 뺏기면 파르스름하게 변하고, 라이벌을 따라 터벅터벅 걸어나감
/// 각 Target에 붙이세요.
/// ※ 아우라를 흐리게 만들려면 캐릭터 이미지의 Read/Write를 켜야 합니다 (안 켜면 단순한 테두리로 대체).
/// </summary>
[RequireComponent(typeof(TargetMover))]
public class TargetVisual : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("비워두면 자식에서 자동으로 찾음")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("마우스 오버 아우라")]
    [SerializeField] private Color auraColor = new Color(1f, 0.72f, 0.88f, 1f);
    [Range(0f, 1f)][SerializeField] private float auraAlpha = 0.85f;
    [Tooltip("아우라가 퍼지는 두께 (월드 단위)")]
    [SerializeField] private float auraSize = 0.15f;
    [Tooltip("나타나고 사라지는 속도")]
    [SerializeField] private float fadeSpeed = 8f;
    [Tooltip("은은하게 숨쉬듯 깜빡이는 정도 (0이면 안 깜빡임)")]
    [Range(0f, 0.5f)][SerializeField] private float pulseAmount = 0.15f;
    [SerializeField] private float pulseSpeed = 4f;
    [Tooltip("이미 유혹했거나 뺏긴 타겟에는 아우라를 띄우지 않음")]
    [SerializeField] private bool onlyWhenAvailable = true;

    [Header("눈빛 공격 받는 중 - 색")]
    [Tooltip("캐릭터 색에 곱해지는 색 (어두운 보라 = 실루엣 느낌)")]
    [SerializeField] private Color charmedTint = new Color(0.25f, 0.13f, 0.36f, 1f);
    [Tooltip("공격받는 동안 아우라 색")]
    [SerializeField] private Color charmedAuraColor = new Color(0.78f, 0.4f, 1f, 1f);
    [Range(0f, 1f)][SerializeField] private float charmedAuraAlpha = 1f;
    [Tooltip("공격받을 때 색이 바뀌는 속도 (클수록 즉시)")]
    [SerializeField] private float charmedTintSpeed = 18f;

    [Header("눈빛 공격 받는 중 - 전기 (파지직)")]
    [Tooltip("동시에 튀는 번개 줄기 수")]
    [Min(0)][SerializeField] private int boltCount = 5;
    [Tooltip("번개 모양이 바뀌는 간격(초). 작을수록 더 빠르게 파지직")]
    [SerializeField] private float boltInterval = 0.05f;
    [Tooltip("번개가 매번 보일 확률 (깜빡임)")]
    [Range(0f, 1f)][SerializeField] private float boltVisibleChance = 0.75f;
    [SerializeField] private Color boltColor = new Color(0.95f, 0.75f, 1f, 1f);
    [SerializeField] private Color boltGlowColor = new Color(0.75f, 0.35f, 1f, 0.9f);
    [SerializeField] private float boltWidth = 0.04f;
    [Tooltip("번개가 캐릭터 몸에서 얼마나 바깥에서 튀는지 (스프라이트 크기 대비)")]
    [SerializeField] private float boltRadius = 0.95f;
    [Tooltip("번개 한 줄기 길이 (월드 단위, 최소/최대)")]
    [SerializeField] private Vector2 boltLength = new Vector2(0.25f, 0.55f);
    [Tooltip("번개가 꺾이는 정도")]
    [SerializeField] private float boltJitter = 0.09f;
    [Tooltip("(선택) 번개 머티리얼. 비우면 자동 생성")]
    [SerializeField] private Material boltMaterial;

    [Header("눈빛 공격 받는 중 - 떨림")]
    [Tooltip("떨리는 정도 (월드 단위, 0이면 안 떨림)")]
    [SerializeField] private float shakeAmount = 0.04f;
    [SerializeField] private float shakeSpeed = 40f;

    [Header("스페셜 타겟 반짝임 (미남 효과)")]
    [Tooltip("TargetInfo의 Target Type이 Special이면 반짝이를 항상 표시")]
    [SerializeField] private bool sparkleForSpecial = true;
    [Tooltip("동시에 보이는 반짝이 수")]
    [Min(0)][SerializeField] private int sparkleCount = 7;
    [Tooltip("반짝이 하나가 나타났다 사라지는 시간 (최소, 최대)")]
    [SerializeField] private Vector2 sparkleLifetime = new Vector2(0.6f, 1.3f);
    [Tooltip("반짝이 크기 (월드 단위, 최소, 최대)")]
    [SerializeField] private Vector2 sparkleSize = new Vector2(0.12f, 0.32f);
    [Tooltip("캐릭터에서 얼마나 넓게 퍼지는지 (스프라이트 크기 대비)")]
    [SerializeField] private float sparkleSpread = 1.05f;
    [SerializeField]
    private Color[] sparkleColors =
    {
        new Color(1f, 1f, 1f, 1f),
        new Color(1f, 0.85f, 0.35f, 1f),
        new Color(1f, 0.65f, 0.9f, 1f),
    };
    [Tooltip("(선택) 반짝이 이미지. 비우면 코드로 만든 4갈래 별 사용")]
    [SerializeField] private Sprite sparkleSprite;

    [Header("라이벌에게 뺏겼을 때")]
    [Tooltip("뺏긴 타겟의 색 (파르스름한 느낌)")]
    [SerializeField] private Color takenTint = new Color(0.62f, 0.74f, 1f, 1f);
    [Tooltip("걸어나갈 때 걸음마다 통통 튀는 높이")]
    [SerializeField] private float takenBobHeight = 0.07f;
    [SerializeField] private float takenBobSpeed = 9f;

    [Header("유혹 성공 색")]
    [SerializeField] private Color loveTint = new Color(1f, 0.82f, 0.9f, 1f);
    [Tooltip("색이 바뀌는 속도")]
    [SerializeField] private float tintSpeed = 3f;

    // 같은 이미지를 쓰는 타겟끼리 아우라 이미지를 공유 (한 번만 만들기)
    private static readonly Dictionary<(Sprite, float), Sprite> auraCache = new Dictionary<(Sprite, float), Sprite>();
    private static bool warnedNotReadable;
    private static Material sharedBoltMaterial;
    private const int Downsample = 4; // 아우라는 1/4 해상도로 만들어도 충분히 부드러움
    private const int BoltSegments = 7;

    private TargetMover target;
    private PlayerAttack player;
    private Collider2D[] colliders;
    private Camera cam;
    private SpriteRenderer aura;
    private Sprite auraBuiltFor;
    private bool auraIsFallback;
    private Color baseColor;
    private float auraVisible;
    private Vector3 spriteBaseLocalPos;
    private bool wasCharmed;

    private LineRenderer[] bolts;      // 밝은 심지
    private LineRenderer[] boltGlows;  // 굵고 흐린 빛
    private float boltTimer;
    private bool boltsActive;

    private class Sparkle
    {
        public SpriteRenderer sr;
        public float age, life, size, spin;
        public Color color;
    }
    private Sparkle[] sparkles;
    private static Sprite generatedSparkle;

    private void Awake()
    {
        target = GetComponent<TargetMover>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        colliders = GetComponentsInChildren<Collider2D>();
        if (spriteRenderer != null)
        {
            baseColor = spriteRenderer.color;
            spriteBaseLocalPos = spriteRenderer.transform.localPosition;
        }
        CreateAuraRenderer();
        CreateBolts();
        if (sparkleForSpecial && target.Info != null && target.Info.targetType == TargetType.Special)
            CreateSparkles();
    }

    private void Start()
    {
        cam = Camera.main;
        player = FindFirstObjectByType<PlayerAttack>();
    }

    private void Update()
    {
        if (spriteRenderer == null) return;
        float dt = Time.unscaledDeltaTime;

        bool charmed = IsBeingCharmed() && spriteRenderer.enabled;
        UpdateShake(charmed);
        UpdateAura(dt, charmed);
        UpdateTint(dt, charmed);
        UpdateBolts(dt, charmed);
        UpdateSparkles(Time.deltaTime);
    }

    #region 눈빛 공격 받는 중

    /// <summary>플레이어가 눈빛을 쏘는 중이거나, 이 타겟을 두고 대결 중</summary>
    private bool IsBeingCharmed()
    {
        if (player != null && player.IsCharming && player.CurrentTarget == target) return true;
        return DuelManager.IsActive && DuelManager.Instance.Target == target;
    }

    private void UpdateShake(bool charmed)
    {
        // 부들부들 떨림 (스프라이트가 자식 오브젝트일 때만: 본체를 흔들면 이동이 꼬임)
        Transform st = spriteRenderer.transform;
        if (st == transform) return;

        if (target.IsTaken && spriteRenderer.enabled)
        {
            // 뺏겨서 끌려가는 중: 터벅터벅 통통
            float bob = Mathf.Abs(Mathf.Sin(Time.time * takenBobSpeed)) * takenBobHeight;
            st.localPosition = spriteBaseLocalPos + new Vector3(0f, bob, 0f);
            wasCharmed = true; // 다음에 원위치로 되돌리도록
            return;
        }

        if (charmed && shakeAmount > 0f)
        {
            float t = Time.time * shakeSpeed;
            Vector3 offset = new Vector3(
                (Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f * shakeAmount,
                (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * shakeAmount, 0f);
            st.localPosition = spriteBaseLocalPos + offset;
        }
        else if (wasCharmed)
        {
            st.localPosition = spriteBaseLocalPos;
        }
        wasCharmed = charmed;
    }

    #endregion

    #region 아우라

    private void UpdateAura(float dt, bool charmed)
    {
        bool hover = IsPointerOver() && (!onlyWhenAvailable || target.IsAvailable);
        bool want = charmed || hover;

        // 공격받기 시작하면 아우라는 즉시 진하게
        if (charmed) auraVisible = 1f;
        else auraVisible = Mathf.MoveTowards(auraVisible, want ? 1f : 0f, fadeSpeed * dt);

        bool show = auraVisible > 0.001f && spriteRenderer.enabled;
        aura.enabled = show;
        if (!show) return;

        if (auraBuiltFor != spriteRenderer.sprite) RebuildAura();

        aura.flipX = spriteRenderer.flipX;
        aura.flipY = spriteRenderer.flipY;
        aura.sortingLayerID = spriteRenderer.sortingLayerID;
        aura.sortingOrder = spriteRenderer.sortingOrder - 1;

        // 공격받는 중엔 빠르게 일렁임
        float speed = charmed ? pulseSpeed * 4f : pulseSpeed;
        float amount = charmed ? 0.12f : pulseAmount;
        float pulse = 1f - amount + amount * Mathf.Sin(Time.unscaledTime * speed);

        Color c = charmed ? charmedAuraColor : auraColor;
        float a = charmed ? charmedAuraAlpha : auraAlpha;
        c.a = a * auraVisible * pulse * (auraIsFallback ? 0.6f : 1f);
        aura.color = c;
    }

    private bool IsPointerOver()
    {
        Pointer p = Pointer.current;
        if (p == null || cam == null) return false;
        if (p is Touchscreen && !p.press.isPressed) return false; // 모바일: 누르고 있을 때만

        Vector2 sp = p.position.ReadValue();
        float depth = Mathf.Abs(cam.transform.position.z - transform.position.z);
        Vector2 wp = cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, depth));

        foreach (Collider2D col in colliders)
            if (col != null && col.enabled && col.OverlapPoint(wp)) return true;
        return false;
    }

    private void CreateAuraRenderer()
    {
        if (spriteRenderer == null) return;
        GameObject go = new GameObject("Aura");
        go.transform.SetParent(spriteRenderer.transform, false);
        aura = go.AddComponent<SpriteRenderer>();
        aura.enabled = false;
    }

    private void RebuildAura()
    {
        Sprite src = spriteRenderer.sprite;
        auraBuiltFor = src;
        aura.transform.localScale = Vector3.one;
        auraIsFallback = false;
        if (src == null)
        {
            aura.sprite = null;
            return;
        }

        var key = (src, auraSize);
        if (!auraCache.TryGetValue(key, out Sprite built))
        {
            built = BuildBlurredAura(src, auraSize);
            auraCache[key] = built; // 실패(null)도 저장해서 매번 다시 시도하지 않음
        }

        if (built != null)
        {
            aura.sprite = built;
            return;
        }

        // 대체: 원본을 살짝 키워서 뒤에 깔기 (Read/Write가 꺼져 있을 때)
        auraIsFallback = true;
        aura.sprite = src;
        Vector2 size = src.bounds.size;
        float k = 1f + auraSize * 2f / Mathf.Max(0.01f, Mathf.Min(size.x, size.y));
        aura.transform.localScale = new Vector3(k, k, 1f);
    }

    /// <summary>스프라이트 실루엣을 넓히고 흐리게 만든 아우라 이미지 생성</summary>
    private static Sprite BuildBlurredAura(Sprite src, float worldSize)
    {
        Texture2D tex = src.texture;
        if (tex == null || !tex.isReadable)
        {
            if (!warnedNotReadable)
            {
                warnedNotReadable = true;
                Debug.LogWarning($"[TargetVisual] '{src.name}' 이미지의 Read/Write가 꺼져 있어서 흐린 아우라 대신 단순 테두리를 사용해요. " +
                                 "이미지 선택 → Inspector → Advanced → Read/Write 체크 → Apply 하면 흐린 아우라가 나와요.");
            }
            return null;
        }

        Rect r = src.packed ? src.textureRect : src.rect;
        int sx = Mathf.FloorToInt(r.x), sy = Mathf.FloorToInt(r.y);
        int sw = Mathf.FloorToInt(r.width), sh = Mathf.FloorToInt(r.height);

        Color32[] srcPx;
        try
        {
            srcPx = tex.GetPixels32();
        }
        catch
        {
            return null;
        }
        int texW = tex.width;

        int w = Mathf.CeilToInt(sw / (float)Downsample);
        int h = Mathf.CeilToInt(sh / (float)Downsample);
        float ppu = src.pixelsPerUnit / Downsample;
        int spread = Mathf.Max(2, Mathf.CeilToInt(worldSize * ppu));
        int pad = spread * 2 + 2;
        int W = w + pad * 2, H = h + pad * 2;

        // 1) 축소하면서 알파(실루엣)만 추출
        float[] a = new float[W * H];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                byte max = 0;
                for (int by = 0; by < Downsample; by++)
                {
                    int py = sy + y * Downsample + by;
                    if (py >= sy + sh) break;
                    int row = py * texW;
                    for (int bx = 0; bx < Downsample; bx++)
                    {
                        int px = sx + x * Downsample + bx;
                        if (px >= sx + sw) break;
                        byte al = srcPx[row + px].a;
                        if (al > max) max = al;
                    }
                }
                a[(y + pad) * W + (x + pad)] = max / 255f;
            }
        }

        // 2) 실루엣을 넓히고 3) 흐리게
        a = MaxFilter(a, W, H, Mathf.Max(1, spread / 2));
        a = BoxBlur(a, W, H, spread);
        a = BoxBlur(a, W, H, spread);

        Color32[] outPx = new Color32[W * H];
        for (int i = 0; i < outPx.Length; i++)
            outPx[i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a[i] * 1.4f) * 255f));

        Texture2D outTex = new Texture2D(W, H, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = src.name + "_Aura"
        };
        outTex.SetPixels32(outPx);
        outTex.Apply();

        // 원본과 같은 지점이 기준점(pivot)이 되도록
        Vector2 pivotPx = src.pivot;
        if (src.packed) pivotPx -= src.textureRectOffset;
        Vector2 pivot = new Vector2((pivotPx.x / Downsample + pad) / W, (pivotPx.y / Downsample + pad) / H);

        return Sprite.Create(outTex, new Rect(0, 0, W, H), pivot, ppu, 0, SpriteMeshType.FullRect);
    }

    private static float[] MaxFilter(float[] src, int w, int h, int r)
    {
        float[] tmp = new float[src.Length];
        float[] dst = new float[src.Length];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float m = 0f;
                for (int k = Mathf.Max(0, x - r); k <= Mathf.Min(w - 1, x + r); k++)
                    m = Mathf.Max(m, src[y * w + k]);
                tmp[y * w + x] = m;
            }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float m = 0f;
                for (int k = Mathf.Max(0, y - r); k <= Mathf.Min(h - 1, y + r); k++)
                    m = Mathf.Max(m, tmp[k * w + x]);
                dst[y * w + x] = m;
            }
        return dst;
    }

    private static float[] BoxBlur(float[] src, int w, int h, int r)
    {
        float[] tmp = new float[src.Length];
        float[] dst = new float[src.Length];
        float inv = 1f / (r * 2 + 1);
        for (int y = 0; y < h; y++)
        {
            float sum = 0f;
            for (int k = -r; k <= r; k++) sum += src[y * w + Mathf.Clamp(k, 0, w - 1)];
            for (int x = 0; x < w; x++)
            {
                tmp[y * w + x] = sum * inv;
                sum += src[y * w + Mathf.Min(w - 1, x + r + 1)] - src[y * w + Mathf.Max(0, x - r)];
            }
        }
        for (int x = 0; x < w; x++)
        {
            float sum = 0f;
            for (int k = -r; k <= r; k++) sum += tmp[Mathf.Clamp(k, 0, h - 1) * w + x];
            for (int y = 0; y < h; y++)
            {
                dst[y * w + x] = sum * inv;
                sum += tmp[Mathf.Min(h - 1, y + r + 1) * w + x] - tmp[Mathf.Max(0, y - r) * w + x];
            }
        }
        return dst;
    }

    #endregion

    #region 색

    private void UpdateTint(float dt, bool charmed)
    {
        Color goal;
        float speed;
        if (charmed)
        {
            goal = baseColor * charmedTint; // 어두운 보라 실루엣
            speed = charmedTintSpeed;
        }
        else
        {
            goal = target.IsLoved ? baseColor * loveTint
                 : target.IsTaken ? baseColor * takenTint
                 : baseColor;
            speed = wasCharmedTint ? charmedTintSpeed : tintSpeed; // 공격이 끝나면 빠르게 원래 색으로
        }
        spriteRenderer.color = Color.Lerp(spriteRenderer.color, goal, 1f - Mathf.Exp(-speed * dt));
        if (!charmed && wasCharmedTint && Vector4.Distance(spriteRenderer.color, goal) < 0.02f) wasCharmedTint = false;
        if (charmed) wasCharmedTint = true;
    }

    private bool wasCharmedTint;

    #endregion

    #region 전기 (파지직)

    private void CreateBolts()
    {
        if (spriteRenderer == null || boltCount <= 0) return;

        Material mat = boltMaterial != null ? boltMaterial : GetSharedBoltMaterial();
        bolts = new LineRenderer[boltCount];
        boltGlows = new LineRenderer[boltCount];

        GameObject root = new GameObject("Bolts");
        root.transform.SetParent(transform, false);

        for (int i = 0; i < boltCount; i++)
        {
            boltGlows[i] = CreateLine(root.transform, $"BoltGlow_{i}", mat, boltWidth * 3.5f, boltGlowColor);
            bolts[i] = CreateLine(root.transform, $"Bolt_{i}", mat, boltWidth, boltColor);
        }
    }

    private LineRenderer CreateLine(Transform parent, string name, Material mat, float width, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = BoltSegments;
        lr.numCapVertices = 2;
        lr.numCornerVertices = 1;
        if (mat != null) lr.sharedMaterial = mat;
        // 양 끝이 가늘어지는 번개
        lr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.15f));
        lr.widthMultiplier = width;
        Color end = color;
        end.a *= 0.6f;
        lr.startColor = color;
        lr.endColor = end;
        lr.enabled = false;
        return lr;
    }

    private static Material GetSharedBoltMaterial()
    {
        if (sharedBoltMaterial != null) return sharedBoltMaterial;
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader != null) sharedBoltMaterial = new Material(shader);
        return sharedBoltMaterial;
    }

    private void UpdateBolts(float dt, bool charmed)
    {
        if (bolts == null) return;

        if (!charmed)
        {
            if (boltsActive) SetBoltsEnabled(false);
            boltsActive = false;
            boltTimer = 0f;
            return;
        }
        boltsActive = true;

        boltTimer -= dt;
        if (boltTimer > 0f) return;
        boltTimer = boltInterval;

        Bounds b = spriteRenderer.bounds;
        Vector2 center = b.center;
        Vector2 radius = new Vector2(b.extents.x, b.extents.y) * boltRadius;
        float z = spriteRenderer.transform.position.z;

        for (int i = 0; i < bolts.Length; i++)
        {
            bool visible = Random.value < boltVisibleChance;
            bolts[i].enabled = visible;
            boltGlows[i].enabled = visible;
            if (!visible) continue;

            bolts[i].sortingLayerID = spriteRenderer.sortingLayerID;
            bolts[i].sortingOrder = spriteRenderer.sortingOrder + 2;
            boltGlows[i].sortingLayerID = spriteRenderer.sortingLayerID;
            boltGlows[i].sortingOrder = spriteRenderer.sortingOrder + 1;

            BuildBolt(bolts[i], boltGlows[i], center, radius, z);
        }
    }

    /// <summary>캐릭터 둘레의 한 지점에서 둘레를 따라 지그재그로 튀는 번개 한 줄기</summary>
    private void BuildBolt(LineRenderer core, LineRenderer glow, Vector2 center, Vector2 radius, float z)
    {
        float length = Random.Range(boltLength.x, boltLength.y);
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float r = Random.Range(0.85f, 1.1f);

        Vector2 start = center + new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y) * r;

        // 둘레를 따라가는 방향 + 살짝 바깥쪽으로
        Vector2 outward = (start - center).normalized;
        Vector2 tangent = new Vector2(-outward.y, outward.x) * (Random.value < 0.5f ? 1f : -1f);
        Vector2 dir = (tangent + outward * Random.Range(-0.2f, 0.6f)).normalized;
        Vector2 normal = new Vector2(-dir.y, dir.x);

        for (int s = 0; s < BoltSegments; s++)
        {
            float t = s / (float)(BoltSegments - 1);
            float jitter = (s == 0 || s == BoltSegments - 1) ? 0f : Random.Range(-1f, 1f) * boltJitter;
            Vector2 p = start + dir * (length * t) + normal * jitter;
            Vector3 pos = new Vector3(p.x, p.y, z);
            core.SetPosition(s, pos);
            glow.SetPosition(s, pos);
        }
    }

    private void SetBoltsEnabled(bool on)
    {
        for (int i = 0; i < bolts.Length; i++)
        {
            bolts[i].enabled = on;
            boltGlows[i].enabled = on;
        }
    }

    #endregion

    #region 스페셜 타겟 반짝임

    private void CreateSparkles()
    {
        if (spriteRenderer == null || sparkleCount <= 0) return;

        Sprite sprite = sparkleSprite != null ? sparkleSprite : GetGeneratedSparkle();
        GameObject root = new GameObject("Sparkles");
        root.transform.SetParent(transform, false);

        sparkles = new Sparkle[sparkleCount];
        for (int i = 0; i < sparkleCount; i++)
        {
            GameObject go = new GameObject($"Sparkle_{i}");
            go.transform.SetParent(root.transform, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sparkles[i] = new Sparkle { sr = sr };
            Respawn(sparkles[i]);
            sparkles[i].age = Random.Range(0f, sparkles[i].life); // 처음부터 엇갈려서 반짝이게
        }
    }

    private void UpdateSparkles(float dt)
    {
        if (sparkles == null) return;

        bool visible = spriteRenderer.enabled;
        foreach (Sparkle sp in sparkles)
        {
            if (!visible)
            {
                sp.sr.enabled = false;
                continue;
            }

            sp.age += dt;
            if (sp.age >= sp.life) Respawn(sp);

            // 톡 커졌다가 작아지며 사라짐 + 천천히 회전
            float t = sp.age / sp.life;
            float k = Mathf.Sin(t * Mathf.PI);
            float twinkle = 0.85f + 0.15f * Mathf.Sin(sp.age * 25f);
            float scale = sp.size * k * twinkle;

            sp.sr.enabled = true;
            sp.sr.sortingLayerID = spriteRenderer.sortingLayerID;
            sp.sr.sortingOrder = spriteRenderer.sortingOrder + 3;
            sp.sr.transform.localScale = new Vector3(scale, scale, 1f);
            sp.sr.transform.Rotate(0f, 0f, sp.spin * dt);

            Color c = sp.color;
            c.a *= k;
            sp.sr.color = c;
        }
    }

    /// <summary>캐릭터 주변(머리 쪽에 조금 더 많이) 랜덤한 곳에 새 반짝이</summary>
    private void Respawn(Sparkle sp)
    {
        sp.age = 0f;
        sp.life = Random.Range(sparkleLifetime.x, sparkleLifetime.y);
        sp.size = Random.Range(sparkleSize.x, sparkleSize.y);
        sp.spin = Random.Range(-90f, 90f);
        sp.color = sparkleColors != null && sparkleColors.Length > 0
            ? sparkleColors[Random.Range(0, sparkleColors.Length)]
            : Color.white;

        Bounds b = spriteRenderer.bounds;
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float r = Random.Range(0.55f, 1f) * sparkleSpread;
        Vector3 world = b.center + new Vector3(
            Mathf.Cos(angle) * b.extents.x * r,
            Mathf.Sin(angle) * b.extents.y * r + b.extents.y * 0.15f, // 살짝 위쪽(얼굴 주변)으로
            0f);

        // 타겟 본체 기준 위치로 저장 → 늑대가 움직여도 같이 따라다님
        Transform t = sp.sr.transform;
        t.position = new Vector3(world.x, world.y, spriteRenderer.transform.position.z);
        t.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 45f));
    }

    /// <summary>순정만화풍 4갈래 반짝이 별 이미지를 코드로 생성</summary>
    private static Sprite GetGeneratedSparkle()
    {
        if (generatedSparkle != null) return generatedSparkle;

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
                // 4갈래 별 (가늘고 뾰족한 십자)
                float star = 1f - (Mathf.Pow(Mathf.Abs(u), 0.6f) + Mathf.Pow(Mathf.Abs(v), 0.6f));
                star = Mathf.Clamp01(star * 3f);
                // 가운데 부드러운 빛
                float glow = Mathf.Exp(-(u * u + v * v) * 10f);
                float a = Mathf.Clamp01(Mathf.Max(star, glow));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();

        generatedSparkle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return generatedSparkle;
    }

    #endregion
}