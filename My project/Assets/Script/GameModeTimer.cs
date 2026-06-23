using TMPro;
using UnityEngine;

public class GameModeTimer : MonoBehaviour
{
    [Header("Timer")]
    [SerializeField] private float countdownSeconds = 180f; // 3 minutes

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private string timePrefix = "Time: ";

    [Header("Scene Redirect")]
    [SerializeField] private string endCreditSceneName = "EndCreditPage";

    private float remainingTime;
    private bool hasTimeExpired;

    public bool HasTimeExpired => hasTimeExpired;

    private void Start()
    {
        remainingTime = Mathf.Max(0f, countdownSeconds);
        RefreshTimerText();
    }

    private void Update()
    {
        if (hasTimeExpired)
        {
            return;
        }

        remainingTime -= Time.deltaTime;

        if (remainingTime <= 0f)
        {
            remainingTime = 0f;
            RefreshTimerText();
            OnTimerExpired();
            return;
        }

        RefreshTimerText();
    }

    private void RefreshTimerText()
    {
        if (timerText == null)
        {
            return;
        }

        int totalSeconds = Mathf.CeilToInt(remainingTime);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        timerText.text = $"{timePrefix}{minutes:00}:{seconds:00}";
    }

    private void OnTimerExpired()
    {
        hasTimeExpired = true;
    }

    public void ResetTimer(float newDurationSeconds)
    {
        countdownSeconds = Mathf.Max(0f, newDurationSeconds);
        remainingTime = countdownSeconds;
        hasTimeExpired = false;
        RefreshTimerText();
    }
}
