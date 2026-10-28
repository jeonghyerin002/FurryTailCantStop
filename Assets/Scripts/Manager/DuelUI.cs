using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 연타 대결 UI. 남은 시간 / 라이벌 수 / "TAP!" 연타 유도 이펙트 / 결과 표시.
/// 게이지는 화면 상단 LoveGaugeUI 슬라이더가 담당 (대결 시작 시 중앙으로 이동).
/// ※ root는 이 스크립트가 붙은 오브젝트가 아닌 "자식" 오브젝트로 지정하세요
///   (꺼진 오브젝트에선 코루틴이 동작하지 않음).
/// </summary>
public class DuelUI : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("대결 UI 전체 루트 (자식 오브젝트)")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text introText;
    [Tooltip("연타 유도 텍스트/이미지 (TAP! TAP!)")]
    [SerializeField] private RectTransform tapPrompt;
    [Tooltip("(선택) 대결 중인 라이벌 수 표시")]
    [SerializeField] private TMP_Text rivalCountText;
    [SerializeField] private TMP_Text resultText;

    [Header("문구")]
    [SerializeField] private string introMessage = "대결!";
    [Tooltip("{0} = 라이벌 수")]
    [SerializeField] private string rivalCountFormat = "라이벌 x{0}";
    [SerializeField] private string rivalJoinedMessage = "라이벌 합류!";
    [SerializeField] private string winMessage = "승리!";
    [SerializeField] private string loseMessage = "패배...";
    [SerializeField] private string feverWinMessage = "FEVER! 한 방에 날려버렸다!";
    [SerializeField] private float resultShowTime = 1f;

    [Header("연타 유도 연출")]
    [SerializeField] private float pulseSpeed = 12f;
    [SerializeField] private float pulseScale = 0.12f;
    [SerializeField] private float tapPunchScale = 0.35f;
    [SerializeField] private float punchRecover = 3f;
    [Tooltip("(선택) 탭할 때 흔들 UI")]
    [SerializeField] private RectTransform shakeTarget;
    [SerializeField] private float shakeStrength = 6f;

    private float punch;
    private float rivalCountPunch;
    private float joinedMessageTimer;
    private Coroutine resultRoutine;
    private Vector2 shakeOrigin;
    private Vector3 tapPromptBaseScale = Vector3.one;     // 에디터에서 맞춰둔 원래 크기
    private Vector3 rivalCountBaseScale = Vector3.one;

    private void Start()
    {
        if (shakeTarget != null) shakeOrigin = shakeTarget.anchoredPosition;
        if (tapPrompt != null) tapPromptBaseScale = tapPrompt.localScale;
        if (rivalCountText != null) rivalCountBaseScale = rivalCountText.rectTransform.localScale;

        DuelManager dm = DuelManager.Instance;
        if (dm != null)
        {
            dm.OnDuelStarted += HandleStarted;
            dm.OnPlayerTap += HandlePlayerTap;
            dm.OnRivalJoined += HandleRivalJoined;
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
        dm.OnRivalJoined -= HandleRivalJoined;
        dm.OnDuelEnded -= HandleEnded;
        dm.OnDuelCancelled -= HandleCancelled;
    }

    private void Update()
    {
        DuelManager dm = DuelManager.Instance;
        if (dm == null || !dm.IsDueling) return;

        float dt = Time.unscaledDeltaTime;

        if (timerText != null) timerText.text = $"{dm.Remaining:0.0}";

        // 라이벌 수 (합류하면 잠깐 크게 + "라이벌 합류!" 문구)
        if (rivalCountText != null)
        {
            joinedMessageTimer -= dt;
            rivalCountText.text = joinedMessageTimer > 0f
                ? rivalJoinedMessage
                : string.Format(rivalCountFormat, dm.RivalCount);
            rivalCountPunch = Mathf.MoveTowards(rivalCountPunch, 0f, punchRecover * dt);
            float rs = 1f + rivalCountPunch;
            rivalCountText.rectTransform.localScale = new Vector3(rivalCountBaseScale.x * rs, rivalCountBaseScale.y * rs, rivalCountBaseScale.z);
        }

        bool intro = dm.InIntro;
        if (introText != null) introText.gameObject.SetActive(intro);
        if (tapPrompt != null)
        {
            tapPrompt.gameObject.SetActive(!intro);
            punch = Mathf.MoveTowards(punch, 0f, punchRecover * dt);
            float s = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseScale + punch;
            tapPrompt.localScale = new Vector3(tapPromptBaseScale.x * s, tapPromptBaseScale.y * s, tapPromptBaseScale.z);
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
        rivalCountPunch = 0f;
        joinedMessageTimer = 0f;
        if (root != null) root.SetActive(true);
        if (resultText != null) resultText.gameObject.SetActive(false);
        if (rivalCountText != null)
        {
            rivalCountText.gameObject.SetActive(true);
            if (DuelManager.Instance != null)
                rivalCountText.text = string.Format(rivalCountFormat, DuelManager.Instance.RivalCount);
        }
        if (introText != null)
        {
            introText.text = introMessage;
            introText.gameObject.SetActive(true);
        }
    }

    private void HandlePlayerTap()
    {
        punch = tapPunchScale;
    }

    private void HandleRivalJoined(int count)
    {
        rivalCountPunch = 0.5f;
        joinedMessageTimer = 0.8f;
    }

    private void HandleEnded(bool playerWon, bool instantByFever)
    {
        StopResult();
        if (shakeTarget != null) shakeTarget.anchoredPosition = shakeOrigin;
        if (tapPrompt != null) tapPrompt.gameObject.SetActive(false);
        if (introText != null) introText.gameObject.SetActive(false);
        if (rivalCountText != null) rivalCountText.gameObject.SetActive(false);

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