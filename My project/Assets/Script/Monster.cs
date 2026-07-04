using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Serialization;

public class Monster : MonoBehaviour
{
    [SerializeField] private float movementSpeed = 5f;
    [SerializeField] private Transform targetVegetable;
    [SerializeField] private Vector3 fixedTargetPoint;
    [SerializeField] private bool useFixedTarget = false;
    [SerializeField] private float flattenDistance = 0.01f;
    [SerializeField] private float offScreenDestroyBuffer = 0.1f;

    [SerializeField] private Animator animator;
    [SerializeField] private GameObject walkVfxPrefab;
    [SerializeField] private Vector3 walkVfxLocalPosition = Vector3.zero;
    [SerializeField] private string deathAnimationTrigger = "Death";
    [SerializeField] private string deathAnimationClipName = "Anim_Monster_Death";
    [FormerlySerializedAs("monsterWalkClip")]
    [SerializeField] private AudioClip walkSfxClip;
    [FormerlySerializedAs("monsterWalkVolume")]
    [SerializeField] [Range(0f, 5f)] private float walkSfxVolume = 0.6f;
    [FormerlySerializedAs("monsterDeadClip")]
    [SerializeField] private AudioClip deathSfxClip;
    [FormerlySerializedAs("monsterDeadVolume")]
    [SerializeField] [Range(0f, 5f)] private float deathSfxVolume = 1f;
    [SerializeField] private bool destroyImmediatelyOnHit = false;
    [SerializeField] private float fadeOutDuration = 0.4f;
    [SerializeField] private bool passThroughOtherMonsters = true;

    private bool isAlive = true;
    private bool isExitingScreen = false;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Vector2 movementDirection = Vector2.zero;
    private Vector2 exitDirection = Vector2.right;
    private bool componentsInitialized = false;
    private Collider2D[] ownColliders;
    private GameObject walkVfxInstance;
    private Vector3 walkVfxBaseLocalScale = Vector3.one;
    private AudioSource runtimeLoopSfxAudioSource;
    private AudioSource oneShotSfxAudioSource;
    private readonly List<AudioSource> registeredSoundEffectSources = new List<AudioSource>();
    private bool isWalkSfxPlaying = false;

    private static readonly List<Collider2D> activeMonsterColliders = new List<Collider2D>();

    private void Awake()
    {
        EnsureComponentsInitialized();
        EnsureAudioSourceInitialized();
        RegisterAudioSourcesForSettings();
    }

    private void Start()
    {
        EnsureComponentsInitialized();
    }

    private void OnDestroy()
    {
        UpdateWalkLoopSfx(false);
        DestroyWalkVfx();
        UnregisterAudioSourcesForSettings();
        UnregisterMonsterColliders();
    }

    private void OnMouseDown()
    {
        if (!isAlive)
        {
            return;
        }

        TakeHit();
    }

    private void FixedUpdate()
    {
        if (!isAlive) return;

        if (isExitingScreen)
        {
            rb.velocity = exitDirection * movementSpeed;
            UpdateFacingDirection(exitDirection);
            UpdateWalkVfx(true);
            UpdateWalkLoopSfx(rb.velocity.sqrMagnitude > 0.0001f);

            if (IsOutsideMainCamera(transform.position))
            {
                Destroy(gameObject);
            }
            return;
        }

        TryFlattenAnyNearbyVegetable();
        if (isExitingScreen)
        {
            return;
        }

        UpdateTargetVegetable();
        if (!useFixedTarget && targetVegetable == null)
        {
            rb.velocity = Vector2.zero;
            UpdateWalkLoopSfx(false);
            return;
        }

        Vector3 targetPosition = useFixedTarget ? fixedTargetPoint : targetVegetable.position;
        movementDirection = (targetPosition - transform.position).normalized;
        rb.velocity = movementDirection * movementSpeed;

        TryFlattenTargetByDistance();
        UpdateFacingDirection(movementDirection);
        UpdateWalkVfx(rb.velocity.sqrMagnitude > 0.0001f);
        UpdateWalkLoopSfx(rb.velocity.sqrMagnitude > 0.0001f);
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

        EnsureComponentsInitialized();

        isAlive = false;
        DestroyWalkVfx();
        UpdateWalkLoopSfx(false);
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
        }

        PlayDetachedSfx(deathSfxClip, deathSfxVolume);

        bool canPlayDeathAnimation = animator != null &&
                                     (!string.IsNullOrWhiteSpace(deathAnimationClipName) ||
                                      !string.IsNullOrWhiteSpace(deathAnimationTrigger));

        if (destroyImmediatelyOnHit && !canPlayDeathAnimation)
        {
            Destroy(gameObject);
            return;
        }

        PlayDeathAnimation();

        StartCoroutine(HandleDeathFadeOut());
    }

    private void PlayDeathAnimation()
    {
        if (animator == null)
        {
            return;
        }

        animator.enabled = true;
        bool played = false;

        if (!string.IsNullOrWhiteSpace(deathAnimationTrigger) && HasAnimatorTrigger(deathAnimationTrigger))
        {
            animator.ResetTrigger(deathAnimationTrigger);
            animator.SetTrigger(deathAnimationTrigger);
            played = true;
        }

        if (!played && !string.IsNullOrWhiteSpace(deathAnimationClipName))
        {
            int stateHash = Animator.StringToHash(deathAnimationClipName);
            for (int i = 0; i < animator.layerCount; i++)
            {
                if (animator.HasState(i, stateHash))
                {
                    animator.Play(stateHash, i, 0f);
                    played = true;
                    break;
                }
            }
        }

        if (played)
        {
            // Force an immediate animator evaluation so first-hit death is visible reliably.
            animator.Update(0f);
        }
    }

    private bool HasAnimatorTrigger(string triggerName)
    {
        if (animator == null)
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Trigger &&
                string.Equals(parameters[i].name, triggerName, System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Called when monster collides with a vegetable
    /// </summary>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isAlive) return;

        Vegetable vegetable = collision.GetComponent<Vegetable>();
        if (vegetable == null)
        {
            vegetable = collision.GetComponentInParent<Vegetable>();
        }

        if (vegetable != null)
        {
            TryFlattenVegetableIfInRange(vegetable);
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (!isAlive) return;

        Vegetable vegetable = collision.GetComponent<Vegetable>();
        if (vegetable == null)
        {
            vegetable = collision.GetComponentInParent<Vegetable>();
        }

        if (vegetable != null)
        {
            TryFlattenVegetableIfInRange(vegetable);
        }
    }

    private void TryFlattenAnyNearbyVegetable()
    {
        Vegetable[] allVegetables = FindObjectsOfType<Vegetable>();
        for (int i = 0; i < allVegetables.Length; i++)
        {
            if (TryFlattenVegetableIfInRange(allVegetables[i]))
            {
                return;
            }
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

        if (IsWithinFlattenDistance(targetVegetable))
        {
            vegetable.MarkDeadByMonster();
            BeginExitAfterFlatten();
        }
    }

    private bool TryFlattenVegetableIfInRange(Vegetable vegetable)
    {
        if (vegetable == null || !vegetable.IsAlive)
        {
            return false;
        }

        if (!IsWithinFlattenDistance(vegetable.transform))
        {
            return false;
        }

        vegetable.MarkDeadByMonster();
        BeginExitAfterFlatten();
        return true;
    }

    private bool IsWithinFlattenDistance(Transform vegetableTransform)
    {
        if (vegetableTransform == null)
        {
            return false;
        }

        float safeFlattenDistance = Mathf.Max(0f, flattenDistance);
        Vector2 monsterPosition = rb != null ? rb.position : (Vector2)transform.position;

        Collider2D[] vegetableColliders = vegetableTransform.GetComponentsInChildren<Collider2D>();
        if (vegetableColliders != null && vegetableColliders.Length > 0)
        {
            float minSqrDistance = float.MaxValue;
            for (int i = 0; i < vegetableColliders.Length; i++)
            {
                Collider2D col = vegetableColliders[i];
                if (col == null || !col.enabled)
                {
                    continue;
                }

                Vector2 closestPoint = col.ClosestPoint(monsterPosition);
                float sqrDistance = (closestPoint - monsterPosition).sqrMagnitude;
                if (sqrDistance < minSqrDistance)
                {
                    minSqrDistance = sqrDistance;
                }
            }

            if (minSqrDistance < float.MaxValue)
            {
                return minSqrDistance <= safeFlattenDistance * safeFlattenDistance;
            }
        }

        float centerDistance = Vector2.Distance(monsterPosition, vegetableTransform.position);
        return centerDistance <= safeFlattenDistance;
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

    private void UpdateWalkVfx(bool shouldBeActive)
    {
        if (walkVfxPrefab == null)
        {
            return;
        }

        if (shouldBeActive)
        {
            if (walkVfxInstance == null)
            {
                walkVfxInstance = Instantiate(walkVfxPrefab, transform);
                walkVfxInstance.transform.localPosition = walkVfxLocalPosition;
                walkVfxInstance.transform.localRotation = Quaternion.identity;
                walkVfxBaseLocalScale = walkVfxInstance.transform.localScale;
            }

            float xSign = spriteRenderer != null && spriteRenderer.flipX ? -1f : 1f;
            walkVfxInstance.transform.localScale = new Vector3(
                Mathf.Abs(walkVfxBaseLocalScale.x) * xSign,
                walkVfxBaseLocalScale.y,
                walkVfxBaseLocalScale.z);

            if (!walkVfxInstance.activeSelf)
            {
                walkVfxInstance.SetActive(true);
            }

            return;
        }

        DestroyWalkVfx();
    }

    private void DestroyWalkVfx()
    {
        if (walkVfxInstance != null)
        {
            Destroy(walkVfxInstance);
            walkVfxInstance = null;
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

        if (animator.runtimeAnimatorController == null)
        {
            return 0.5f;
        }

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;

        if (!string.IsNullOrWhiteSpace(deathAnimationClipName))
        {
            foreach (AnimationClip clip in clips)
            {
                if (string.Equals(clip.name, deathAnimationClipName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return clip.length;
                }
            }
        }

        foreach (AnimationClip clip in clips)
        {
            if (string.Equals(clip.name, triggerName, System.StringComparison.OrdinalIgnoreCase) ||
                clip.name.IndexOf(triggerName, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return clip.length;
            }
        }

        return 0.5f;
    }

    private IEnumerator HandleDeathFadeOut()
    {
        // Ensure at least one frame renders the death pose before waiting/fading.
        yield return null;

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

    private void EnsureComponentsInitialized()
    {
        if (componentsInitialized)
        {
            return;
        }

        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        ownColliders = GetComponentsInChildren<Collider2D>();
        RegisterMonsterColliders();

        UpdateWalkVfx(false);

        componentsInitialized = true;
    }

    private void RegisterMonsterColliders()
    {
        if (!passThroughOtherMonsters || ownColliders == null || ownColliders.Length == 0)
        {
            return;
        }

        for (int i = 0; i < ownColliders.Length; i++)
        {
            Collider2D current = ownColliders[i];
            if (current == null)
            {
                continue;
            }

            for (int j = 0; j < activeMonsterColliders.Count; j++)
            {
                Collider2D other = activeMonsterColliders[j];
                if (other == null || other == current)
                {
                    continue;
                }

                Physics2D.IgnoreCollision(current, other, true);
            }

            if (!activeMonsterColliders.Contains(current))
            {
                activeMonsterColliders.Add(current);
            }
        }
    }

    private void UnregisterMonsterColliders()
    {
        if (ownColliders == null || ownColliders.Length == 0)
        {
            return;
        }

        for (int i = 0; i < ownColliders.Length; i++)
        {
            Collider2D current = ownColliders[i];
            if (current == null)
            {
                continue;
            }

            activeMonsterColliders.Remove(current);
        }
    }

    private void RegisterAudioSourcesForSettings()
    {
        if (SettingManager.Instance == null)
        {
            return;
        }

        AudioSource[] audioSources = GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < audioSources.Length; i++)
        {
            AudioSource source = audioSources[i];
            if (source == null)
            {
                continue;
            }

            if (source == runtimeLoopSfxAudioSource || source == oneShotSfxAudioSource)
            {
                continue;
            }

            registeredSoundEffectSources.Add(source);
            SettingManager.Instance.RegisterSoundEffectSource(source);
        }
    }

    private void UnregisterAudioSourcesForSettings()
    {
        if (SettingManager.Instance == null)
        {
            return;
        }

        for (int i = 0; i < registeredSoundEffectSources.Count; i++)
        {
            AudioSource source = registeredSoundEffectSources[i];
            if (source == null)
            {
                continue;
            }

            SettingManager.Instance.UnregisterSoundEffectSource(source);
        }

        registeredSoundEffectSources.Clear();
    }

    private void EnsureAudioSourceInitialized()
    {
        if (runtimeLoopSfxAudioSource != null && oneShotSfxAudioSource != null)
        {
            ConfigureSfxSource(runtimeLoopSfxAudioSource, allowLoop: true);
            ConfigureSfxSource(oneShotSfxAudioSource, allowLoop: false);
            return;
        }

        runtimeLoopSfxAudioSource = GetComponent<AudioSource>();
        if (runtimeLoopSfxAudioSource == null)
        {
            runtimeLoopSfxAudioSource = gameObject.AddComponent<AudioSource>();
        }

        oneShotSfxAudioSource = gameObject.AddComponent<AudioSource>();

        ConfigureSfxSource(runtimeLoopSfxAudioSource, allowLoop: true);
        ConfigureSfxSource(oneShotSfxAudioSource, allowLoop: false);
    }

    private static void ConfigureSfxSource(AudioSource source, bool allowLoop)
    {
        if (source == null)
        {
            return;
        }

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.mute = false;
        source.volume = allowLoop ? source.volume : 1f;
    }

    private void PlaySfxOneShot(AudioClip clip, float volume)
    {
        if (clip == null)
        {
            return;
        }

        float scaledVolume = SettingManager.Instance != null
            ? SettingManager.Instance.GetScaledSoundEffectVolume(volume)
            : volume;

        if (oneShotSfxAudioSource != null)
        {
            oneShotSfxAudioSource.PlayOneShot(clip, scaledVolume);
            return;
        }

        PlayDetachedSfx(clip, volume);
    }

    private void PlayDetachedSfx(AudioClip clip, float volume)
    {
        if (clip == null)
        {
            return;
        }

        float scaledVolume = SettingManager.Instance != null
            ? SettingManager.Instance.GetScaledSoundEffectVolume(volume)
            : volume;

        if (runtimeLoopSfxAudioSource != null)
        {
            runtimeLoopSfxAudioSource.PlayOneShot(clip, scaledVolume);
            return;
        }

        GameObject tempAudioObject = new GameObject("MonsterSfxDetached");
        tempAudioObject.transform.position = transform.position;

        AudioSource tempSource = tempAudioObject.AddComponent<AudioSource>();
        tempSource.playOnAwake = false;
        tempSource.spatialBlend = 0f;
        tempSource.volume = 1f;
        tempSource.PlayOneShot(clip, scaledVolume);

        Destroy(tempAudioObject, Mathf.Max(1f, clip.length + 0.25f));
    }

    private void UpdateWalkLoopSfx(bool shouldPlay)
    {
        if (runtimeLoopSfxAudioSource == null || walkSfxClip == null)
        {
            isWalkSfxPlaying = false;
            return;
        }

        if (!shouldPlay)
        {
            if (isWalkSfxPlaying)
            {
                runtimeLoopSfxAudioSource.Stop();
                isWalkSfxPlaying = false;
            }

            return;
        }

        float scaledVolume = SettingManager.Instance != null
            ? SettingManager.Instance.GetScaledSoundEffectVolume(walkSfxVolume)
            : walkSfxVolume;

        if (isWalkSfxPlaying && runtimeLoopSfxAudioSource.isPlaying)
        {
            runtimeLoopSfxAudioSource.volume = scaledVolume;
            return;
        }

        runtimeLoopSfxAudioSource.clip = walkSfxClip;
        runtimeLoopSfxAudioSource.loop = true;
        runtimeLoopSfxAudioSource.volume = scaledVolume;
        runtimeLoopSfxAudioSource.Play();
        isWalkSfxPlaying = true;
    }
}
