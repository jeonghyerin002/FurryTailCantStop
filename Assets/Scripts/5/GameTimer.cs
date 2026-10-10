using UnityEngine;
using TMPro;

public class GameTimer : MonoBehaviour
{
    public float timeLimit = 30f;
    public TMP_Text timeText;
    public ScoreManager scoreManager;

    void Update()
    {
        timeLimit -= Time.deltaTime;
        timeText.text = Mathf.CeilToInt(timeLimit).ToString();
        if (timeLimit <= 0f)
        {
            scoreManager.GameOver();
            enabled = false;
        }
    }
}
