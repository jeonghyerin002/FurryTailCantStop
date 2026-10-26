using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 매력 게이지 / 피버 모드 UI.
/// </summary>
public class FeverUI : MonoBehaviour
{
    [Header("매력 게이지")]
    [SerializeField] private Slider gaugeSlider;
    [Tooltip("게이지 Fill 이미지 (피버 중 색 변경)")]
    [SerializeField] private Image fillImage;
    [SerializeField] private Color normalColor = new Color(1f, 0.45f, 0.7f);
    [SerializeField] private Color feverColor = new Color(1f, 0.85f, 0.2f);

    [Header("피버 표시")]
    [Tooltip("피버 중에만 켜지는 오브젝트 (FEVER! 텍스트 등)")]
    [SerializeField] private GameObject feverLabel;
    [SerializeField] private TMP_Text feverTimeText;
    [Tooltip("피버 중 화면 테두리 반짝임 (CanvasGroup, 내부 Image의 Raycast Target은 꺼두기)")]
    [SerializeField] private CanvasGroup feverOverlay;
    [SerializeField] private float overlayMaxAlpha = 0.35f;
    [SerializeField] private float overlayPulseSpeed = 5f;
    [Tooltip("피버 중 켤 월드 오브젝트 (플레이어 오라 파티클 등)")]
    [SerializeField] private GameObject[] feverEffects;

    private void Start()
    {
        if (gaugeSlider != null)
        {
            gaugeSlider.minValue = 0f;
            gaugeSlider.maxValue = 1f;
            gaugeSlider.interactable = false;
        }

        FeverManager fm = FeverManager.Instance;
        if (fm != null)
        {
            fm.OnGaugeChanged += HandleGauge;
            fm.OnFeverStarted += HandleFeverStart;
            fm.OnFeverEnded += HandleFeverEnd;
            HandleGauge(fm.GaugeNormalized);
            SetFeverVisual(fm.IsFever);
        }
        else
        {
            Debug.LogWarning("[FeverUI] 씬에 FeverManager가 없어요.", this);
            SetFeverVisual(false);
        }
    }

    private void OnDestroy()
    {
        FeverManager fm = FeverManager.Instance;
        if (fm == null) return;
        fm.OnGaugeChanged -= HandleGauge;
        fm.OnFeverStarted -= HandleFeverStart;
        fm.OnFeverEnded -= HandleFeverEnd;
    }

    private void Update()
    {
        if (!FeverManager.IsFeverActive) return;

        if (feverOverlay != null)
            feverOverlay.alpha = (Mathf.Sin(Time.time * overlayPulseSpeed) + 1f) * 0.5f * overlayMaxAlpha;
        if (feverTimeText != null)
            feverTimeText.text = $"{FeverManager.Instance.Remaining:0.0}";
    }

    private void HandleGauge(float normalized)
    {
        if (gaugeSlider != null) gaugeSlider.value = normalized;
    }

    private void HandleFeverStart() => SetFeverVisual(true);
    private void HandleFeverEnd() => SetFeverVisual(false);

    private void SetFeverVisual(bool on)
    {
        if (fillImage != null) fillImage.color = on ? feverColor : normalColor;
        if (feverLabel != null) feverLabel.SetActive(on);
        if (feverTimeText != null) feverTimeText.gameObject.SetActive(on);
        if (feverOverlay != null)
        {
            feverOverlay.gameObject.SetActive(on);
            feverOverlay.alpha = 0f;
            feverOverlay.blocksRaycasts = false;
            feverOverlay.interactable = false;
        }
        if (feverEffects != null)
            foreach (GameObject fx in feverEffects)
                if (fx != null) fx.SetActive(on);
    }
}