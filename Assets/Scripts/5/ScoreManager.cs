using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public int score;
    public GameObject resultPanel;
    public TMP_Text scoreText;
    public TMP_Text bestText;

    public TMP_Text hudText;
    public GameObject newBestText;
    public GameObject startPanel;

    void Start()
    {
        Time.timeScale = 0f;
        startPanel.SetActive(true);
    }

    public void StartGame()
    {
        startPanel.SetActive(false);
        Time.timeScale = 1f;
    }

    public void AddScore(int amount)
    {
        score = Mathf.Max(0, score + amount);

        hudText.text = score.ToString();
    }
    public void GameOver()
    {
        string key = "Best_" + SceneManager.GetActiveScene().name;
        int best = PlayerPrefs.GetInt(key, 0);
        if (score > best)
        {
            best = score;
            PlayerPrefs.SetInt("BestScore", best);
            PlayerPrefs.Save();
            newBestText.SetActive(true);
        }
        scoreText.text = "SCORE " + score;
        bestText.text = "BEST " + best;
        resultPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}


