using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // Remove this if you're using legacy Text, not TextMeshPro

public class LevelIntroUI : MonoBehaviour
{
    [Header("UI Reference")]
    public TextMeshProUGUI levelText;

    [Header("Settings")]
    public string levelLabel = "MISSION 1"; // Change per scene, or set dynamically
    public float displayDuration = 5f;

    void Start()
    {
        levelText.text = levelLabel;
        levelText.gameObject.SetActive(true);
        StartCoroutine(HideAfterDelay());
    }

    IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);
        levelText.gameObject.SetActive(false);
    }
}
