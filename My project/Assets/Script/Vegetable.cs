using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Serialization;

public class Vegetable : MonoBehaviour
{
    public static event System.Action<Vegetable> VegetableDied;

    public enum VegetableState { Alive, Flattened }
    private VegetableState currentState = VegetableState.Alive;
    [SerializeField] private bool isDead = false;

    [SerializeField] private float movementSpeed = 1f;
    [SerializeField] private float patrolDistanceEachSide = 1f;
    [SerializeField] private Collider2D confinementArea;
    [SerializeField] private Bounds confinementBounds;
    [SerializeField] private bool useConfinementBounds = true;

    [SerializeField] private Animator animator;
    [SerializeField] private string walkAnimationParam = "IsWalking";
    [SerializeField] private string idleStateName = "Idle";
    [SerializeField] private string walkStateName = "Walk";
    [SerializeField] private float initialIdleDuration = 1f;
    [SerializeField] private string deathAnimationTrigger = "Death";
    [SerializeField] private string deathAnimationStateName = "Dead";
    [SerializeField] private string flattenAnimationTrigger = "Flatten";
    [SerializeField] private string missionCompleteJumpTrigger = "Jump";
    [SerializeField] private string missionCompleteJumpStateName = "Jump";
    [FormerlySerializedAs("vegetableWalkClip")]
    [SerializeField] private AudioClip walkSfxClip;
    [FormerlySerializedAs("vegetableWalkVolume")]
    [SerializeField] [Range(0f, 5f)] private float walkSfxVolume = 0.6f;
    [FormerlySerializedAs("vegetableFlatClip")]
    [SerializeField] private AudioClip flattenSfxClip;
    [FormerlySerializedAs("vegetableFlatVolume")]
    [SerializeField] [Range(0f, 5f)] private float flattenSfxVolume = 1f;
    [SerializeField] private GameObject deathVfxPrefab;
    [SerializeField] private SpriteRenderer targetSpriteRenderer;

    private Sprite originalSprite;
    private bool animatorWasEnabledBeforeFlatten;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Vector2 movementDirection = Vector2.right;
    private float patrolCenterX;
    private float patrolLeftLimit;
    private float patrolRightLimit;
    private bool canMove = false;
    private AudioSource runtimeLoopSfxAudioSource;
    private AudioSource runtimeOneShotSfxAudioSource;
    private readonly List<AudioSource> registeredSoundEffectSources = new List<AudioSource>();
    private bool isWalkSfxPlaying = false;

    private void Start()
    {
        EnsureAudioSourceInitialized();
        RegisterAudioSourcesForSettings();

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }
        rb.gravityScale = 0;

        ResolveSpriteRenderer();
        if (spriteRenderer != null)
        {
            originalSprite = spriteRenderer.sprite;
        }

        if (confinementArea != null)
        {
            confinementBounds = confinementArea.bounds;
            useConfinementBounds = true;
        }
        else
        {
            useConfinementBounds = false;
        }

        InitializePatrolLimits();
        SelectNewDirection();

        StartCoroutine(BeginMovementAfterIdleDelay());
    }

    private void OnDestroy()
    {
        UpdateWalkLoopSfx(false);
        UnregisterAudioSourcesForSettings();
    }

    private void FixedUpdate()
    {
        if (currentState != VegetableState.Alive) return;

        HandleMovement();
    }

    private void HandleMovement()
    {
        if (!canMove)
        {
            rb.velocity = Vector2.zero;
            UpdateWalkLoopSfx(false);
            return;
        }

        // Patrol on horizontal axis between fixed left/right limits.
        Vector2 nextPosition = (Vector2)transform.position + (movementDirection * movementSpeed * Time.fixedDeltaTime);

        if (nextPosition.x <= patrolLeftLimit)
        {
            movementDirection = Vector2.right;
        }
        else if (nextPosition.x >= patrolRightLimit)
        {
            movementDirection = Vector2.left;
        }

        nextPosition = (Vector2)transform.position + (movementDirection * movementSpeed * Time.fixedDeltaTime);
        if (!IsWithinConfinement(nextPosition))
        {
            movementDirection = new Vector2(-movementDirection.x, 0f);
        }

        rb.velocity = movementDirection * movementSpeed;
        UpdateWalkLoopSfx(rb.velocity.sqrMagnitude > 0.0001f);
        if (animator != null)
        {
            if (HasAnimatorBoolParameter(walkAnimationParam))
            {
                animator.SetBool(walkAnimationParam, true);
            }
            else
            {
                EnsureStatePlaying(walkStateName);
            }
        }

        // Flip sprite based on direction
        if (movementDirection.x != 0 && spriteRenderer != null)
        {
            spriteRenderer.flipX = movementDirection.x < 0;
        }
    }

    private void SelectNewDirection()
    {
        movementDirection = Random.value < 0.5f ? Vector2.left : Vector2.right;
    }

    private void InitializePatrolLimits()
    {
        patrolCenterX = transform.position.x;
        patrolLeftLimit = patrolCenterX - patrolDistanceEachSide;
        patrolRightLimit = patrolCenterX + patrolDistanceEachSide;

        if (useConfinementBounds)
        {
            patrolLeftLimit = Mathf.Max(patrolLeftLimit, confinementBounds.min.x);
            patrolRightLimit = Mathf.Min(patrolRightLimit, confinementBounds.max.x);
        }

        if (patrolLeftLimit > patrolRightLimit)
        {
            float currentX = transform.position.x;
            patrolLeftLimit = currentX;
            patrolRightLimit = currentX;
        }
    }

    private bool IsWithinConfinement(Vector2 position)
    {
        if (!useConfinementBounds) return true;

        return position.x >= confinementBounds.min.x &&
               position.x <= confinementBounds.max.x &&
               position.y >= confinementBounds.min.y &&
               position.y <= confinementBounds.max.y;
    }

    /// <summary>
    /// Flatten the vegetable (becomes stationary and stuck to ground)
    /// </summary>
    public void FlattenVegetable()
    {
        MarkDeadByMonster();
    }

    /// <summary>
    /// Called when monster reaches this vegetable and kills it.
    /// </summary>
    public void MarkDeadByMonster()
    {
        if (isDead || currentState == VegetableState.Flattened) return;

        ResolveSpriteRenderer();

        isDead = true;
        currentState = VegetableState.Flattened;
        canMove = false;
        rb.velocity = Vector2.zero;
        UpdateWalkLoopSfx(false);
        PlaySfxOneShot(flattenSfxClip, flattenSfxVolume);

        bool playedDeathAnimation = false;
        if (animator != null)
        {
            animatorWasEnabledBeforeFlatten = animator.enabled;
            animator.enabled = true;

            if (HasAnimatorBoolParameter(walkAnimationParam))
            {
                animator.SetBool(walkAnimationParam, false);
            }

            playedDeathAnimation = TrySetAnimatorTrigger(deathAnimationTrigger) ||
                                   TrySetAnimatorTrigger(flattenAnimationTrigger) ||
                                   PlayStateIfExists(deathAnimationStateName);

            if (playedDeathAnimation)
            {
                animator.Update(0f);
            }
        }

        SpawnDeathVfx();

        VegetableDied?.Invoke(this);

        // Disable movement
        this.enabled = false;
    }

    /// <summary>
    /// Revive the vegetable (for level restart)
    /// </summary>
    public void ReviveVegetable()
    {
        ResolveSpriteRenderer();

        isDead = false;
        currentState = VegetableState.Alive;
        UpdateWalkLoopSfx(false);
        InitializePatrolLimits();
        SelectNewDirection();
        this.enabled = true;

        if (animator != null)
        {
            animator.enabled = animatorWasEnabledBeforeFlatten;
            if (HasAnimatorBoolParameter(walkAnimationParam))
            {
                animator.SetBool(walkAnimationParam, false);
            }
            animator.SetTrigger("Revive");
        }

        if (spriteRenderer != null && originalSprite != null)
        {
            spriteRenderer.sprite = originalSprite;
        }
    }

    public VegetableState CurrentState => currentState;
    public bool IsDead => isDead;
    public bool IsAlive => currentState == VegetableState.Alive;

    public void PlayMissionCompleteJumpAnimation()
    {
        if (currentState != VegetableState.Alive || animator == null)
        {
            return;
        }

        // Freeze patrol so FixedUpdate does not immediately force Walk again.
        canMove = false;
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
        }
        UpdateWalkLoopSfx(false);

        if (HasAnimatorBoolParameter(walkAnimationParam))
        {
            animator.SetBool(walkAnimationParam, false);
        }

        bool playedJumpAnimation = TrySetAnimatorTrigger(missionCompleteJumpTrigger) ||
                                   PlayStateIfExists(missionCompleteJumpStateName);

        if (!playedJumpAnimation)
        {
            EnsureStatePlaying(walkStateName);
            return;
        }

        animator.Update(0f);
    }

    public void SetMovementSpeed(float speed)
    {
        movementSpeed = Mathf.Max(0.1f, speed);
    }

    public void SetInitialIdleDuration(float duration)
    {
        initialIdleDuration = Mathf.Max(0f, duration);
    }

    public void SetConfinementArea(Collider2D area)
    {
        confinementArea = area;
        if (area != null)
        {
            confinementBounds = area.bounds;
            useConfinementBounds = true;
        }
        else
        {
            useConfinementBounds = false;
        }

        InitializePatrolLimits();
    }

    private void ResolveSpriteRenderer()
    {
        if (targetSpriteRenderer != null)
        {
            spriteRenderer = targetSpriteRenderer;
            return;
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }

    private IEnumerator BeginMovementAfterIdleDelay()
    {
        if (animator != null)
        {
            PlayStateIfExists(idleStateName);

            if (HasAnimatorBoolParameter(walkAnimationParam))
            {
                animator.SetBool(walkAnimationParam, false);
            }
        }

        float delay = Mathf.Max(0f, initialIdleDuration);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        canMove = true;

        if (animator != null)
        {
            if (HasAnimatorBoolParameter(walkAnimationParam))
            {
                animator.SetBool(walkAnimationParam, true);
            }

            PlayStateIfExists(walkStateName);
        }
    }

    private bool PlayStateIfExists(string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
        {
            return false;
        }

        int stateHash = Animator.StringToHash(stateName);
        for (int i = 0; i < animator.layerCount; i++)
        {
            if (animator.HasState(i, stateHash))
            {
                animator.Play(stateHash, i, 0f);
                animator.Update(0f);
                return true;
            }
        }

        return false;
    }

    private void EnsureStatePlaying(string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
        {
            return;
        }

        int stateHash = Animator.StringToHash(stateName);
        if (IsStatePlaying(stateHash))
        {
            return;
        }

        PlayStateIfExists(stateName);
    }

    private bool IsStatePlaying(int stateHash)
    {
        if (animator == null)
        {
            return false;
        }

        for (int i = 0; i < animator.layerCount; i++)
        {
            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(i);
            if (current.shortNameHash == stateHash)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasAnimatorBoolParameter(string paramName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(paramName))
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Bool &&
                string.Equals(parameters[i].name, paramName, System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private bool TrySetAnimatorTrigger(string triggerName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(triggerName))
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Trigger &&
                string.Equals(parameters[i].name, triggerName, System.StringComparison.Ordinal))
            {
                animator.ResetTrigger(triggerName);
                animator.SetTrigger(triggerName);
                return true;
            }
        }

        return false;
    }

    private void SpawnDeathVfx()
    {
        if (deathVfxPrefab == null)
        {
            return;
        }

        GameObject deathVfxInstance = Instantiate(deathVfxPrefab, transform.position, Quaternion.identity);

        ParticleSystem particleSystem = deathVfxInstance.GetComponentInChildren<ParticleSystem>();
        if (particleSystem != null)
        {
            float lifetime = particleSystem.main.duration + particleSystem.main.startLifetime.constantMax;
            Destroy(deathVfxInstance, Mathf.Max(0.1f, lifetime));
            return;
        }

        Destroy(deathVfxInstance, 2f);
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

            if (source == runtimeLoopSfxAudioSource || source == runtimeOneShotSfxAudioSource)
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
        if (runtimeLoopSfxAudioSource != null && runtimeOneShotSfxAudioSource != null)
        {
            ConfigureSfxSource(runtimeLoopSfxAudioSource, allowLoop: true);
            ConfigureSfxSource(runtimeOneShotSfxAudioSource, allowLoop: false);
            return;
        }

        runtimeLoopSfxAudioSource = GetComponent<AudioSource>();
        if (runtimeLoopSfxAudioSource == null)
        {
            runtimeLoopSfxAudioSource = gameObject.AddComponent<AudioSource>();
        }

        runtimeOneShotSfxAudioSource = gameObject.AddComponent<AudioSource>();

        ConfigureSfxSource(runtimeLoopSfxAudioSource, allowLoop: true);
        ConfigureSfxSource(runtimeOneShotSfxAudioSource, allowLoop: false);
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

        if (runtimeOneShotSfxAudioSource != null)
        {
            runtimeOneShotSfxAudioSource.PlayOneShot(clip, scaledVolume);
            return;
        }

        PlayDetachedSfx(clip, scaledVolume);
    }

    private void PlayDetachedSfx(AudioClip clip, float scaledVolume)
    {
        GameObject tempAudioObject = new GameObject("VegetableSfxDetached");
        tempAudioObject.transform.position = transform.position;

        AudioSource tempSource = tempAudioObject.AddComponent<AudioSource>();
        tempSource.playOnAwake = false;
        tempSource.spatialBlend = 0f;
        tempSource.volume = 1f;
        tempSource.PlayOneShot(clip, scaledVolume);

        Destroy(tempAudioObject, Mathf.Max(0.1f, clip.length));
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
