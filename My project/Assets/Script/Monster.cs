using UnityEngine;
using UnityEngine.AI;

public class Monster : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float movementSpeed = 5f;
    [SerializeField] private Transform targetVegetable;
    [SerializeField] private Vector3 fixedTargetPoint;
    [SerializeField] private bool useFixedTarget = false;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string deathAnimationTrigger = "Death";

    [Header("State")]
    private bool isAlive = true;
    private Rigidbody2D rb;
    private Vector2 movementDirection = Vector2.zero;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }
        rb.gravityScale = 0;
    }

    private void FixedUpdate()
    {
        if (!isAlive) return;

        UpdateTargetVegetable();
        Vector3 targetPosition = useFixedTarget ? fixedTargetPoint : targetVegetable.position;
        
        movementDirection = (targetPosition - transform.position).normalized;
        rb.velocity = movementDirection * movementSpeed;

        // Flip sprite based on direction
        if (movementDirection.x != 0)
        {
            GetComponent<SpriteRenderer>().flipX = movementDirection.x < 0;
        }
    }

    /// <summary>
    /// Find the nearest vegetable if target is not assigned
    /// </summary>
    private void UpdateTargetVegetable()
    {
        if (targetVegetable == null || !useFixedTarget)
        {
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
    }

    /// <summary>
    /// Called when monster is hit by player's shot
    /// </summary>
    public void TakeHit()
    {
        if (!isAlive) return;

        isAlive = false;
        rb.velocity = Vector2.zero;

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
        
        AnimatorClip[] clips = animator.runtimeAnimatorController.animationClips;
        foreach (AnimationClip clip in clips)
        {
            if (clip.name == triggerName)
                return clip.length;
        }
        return 0.5f;
    }
}
