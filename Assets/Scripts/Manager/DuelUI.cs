using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 연타 대결 UI. 게이지 / 남은 시간 / "TAP!" 연타 유도 이펙트 / 결과 표시.
/// ※ root는 이 스크립트가 붙은 오브젝트가 아닌 "자식" 오브젝트로 지정하세요
///   (꺼진 오브젝트에선 코루틴이 동작하지 않음).
/// </summary>
public class DuelUI : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("대결 UI 전체 루트 (자식 오브젝트)")]
    [SerializeField] private GameObject root;
    [Tooltip("플레이어 게이지 (0 = 라이벌 승, 1 = 플레이어 승). 왼쪽 플레이어 색 / 오른쪽 라이벌 색 추천")]
    [SerializeField] private Slider gaugeSlider;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text introText;
    [Tooltip("연타 유도 텍스트/이미지 (TAP! TAP!)")]
    [SerializeField] private RectTransform tapPrompt;
    [SerializeField] private TMP_Text resultText;

    [Header("문구")]
    [SerializeField] private string introMessage = "대결!";
    [SerializeField] private string winMessage = "승리!";
    [SerializeField] private string loseMessage = "패배...";
    [SerializeField] private string feverWinMessage = "FEVER! 한 방에 날려버렸다!";
    [SerializeField] private float resultShowTime = 1f;

    [Header("연타 유도 연출")]
    [SerializeField] private float pulseSpeed = 12f;
    [SerializeField] private float pulseScale = 0.12f;
    [SerializeField] private float tapPunchScale = 0.35f;
    [SerializeField] private float punchRecover = 3f;
    [Tooltip("(선택) 탭할 때 흔들 UI (게이지 패널 등)")]
    [SerializeField] private RectTransform shakeTarget;
    [SerializeField] private float shakeStrength = 6f;

    private float punch;
    private Coroutine resultRoutine;
    private Vector2 shakeOrigin;

    private void Start()
    {
        if (gaugeSlider != null)
        {
            gaugeSlider.minValue = 0f;
            gaugeSlider.maxValue = 1f;
            gaugeSlider.interactable = false;
        }
        if (shakeTarget != null) shakeOrigin = shakeTarget.anchoredPosition;

        DuelManager dm = DuelManager.Instance;
        if (dm != null)
        {
            dm.OnDuelStarted += HandleStarted;
            dm.OnPlayerTap += HandlePlayerTap;
            dm.OnDuelEnded += HandleEnded;
            dm.OnDuelCancelled += HandleCancelled;
        }
        else Debug.LogWarning("[DuelUI] 씬에 DuelManager가 없어요.", this);

        if (root != null) root.SetActive(false);
    }

    private void OnDestroy()
    {
        DuelManager dm = DuelManager.Instance;
        if (dm == null) return;
        dm.OnDuelStarted -= HandleStarted;
        dm.OnPlayerTap -= HandlePlayerTap;
        dm.OnDuelEnded -= HandleEnded;
        dm.OnDuelCancelled -= HandleCancelled;
    }

    private void Update()
    {
        DuelManager dm = DuelManager.Instance;
        if (dm == null || !dm.IsDueling) return;

        float dt = Time.unscaledDeltaTime;

        if (gaugeSlider != null) gaugeSlider.value = Mathf.Lerp(gaugeSlider.value, dm.Gauge, 15f * dt);
        if (timerText != null) timerText.text = $"{dm.Remaining:0.0}";

        bool intro = dm.InIntro;
        if (introText != null) introText.gameObject.SetActive(intro);
        if (tapPrompt != null)
        {
            tapPrompt.gameObject.SetActive(!intro);
            punch = Mathf.MoveTowards(punch, 0f, punchRecover * dt);
            float s = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseScale + punch;
            tapPrompt.localScale = new Vector3(s, s, 1f);
        }

        if (shakeTarget != null)
        {
            float k = tapPunchScale > 0f ? punch / tapPunchScale : 0f;
            shakeTarget.anchoredPosition = shakeOrigin + Random.insideUnitCircle * shakeStrength * k;
        }
    }

    private void HandleStarted()
    {
        StopResult();
        punch = 0f;
        if (root != null) root.SetActive(true);
        if (resultText != null) resultText.gameObject.SetActive(false);
        if (introText != null)
        {
            introText.text = introMessage;
            introText.gameObject.SetActive(true);
        }
        if (gaugeSlider != null && DuelManager.Instance != null) gaugeSlider.value = DuelManager.Instance.Gauge;
    }

    private void HandlePlayerTap()
    {
        punch = tapPunchScale;
    }

    private void HandleEnded(bool playerWon, bool instantByFever)
    {
        StopResult();
        if (shakeTarget != null) shakeTarget.anchoredPosition = shakeOrigin;
        if (tapPrompt != null) tapPrompt.gameObject.SetActive(false);
        if (introText != null) introText.gameObject.SetActive(false);
        if (gaugeSlider != null) gaugeSlider.value = playerWon ? 1f : 0f;

        string msg = instantByFever ? feverWinMessage : (playerWon ? winMessage : loseMessage);
        resultRoutine = StartCoroutine(ShowResult(msg));
    }

    private void HandleCancelled()
    {
        StopResult();
        if (shakeTarget != null) shakeTarget.anchoredPosition = shakeOrigin;
        if (root != null) root.SetActive(false);
    }

    private IEnumerator ShowResult(string msg)
    {
        if (root != null) root.SetActive(true);
        if (resultText != null)
        {
            resultText.text = msg;
            resultText.gameObject.SetActive(true);
        }
        yield return new WaitForSecondsRealtime(resultShowTime);
        if (resultText != null) resultText.gameObject.SetActive(false);
        if (root != null) root.SetActive(false);
        resultRoutine = null;
    }

    private void StopResult()
    {
        if (resultRoutine != null) StopCoroutine(resultRoutine);
        resultRoutine = null;
    }
}