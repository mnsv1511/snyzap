using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CutSceneManager : MonoBehaviour
{
    [Header("Cutscene Pages")]
    [SerializeField] private List<GameObject> pages; // Assign your page GameObjects here

    [Header("UI Elements")]
    [SerializeField] private Button skipButton;

    private enum AdvanceMode
    {
        LeftClick,
        Timed,
        ClickOrTimed
    }

    [Header("Timing Settings")]
    [SerializeField] private AdvanceMode advanceMode = AdvanceMode.LeftClick;
    [SerializeField] private float autoAdvanceDelay = 3.0f; // Seconds before page auto-advances when timed mode is active
    [SerializeField] private float clickCooldown = 0.5f;   // Time required between clicks
    [SerializeField] private float timeBeforeSkipAppears = 3.0f; // Seconds before skip button shows
    [SerializeField] private float pageTransitionDuration = 0.5f; // Fade duration between pages
    [SerializeField] private UnityEvent onCutsceneEnd;

    private int currentPageIndex = 0;
    private bool canAdvance = true;
    private bool isTransitioning = false;
    private Coroutine autoAdvanceCoroutine;

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (pages == null || pages.Count == 0)
        {
            Debug.LogError("No pages assigned to the Cutscene Manager!");
            return;
        }

        InitializePages();
        StartPageAdvanceTimer();

        if (skipButton != null)
        {
            skipButton.gameObject.SetActive(false);
            skipButton.onClick.AddListener(SkipCutscene);
            StartCoroutine(RevealSkipButtonRoutine());
        }
    }

    void Update()
    {
        if (advanceMode == AdvanceMode.Timed)
        {
            return;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && canAdvance && !isTransitioning)
        {
            StopPageAdvanceTimer();
            AdvancePage();
        }
    }

    private void InitializePages()
    {
        for (int i = 0; i < pages.Count; i++)
        {
            GameObject page = pages[i];
            if (page == null)
            {
                Debug.LogWarning($"Cutscene page at index {i} is null. Skipping.");
                continue;
            }

            CanvasGroup canvasGroup = GetOrAddCanvasGroup(page);
            page.SetActive(i == 0);
            canvasGroup.alpha = i == 0 ? 1f : 0f;
            canvasGroup.interactable = i == 0;
            canvasGroup.blocksRaycasts = i == 0;
        }

        currentPageIndex = 0;
    }

    private IEnumerator FadeBetweenPages(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= pages.Count || toIndex < 0 || toIndex >= pages.Count)
        {
            yield break;
        }

        isTransitioning = true;

        GameObject fromPage = pages[fromIndex];
        GameObject toPage = pages[toIndex];

        if (fromPage == null || toPage == null)
        {
            isTransitioning = false;
            yield break;
        }

        CanvasGroup fromGroup = GetOrAddCanvasGroup(fromPage);
        CanvasGroup toGroup = GetOrAddCanvasGroup(toPage);

        toPage.SetActive(true);
        toGroup.alpha = 0f;
        toGroup.interactable = false;
        toGroup.blocksRaycasts = false;

        float elapsed = 0f;
        while (elapsed < pageTransitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / pageTransitionDuration);
            toGroup.alpha = t;
            yield return null;
        }

        fromGroup.interactable = false;
        fromGroup.blocksRaycasts = false;
        fromPage.SetActive(false);

        toGroup.alpha = 1f;
        toGroup.interactable = true;
        toGroup.blocksRaycasts = true;

        currentPageIndex = toIndex;
        isTransitioning = false;
        StartPageAdvanceTimer();
    }

    private IEnumerator AdvanceCooldownRoutine()
    {
        yield return new WaitForSeconds(clickCooldown);
        canAdvance = true;
    }

    private IEnumerator RevealSkipButtonRoutine()
    {
        yield return new WaitForSeconds(timeBeforeSkipAppears);
        if (skipButton != null)
        {
            skipButton.gameObject.SetActive(true);
        }
    }

    private void StartPageAdvanceTimer()
    {
        if (advanceMode == AdvanceMode.LeftClick)
        {
            return;
        }

        StopPageAdvanceTimer();
        autoAdvanceCoroutine = StartCoroutine(AutoAdvanceRoutine());
    }

    private void StopPageAdvanceTimer()
    {
        if (autoAdvanceCoroutine != null)
        {
            StopCoroutine(autoAdvanceCoroutine);
            autoAdvanceCoroutine = null;
        }
    }

    private IEnumerator AutoAdvanceRoutine()
    {
        yield return new WaitForSeconds(autoAdvanceDelay);
        if (canAdvance && !isTransitioning)
        {
            AdvancePage();
        }
    }

    private void AdvancePage()
    {
        if (!canAdvance || isTransitioning)
        {
            return;
        }

        if (currentPageIndex + 1 >= pages.Count)
        {
            EndCutscene();
            return;
        }

        canAdvance = false;
        StartCoroutine(AdvanceCooldownRoutine());
        StartCoroutine(FadeBetweenPages(currentPageIndex, currentPageIndex + 1));
    }

    public void SkipCutscene()
    {
        Debug.Log("Cutscene Skipped!");
        EndCutscene();
    }

    private void EndCutscene()
    {
        Debug.Log("Cutscene Finished. Transitioning to game...");
        onCutsceneEnd?.Invoke();
        // TODO: Insert your scene transition or gameplay activation logic here
    }

    private static CanvasGroup GetOrAddCanvasGroup(GameObject page)
    {
        CanvasGroup canvasGroup = page.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = page.AddComponent<CanvasGroup>();
        }

        return canvasGroup;
    }
}
