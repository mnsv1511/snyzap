using UnityEngine;

public class Monster : MonoBehaviour
{
    [SerializeField] private float movementSpeed = 5f;
    [SerializeField] private Transform targetVegetable;
    [SerializeField] private Vector3 fixedTargetPoint;
    [SerializeField] private bool useFixedTarget = false;

    [SerializeField] private Animator animator;
    [SerializeField] private string deathAnimationTrigger = "Death";
    [SerializeField] private AudioClip hitVoiceClip;
    [SerializeField] [Range(0f, 1f)] private float hitVoiceVolume = 1f;
    [SerializeField] private bool destroyImmediatelyOnHit = true;

    private bool isAlive = true;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Vector2 movementDirection = Vector2.zero;

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

        UpdateTargetVegetable();
        if (!useFixedTarget && targetVegetable == null)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        Vector3 targetPosition = useFixedTarget ? fixedTargetPoint : targetVegetable.position;
        movementDirection = (targetPosition - transform.position).normalized;
        rb.velocity = movementDirection * movementSpeed;

        // Keep default orientation when moving right-to-left; flip only for left-to-right movement.
        if (movementDirection.x != 0f && spriteRenderer != null)
        {
            spriteRenderer.flipX = movementDirection.x > 0f;
        }
    }

    /// <summary>
    /// Find the nearest vegetable if target is not assigned and not using a fixed point
    /// </summary>
    private void UpdateTargetVegetable()
    {
        if (useFixedTarget)
        {
            return;
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

        // Destroy after death animation completes (or immediately if no animator)
        float destroyDelay = animator != null ? GetAnimationLength(deathAnimationTrigger) : 0.5f;
        Destroy(gameObject, destroyDelay);
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
        }
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
}
