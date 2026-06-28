using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UISpriteAnimation : MonoBehaviour
{
    [SerializeField] private Image uiImage;
    [SerializeField] private Sprite[] animationFrames;
    [SerializeField] private float frameRate = 0.1f;
    [SerializeField] private bool loop = true;

    private Coroutine animationCoroutine;

    private void OnEnable()
    {
        // Stop any previously running animation before starting a new one.
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }
        animationCoroutine = StartCoroutine(PlayAnimation());
    }

    private void OnDisable()
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }
    }

    private IEnumerator PlayAnimation()
    {
        if (uiImage == null || animationFrames == null || animationFrames.Length == 0)
        {
            yield break; // Exit if there's nothing to animate
        }

        int currentFrame = 0;
        uiImage.sprite = animationFrames[currentFrame];

        // Use a WaitForSecondsRealtime to make the animation independent of game pause (Time.timeScale)
        var wait = new WaitForSecondsRealtime(frameRate);

        while (true)
        {
            yield return wait;

            currentFrame++;

            if (currentFrame >= animationFrames.Length)
            {
                if (!loop) yield break;
                currentFrame = 0;
            }

            uiImage.sprite = animationFrames[currentFrame];
        }
    }
}