using UnityEngine;
using UnityEngine.SceneManagement;

public class PageManager : MonoBehaviour
{
    [Header("Page Objects")]
    [SerializeField] private GameObject startPage;
    [SerializeField] private GameObject settingPage;
    [SerializeField] private GameObject cutScenePage1;
    [SerializeField] private GameObject cutScenePage2;
    [SerializeField] private GameObject levelPage;
    [SerializeField] private GameObject gameModePage;
    [SerializeField] private GameObject endCreditPage;
    [SerializeField] private GameObject gameOverPage;
    [SerializeField] private GameObject victoryPage;

    [Header("Game Management")]
    [SerializeField] private SpawnManager spawnManager;
    [SerializeField] private Vegetable[] vegetables;

    private int currentLevel = 1;
    private const int maxLevel = 5; // Can be 3 or 5 as mentioned in doc

    private void Start()
    {
        ShowStartPage();
    }

    #region Page Navigation

    public void ShowStartPage()
    {
        HideAllPages();
        if (startPage != null) startPage.SetActive(true);
    }

    public void ShowSettingPage()
    {
        HideAllPages();
        if (settingPage != null) settingPage.SetActive(true);
    }

    public void ShowCutScene1()
    {
        HideAllPages();
        if (cutScenePage1 != null) cutScenePage1.SetActive(true);
    }

    public void ShowCutScene2()
    {
        HideAllPages();
        if (cutScenePage2 != null) cutScenePage2.SetActive(true);
    }

    public void ShowLevelPage()
    {
        HideAllPages();
        if (levelPage != null) levelPage.SetActive(true);
    }

    public void ShowGameModePage()
    {
        HideAllPages();
        if (gameModePage != null) gameModePage.SetActive(true);
        
        // Initialize game
        if (spawnManager != null)
        {
            spawnManager.ResetWaves();
        }
        
        ReviveAllVegetables();
    }

    public void ShowEndCreditPage()
    {
        HideAllPages();
        if (endCreditPage != null) endCreditPage.SetActive(true);
    }

    public void ShowGameOverPage()
    {
        HideAllPages();
        if (gameOverPage != null) gameOverPage.SetActive(true);
    }

    public void ShowVictoryPage()
    {
        HideAllPages();
        if (victoryPage != null) victoryPage.SetActive(true);
    }

    private void HideAllPages()
    {
        if (startPage != null) startPage.SetActive(false);
        if (settingPage != null) settingPage.SetActive(false);
        if (cutScenePage1 != null) cutScenePage1.SetActive(false);
        if (cutScenePage2 != null) cutScenePage2.SetActive(false);
        if (levelPage != null) levelPage.SetActive(false);
        if (gameModePage != null) gameModePage.SetActive(false);
        if (endCreditPage != null) endCreditPage.SetActive(false);
        if (gameOverPage != null) gameOverPage.SetActive(false);
        if (victoryPage != null) victoryPage.SetActive(false);
    }

    #endregion

    #region Game Flow

    public void StartGame()
    {
        // Flow: Start Page -> Cut Scene 1 -> Level Page
        ShowCutScene1();
    }

    public void ContinueFromCutScene1()
    {
        ShowCutScene2();
    }

    public void ContinueFromCutScene2()
    {
        ShowLevelPage();
    }

    public void StartLevel()
    {
        // Flow: Level Page -> Game Mode Page
        ShowGameModePage();
    }

    public void RestartLevel()
    {
        // Clear all level
        currentLevel = 1;
        ShowGameModePage();
    }

    public void NextLevel()
    {
        currentLevel++;
        if (currentLevel > maxLevel)
        {
            // All levels completed
            ShowVictoryPage();
        }
        else
        {
            ShowGameModePage();
        }
    }

    public void GameOver()
    {
        // When Junimo is all flattened (แบน)
        if (spawnManager != null)
        {
            spawnManager.StopSpawning();
        }
        ShowGameOverPage();
    }

    public void OnVictory()
    {
        // When all monsters are killed (complete mission)
        if (spawnManager != null)
        {
            spawnManager.StopSpawning();
        }
        ShowVictoryPage();
    }

    public void BackToStartPage()
    {
        // From any page, go back to start
        Time.timeScale = 1f; // Resume time if paused
        ShowStartPage();
    }

    public void ExitGame()
    {
        Debug.Log("Exiting game...");
        Application.Quit();
    }

    #endregion

    #region Helper Methods

    private void ReviveAllVegetables()
    {
        if (vegetables == null || vegetables.Length == 0)
        {
            vegetables = FindObjectsOfType<Vegetable>();
        }

        foreach (Vegetable veg in vegetables)
        {
            if (veg.CurrentState != Vegetable.VegetableState.Alive)
            {
                veg.ReviveVegetable();
            }
        }
    }

    public bool AreAllVegetablesDead()
    {
        if (vegetables == null || vegetables.Length == 0)
        {
            vegetables = FindObjectsOfType<Vegetable>();
        }

        foreach (Vegetable veg in vegetables)
        {
            if (veg.IsAlive)
            {
                return false;
            }
        }

        return true;
    }

    public int GetCurrentLevel() => currentLevel;
    public int GetMaxLevel() => maxLevel;

    #endregion
}
