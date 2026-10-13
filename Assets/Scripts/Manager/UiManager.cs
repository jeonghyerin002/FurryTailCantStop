using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 흐름 + UI 관리.
/// 시작 → StartCanvas(현재 점수/최고 점수/시작 버튼) 표시
/// 시작 버튼 → StartCanvas 끄기, 상단 타이머 시작
/// 타이머 종료 → 게임 정지, 점수 저장 후 StartCanvas 다시 표시
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private PlayerInfo player;

    [Header("Start Canvas")]
    [SerializeField] private GameObject startPanel;
    [SerializeField] private TMP_Text currentScoreText;
    [SerializeField] private TMP_Text bestScoreText;
    [SerializeField] private Button startButton;

    [Header("게임 중 UI (상단)")]
    [Tooltip("타이머 텍스트를 담은 오브젝트 (게임 중에만 표시). 비워두면 timerText만 사용")]
    [SerializeField] private GameObject inGameUIRoot;
    [SerializeField] private TMP_Text timerText;
    [Tooltip("게임 중 점수 표시 (선택)")]
    [SerializeField] private TMP_Text inGameScoreText;

    [Header("타이머")]
    [Tooltip("게임 시간(초). 추후 120~180으로 변경")]
    [SerializeField] private float gameDuration = 30f;

    [Header("텍스트 형식 ({0} 자리에 숫자)")]
    [SerializeField] private string currentScoreFormat = "Score : {0}";
    [SerializeField] private string bestScoreFormat = "Best : {0}";
    [SerializeField] private string inGameScoreFormat = "{0}";

    public bool IsPlaying { get; private set; }
    public float RemainingTime { get; private set; }

    private TargetMover[] targets;

    private void Start()
    {
        if (player == null) player = FindFirstObjectByType<PlayerInfo>();
        targets = FindObjectsByType<TargetMover>(FindObjectsSortMode.None);

        if (startButton != null) startButton.onClick.AddListener(StartGame);
        if (ScoreManager.Instance != null) ScoreManager.Instance.OnScoreChanged += RefreshInGameScore;

        ShowStartScreen();
    }

    private void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(StartGame);
        if (ScoreManager.Instance != null) ScoreManager.Instance.OnScoreChanged -= RefreshInGameScore;
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
        Time.timeScale = 0f; // 시작 화면에서는 타겟도 정지 (UI 버튼은 정상 동작)

        if (inGameUIRoot != null) inGameUIRoot.SetActive(false);
        else if (timerText != null) timerText.gameObject.SetActive(false);

        RefreshStartCanvasScores();
        if (startPanel != null) startPanel.SetActive(true);
    }

    /// <summary>시작 버튼 OnClick</summary>
    public void StartGame()
    {
        if (startPanel != null) startPanel.SetActive(false);

        // 초기화
        if (ScoreManager.Instance != null) ScoreManager.Instance.ResetScore();
        foreach (TargetMover t in targets)
            if (t != null) t.ResetTarget();
        if (player != null)
        {
            player.ResetCaptives();
            if (player.TryGetComponent(out PlayerController controller)) controller.Stop();
        }

        // 타이머 시작
        RemainingTime = gameDuration;
        if (inGameUIRoot != null) inGameUIRoot.SetActive(true);
        else if (timerText != null) timerText.gameObject.SetActive(true);
        RefreshTimer();
        RefreshInGameScore(ScoreManager.Instance != null ? ScoreManager.Instance.CurrentScore : 0);

        Time.timeScale = 1f;
        IsPlaying = true;
        if (player != null) player.SetPlaying(true);
    }

    private void EndGame()
    {
        if (ScoreManager.Instance != null) ScoreManager.Instance.SaveScore();
        ShowStartScreen();
    }

    #endregion

    #region UI 갱신

    private void RefreshStartCanvasScores()
    {
        int current = ScoreManager.Instance != null ? ScoreManager.Instance.CurrentScore : 0;
        int best = ScoreManager.Instance != null ? ScoreManager.Instance.BestScore : 0;

        if (currentScoreText != null) currentScoreText.text = string.Format(currentScoreFormat, current);
        if (bestScoreText != null) bestScoreText.text = string.Format(bestScoreFormat, best);
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

    #endregion
}