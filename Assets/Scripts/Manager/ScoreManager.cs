using System;
using UnityEngine;

/// <summary>
/// 현재 점수 / 최고 점수 관리 (최고 점수와 마지막 점수는 PlayerPrefs에 저장).
/// 씬에 하나만 두세요.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    private const string BestScoreKey = "BestScore";
    private const string LastScoreKey = "LastScore";

    /// <summary>현재(또는 마지막 판) 점수</summary>
    public int CurrentScore { get; private set; }
    public int BestScore { get; private set; }

    public event Action<int> OnScoreChanged;
    public event Action<int> OnBestScoreChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        BestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
        CurrentScore = PlayerPrefs.GetInt(LastScoreKey, 0); // 앱을 다시 켜도 마지막 점수 표시
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>새 게임 시작 시 0으로 초기화</summary>
    public void ResetScore()
    {
        CurrentScore = 0;
        OnScoreChanged?.Invoke(CurrentScore);
    }

    public void AddScore(int amount)
    {
        CurrentScore += amount;
        OnScoreChanged?.Invoke(CurrentScore);
    }

    /// <summary>게임 종료 시 호출. 마지막 점수 저장 + 최고 점수 갱신 시 true</summary>
    public bool SaveScore()
    {
        PlayerPrefs.SetInt(LastScoreKey, CurrentScore);

        bool isNewBest = CurrentScore > BestScore;
        if (isNewBest)
        {
            BestScore = CurrentScore;
            PlayerPrefs.SetInt(BestScoreKey, BestScore);
            OnBestScoreChanged?.Invoke(BestScore);
        }

        PlayerPrefs.Save();
        return isNewBest;
    }

    [ContextMenu("최고 점수 초기화")]
    public void ResetBestScore()
    {
        BestScore = 0;
        PlayerPrefs.DeleteKey(BestScoreKey);
        PlayerPrefs.DeleteKey(LastScoreKey);
        PlayerPrefs.Save();
        OnBestScoreChanged?.Invoke(BestScore);
    }
}