using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    [SerializeField] private GameObject settingsPopup;

    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void LoadSettingsPopup()
    {
        if (settingsPopup == null)
        {
            Debug.LogWarning("SceneLoader: settingsPopup is not assigned.");
            return;
        }

        settingsPopup.SetActive(true);
    }

    public void CloseSettingsPopup()
    {
        if (settingsPopup == null)
        {
            Debug.LogWarning("SceneLoader: settingsPopup is not assigned.");
            return;
        }

        settingsPopup.SetActive(false);
    }

    public void LoadNextScene()
    {
        int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextIndex);
        }
        else
        {
            Debug.LogWarning("SceneLoader: no next scene available in Build Settings for " + SceneManager.GetActiveScene().name);
        }
    }

    public void ReloadCurrentScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitGame()
    {
        Debug.Log("SceneLoader: Quit requested.");
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
