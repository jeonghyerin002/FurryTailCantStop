using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public struct GradeRule
{
    public string grade;
    public int minScore;
}

/// <summary>
/// 게임 흐름 + UI 관리.
/// 시작 → StartCanvas(현재 점수/최고 점수/등급/시작 버튼) 표시
/// 시작 버튼 → 모든 상태 초기화, 상단 타이머 시작
/// 타이머(5분) 종료 → 게임 정지, 점수 저장 후 StartCanvas에 최종 스코어/등급 표시
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private PlayerInfo player;

    [Header("Start Canvas")]
    [SerializeField] private GameObject startPanel;
    [SerializeField] private TMP_Text currentScoreText;
    [SerializeField] private TMP_Text bestScoreText;
    [Tooltip("최종 등급 표시 (선택)")]
    [SerializeField] private TMP_Text gradeText;
    [Tooltip("최고 점수 갱신 시 켜지는 오브젝트 (선택)")]
    [SerializeField] private GameObject newBestBadge;
    [SerializeField] private Button startButton;

    [Header("게임 중 UI (상단)")]
    [Tooltip("타이머/점수/피버 게이지 등을 담은 오브젝트 (게임 중에만 표시). 비워두면 timerText만 사용")]
    [SerializeField] private GameObject inGameUIRoot;
    [SerializeField] private TMP_Text timerText;
    [Tooltip("게임 중 점수 표시 (선택)")]
    [SerializeField] private TMP_Text inGameScoreText;
    [Tooltip("게임 중 포로 수 표시 (선택)")]
    [SerializeField] private TMP_Text captiveText;

    [Header("타이머")]
    [Tooltip("게임 시간(초). 기획서: 5분 = 300초")]
    [SerializeField] private float gameDuration = 300f;

    [Header("등급 (점수 이상이면 해당 등급)")]
    [SerializeField]
    private GradeRule[] gradeRules =
    {
        new GradeRule { grade = "S", minScore = 200 },
        new GradeRule { grade = "A", minScore = 140 },
        new GradeRule { grade = "B", minScore = 90 },
        new GradeRule { grade = "C", minScore = 45 },
        new GradeRule { grade = "D", minScore = 0 },
    };

    [Header("텍스트 형식 ({0} 자리에 값)")]
    [SerializeField] private string currentScoreFormat = "Score : {0}";
    [SerializeField] private string bestScoreFormat = "Best : {0}";
    [SerializeField] private string gradeFormat = "Rank : {0}";
    [SerializeField] private string inGameScoreFormat = "{0}";
    [SerializeField] private string captiveFormat = "x {0}";

    public bool IsPlaying { get; private set; }
    public float RemainingTime { get; private set; }

    private TargetMover[] targets;
    private RivalAI[] rivals;
    private bool lastGameWasNewBest;

    private void Start()
    {
        if (player == null) player = FindFirstObjectByType<PlayerInfo>();
        targets = FindObjectsByType<TargetMover>(FindObjectsSortMode.None);
        rivals = FindObjectsByType<RivalAI>(FindObjectsSortMode.None);

        if (startButton != null) startButton.onClick.AddListener(StartGame);
        if (ScoreManager.Instance != null) ScoreManager.Instance.OnScoreChanged += RefreshInGameScore;
        if (player != null) player.OnCaptiveChanged += RefreshCaptives;

        ShowStartScreen();
    }

    private void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(StartGame);
        if (ScoreManager.Instance != null) ScoreManager.Instance.OnScoreChanged -= RefreshInGameScore;
        if (player != null) player.OnCaptiveChanged -= RefreshCaptives;
    }

    private void Update()
    {
        if (!IsPlaying) return;

        RemainingTime -= Time.deltaTime;
        if (RemainingTime <= 0f)
        {
            RemainingTime = 0f;
            RefreshTimer();
            EndGame();
            return;
        }
        RefreshTimer();
    }

    #region 게임 흐름

    private void ShowStartScreen()
    {
        IsPlaying = false;
        if (player != null) player.SetPlaying(false);
        Time.timeScale = 0f; // 시작 화면에서는 타겟/라이벌도 정지 (UI 버튼은 정상 동작)

        if (inGameUIRoot != null) inGameUIRoot.SetActive(false);
        else if (timerText != null) timerText.gameObject.SetActive(false);

        RefreshStartCanvasScores();
        if (newBestBadge != null) newBestBadge.SetActive(lastGameWasNewBest);
        if (startPanel != null) startPanel.SetActive(true);
    }

    /// <summary>시작 버튼 OnClick</summary>
    public void StartGame()
    {
        if (startPanel != null) startPanel.SetActive(false);
        lastGameWasNewBest = false;

        // 초기화
        if (DuelManager.Instance != null) DuelManager.Instance.ResetDuel();
        if (FeverManager.Instance != null) FeverManager.Instance.ResetFever();
        if (ScoreManager.Instance != null) ScoreManager.Instance.ResetScore();

        foreach (TargetMover t in targets)
            if (t != null) t.ResetTarget();
        foreach (RivalAI r in rivals)
            if (r != null) r.ResetRival();

        if (player != null)
        {
            player.ResetCaptives();
            if (player.TryGetComponent(out StunController stun)) stun.ResetState();
            if (player.TryGetComponent(out PlayerAttack attack)) attack.CancelAttack();
            if (player.TryGetComponent(out PlayerController controller)) controller.ResetToStart();
        }

        // 타이머 시작
        RemainingTime = gameDuration;
        if (inGameUIRoot != null) inGameUIRoot.SetActive(true);
        else if (timerText != null) timerText.gameObject.SetActive(true);
        RefreshTimer();
        RefreshInGameScore(ScoreManager.Instance != null ? ScoreManager.Instance.CurrentScore : 0);
        RefreshCaptives(player != null ? player.Captives : 0);

        Time.timeScale = 1f;
        IsPlaying = true;
        if (player != null) player.SetPlaying(true);
    }

    private void EndGame()
    {
        if (DuelManager.Instance != null) DuelManager.Instance.ResetDuel();
        if (FeverManager.Instance != null) FeverManager.Instance.ResetFever();
        if (player != null && player.TryGetComponent(out PlayerAttack attack)) attack.CancelAttack();

        if (ScoreManager.Instance != null) lastGameWasNewBest = ScoreManager.Instance.SaveScore();
        ShowStartScreen();
    }

    #endregion

    #region 등급

    public string GetGrade(int score)
    {
        string best = "-";
        int bestMin = int.MinValue;
        foreach (GradeRule rule in gradeRules)
        {
            if (score >= rule.minScore && rule.minScore > bestMin)
            {
                bestMin = rule.minScore;
                best = rule.grade;
            }
        }
        return best;
    }

    #endregion

    #region UI 갱신

    private void RefreshStartCanvasScores()
    {
        int current = ScoreManager.Instance != null ? ScoreManager.Instance.CurrentScore : 0;
        int best = ScoreManager.Instance != null ? ScoreManager.Instance.BestScore : 0;

        if (currentScoreText != null) currentScoreText.text = string.Format(currentScoreFormat, current);
        if (bestScoreText != null) bestScoreText.text = string.Format(bestScoreFormat, best);
        if (gradeText != null) gradeText.text = string.Format(gradeFormat, current > 0 ? GetGrade(current) : "-");
    }

    private void RefreshTimer()
    {
        if (timerText == null) return;
        int sec = Mathf.CeilToInt(RemainingTime);
        timerText.text = $"{sec / 60:00}:{sec % 60:00}";
    }

    private void RefreshInGameScore(int score)
    {
        if (inGameScoreText != null) inGameScoreText.text = string.Format(inGameScoreFormat, score);
    }

    private void RefreshCaptives(int count)
    {
        if (captiveText != null) captiveText.text = string.Format(captiveFormat, count);
    }

    #endregion
}