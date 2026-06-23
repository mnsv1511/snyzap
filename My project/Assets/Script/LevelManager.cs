using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class LevelIntroUI : MonoBehaviour
{
    [Header("UI Reference")]
    public TextMeshProUGUI levelText;

    [Header("Settings")]
    public string levelLabel = "MISSION 1";
    public float displayDuration = 5f;

    [Header("Mission Outcome")]
    [SerializeField] private string missionFailText = "MISSION FAIL";
    [SerializeField] private string missionCompleteText = "MISSION COMPLETE";
    [SerializeField] private string startPageSceneName = "StartPage";
    [SerializeField] private string endCreditSceneName = "EndCreditPage";
    [SerializeField] private float redirectDelay = 1.5f;
    [SerializeField] private bool autoFindVegetablesIfListEmpty = true;
    [SerializeField] private Vegetable[] trackedVegetables;
    [SerializeField] private GameModeTimer gameModeTimer;

    private bool missionEnded;
    private Coroutine introHideCoroutine;

    private void Start()
    {
        ResolveReferences();

        levelText.text = levelLabel;
        levelText.gameObject.SetActive(true);
        introHideCoroutine = StartCoroutine(HideAfterDelay());
    }

    private void Update()
    {
        if (missionEnded)
        {
            return;
        }

        if (gameModeTimer != null && gameModeTimer.HasTimeExpired)
        {
            StartCoroutine(CompleteMissionAndRedirect());
            return;
        }

        if (AreAllTrackedVegetablesDead())
        {
            StartCoroutine(FailMissionAndRedirect());
        }
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);
        levelText.gameObject.SetActive(false);
    }

    private IEnumerator FailMissionAndRedirect()
    {
        missionEnded = true;
        ShowMissionText(missionFailText);

        float wait = Mathf.Max(0f, redirectDelay);
        if (wait > 0f)
        {
            yield return new WaitForSeconds(wait);
        }

        if (!string.IsNullOrWhiteSpace(startPageSceneName))
        {
            SceneManager.LoadScene(startPageSceneName);
        }
        else
        {
            Debug.LogError("LevelIntroUI: Start page scene name is empty.", this);
        }
    }

    private IEnumerator CompleteMissionAndRedirect()
    {
        missionEnded = true;
        ShowMissionText(missionCompleteText);

        float wait = Mathf.Max(0f, redirectDelay);
        if (wait > 0f)
        {
            yield return new WaitForSeconds(wait);
        }

        if (!string.IsNullOrWhiteSpace(endCreditSceneName))
        {
            SceneManager.LoadScene(endCreditSceneName);
        }
        else
        {
            Debug.LogError("LevelIntroUI: End credit scene name is empty.", this);
        }
    }

    private void ShowMissionText(string message)
    {
        if (levelText == null)
        {
            return;
        }

        if (introHideCoroutine != null)
        {
            StopCoroutine(introHideCoroutine);
            introHideCoroutine = null;
        }
        levelText.text = message;
        levelText.gameObject.SetActive(true);
    }

    private bool AreAllTrackedVegetablesDead()
    {
        if ((trackedVegetables == null || trackedVegetables.Length == 0) && autoFindVegetablesIfListEmpty)
        {
            trackedVegetables = FindObjectsOfType<Vegetable>();
        }

        if (trackedVegetables == null || trackedVegetables.Length == 0)
        {
            return false;
        }

        bool hasAnyValidVegetable = false;
        for (int i = 0; i < trackedVegetables.Length; i++)
        {
            Vegetable vegetable = trackedVegetables[i];
            if (vegetable == null)
            {
                continue;
            }

            hasAnyValidVegetable = true;
            if (!vegetable.IsDead)
            {
                return false;
            }
        }

        return hasAnyValidVegetable;
    }

    private void ResolveReferences()
    {
        if (levelText == null)
        {
            TextMeshProUGUI[] allTexts = FindObjectsOfType<TextMeshProUGUI>(true);
            for (int i = 0; i < allTexts.Length; i++)
            {
                if (allTexts[i] != null && allTexts[i].gameObject.name == "Mission")
                {
                    levelText = allTexts[i];
                    break;
                }
            }
        }

        if (gameModeTimer == null)
        {
            gameModeTimer = FindObjectOfType<GameModeTimer>();
        }
    }
}
