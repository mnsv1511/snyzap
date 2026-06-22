using UnityEngine;
using System.Collections;

public class Monster : MonoBehaviour
{
    [SerializeField] private float movementSpeed = 5f;
    [SerializeField] private Transform targetVegetable;
    [SerializeField] private Vector3 fixedTargetPoint;
    [SerializeField] private bool useFixedTarget = false;
    [SerializeField] private float flattenDistance = 0.25f;
    [SerializeField] private float offScreenDestroyBuffer = 0.1f;

    [SerializeField] private Animator animator;
    [SerializeField] private string deathAnimationTrigger = "Death";
    [SerializeField] private AudioClip hitVoiceClip;
    [SerializeField] [Range(0f, 1f)] private float hitVoiceVolume = 1f;
    [SerializeField] private bool destroyImmediatelyOnHit = false;
    [SerializeField] private float fadeOutDuration = 0.4f;

    private bool isAlive = true;
    private bool isExitingScreen = false;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Vector2 movementDirection = Vector2.zero;
    private Vector2 exitDirection = Vector2.right;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    private void FixedUpdate()
    {
        if (!isAlive) return;

        if (isExitingScreen)
        {
            rb.velocity = exitDirection * movementSpeed;
            UpdateFacingDirection(exitDirection);

            if (IsOutsideMainCamera(transform.position))
            {
                Destroy(gameObject);
            }
            return;
        }

        UpdateTargetVegetable();
        if (!useFixedTarget && targetVegetable == null)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        Vector3 targetPosition = useFixedTarget ? fixedTargetPoint : targetVegetable.position;
        movementDirection = (targetPosition - transform.position).normalized;
        rb.velocity = movementDirection * movementSpeed;

        TryFlattenTargetByDistance();
        UpdateFacingDirection(movementDirection);
    }

    /// <summary>
    /// Find the nearest vegetable if target is not assigned and not using a fixed point
    /// </summary>
    private void UpdateTargetVegetable()
    {
        if (isExitingScreen)
        {
            return;
        }

        if (useFixedTarget)
        {
            return;
        }

        if (targetVegetable != null)
        {
            Vegetable assignedVegetable = targetVegetable.GetComponent<Vegetable>();
            if (assignedVegetable == null || !assignedVegetable.IsAlive)
            {
                targetVegetable = null;
            }
        }

        if (targetVegetable != null)
        {
            return;
        }

        Vegetable[] allVegetables = FindObjectsOfType<Vegetable>();
        float minDistance = float.MaxValue;
        Transform nearestVegetable = null;

        foreach (Vegetable veg in allVegetables)
        {
            if (veg.IsAlive)
            {
                float distance = Vector3.Distance(transform.position, veg.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestVegetable = veg.transform;
                }
            }
        }

        if (nearestVegetable != null)
        {
            targetVegetable = nearestVegetable;
        }
    }

    /// <summary>
    /// Called when monster is hit by player's shot
    /// </summary>
    public void TakeHit()
    {
        if (!isAlive) return;

        isAlive = false;
        rb.velocity = Vector2.zero;

        if (hitVoiceClip != null)
        {
            float scaledVolume = SettingManager.Instance != null
                ? SettingManager.Instance.GetScaledSoundEffectVolume(hitVoiceVolume)
                : hitVoiceVolume;
            AudioSource.PlayClipAtPoint(hitVoiceClip, transform.position, scaledVolume);
        }

        if (destroyImmediatelyOnHit)
        {
            Destroy(gameObject);
            return;
        }

        if (animator != null)
        {
            animator.SetTrigger(deathAnimationTrigger);
        }

        StartCoroutine(HandleDeathFadeOut());
    }

    /// <summary>
    /// Called when monster collides with a vegetable
    /// </summary>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isAlive) return;

        Vegetable vegetable = collision.GetComponent<Vegetable>();
        if (vegetable != null && vegetable.IsAlive)
        {
            vegetable.FlattenVegetable();
            BeginExitAfterFlatten();
        }
    }

    private void TryFlattenTargetByDistance()
    {
        if (useFixedTarget || targetVegetable == null)
        {
            return;
        }

        Vegetable vegetable = targetVegetable.GetComponent<Vegetable>();
        if (vegetable == null || !vegetable.IsAlive)
        {
            targetVegetable = null;
            return;
        }

        float distance = Vector2.Distance(transform.position, targetVegetable.position);
        if (distance <= flattenDistance)
        {
            vegetable.FlattenVegetable();
            BeginExitAfterFlatten();
        }
    }

    private void BeginExitAfterFlatten()
    {
        if (isExitingScreen)
        {
            return;
        }

        isExitingScreen = true;
        targetVegetable = null;

        if (movementDirection.sqrMagnitude > 0.0001f)
        {
            exitDirection = movementDirection.normalized;
        }
        else
        {
            exitDirection = spriteRenderer != null && spriteRenderer.flipX ? Vector2.left : Vector2.right;
        }
    }

    private void UpdateFacingDirection(Vector2 direction)
    {
        // Keep default orientation when moving right-to-left; flip only for left-to-right movement.
        if (direction.x != 0f && spriteRenderer != null)
        {
            spriteRenderer.flipX = direction.x > 0f;
        }
    }

    private bool IsOutsideMainCamera(Vector3 worldPosition)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return false;
        }

        Vector3 viewportPos = mainCamera.WorldToViewportPoint(worldPosition);
        if (viewportPos.z < 0f)
        {
            return true;
        }

        return viewportPos.x < -offScreenDestroyBuffer || viewportPos.x > 1f + offScreenDestroyBuffer ||
               viewportPos.y < -offScreenDestroyBuffer || viewportPos.y > 1f + offScreenDestroyBuffer;
    }

    /// <summary>
    /// Set the target vegetable or fixed target point
    /// </summary>
    public void SetTarget(Transform vegetableTarget, Vector3 fixedTarget = default, bool useFixed = false)
    {
        targetVegetable = vegetableTarget;
        fixedTargetPoint = fixedTarget;
        useFixedTarget = useFixed;
    }

    /// <summary>
    /// Set movement speed
    /// </summary>
    public void SetMovementSpeed(float speed)
    {
        movementSpeed = Mathf.Max(0.1f, speed);
    }

    public float GetMovementSpeed() => movementSpeed;
    public bool IsAlive => isAlive;

    /// <summary>
    /// Helper method to get animation clip length
    /// </summary>
    private float GetAnimationLength(string triggerName)
    {
        if (animator == null) return 0.5f;
        
        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        foreach (AnimationClip clip in clips)
        {
            if (clip.name == triggerName)
                return clip.length;
        }
        return 0.5f;
    }

    private IEnumerator HandleDeathFadeOut()
    {
        float deathAnimationDuration = animator != null ? GetAnimationLength(deathAnimationTrigger) : 0f;
        if (deathAnimationDuration > 0f)
        {
            yield return new WaitForSeconds(deathAnimationDuration);
        }

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        if (renderers == null || renderers.Length == 0)
        {
            Destroy(gameObject);
            yield break;
        }

        Color[] startColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            startColors[i] = renderers[i].color;
        }

        float safeFadeDuration = Mathf.Max(0.01f, fadeOutDuration);
        float elapsed = 0f;

        while (elapsed < safeFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / safeFadeDuration);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }

                Color c = startColors[i];
                c.a = Mathf.Lerp(startColors[i].a, 0f, t);
                renderers[i].color = c;
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}
