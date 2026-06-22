using UnityEngine;

public class Vegetable : MonoBehaviour
{
    public enum VegetableState { Alive, Flattened }
    private VegetableState currentState = VegetableState.Alive;

    [SerializeField] private float movementSpeed = 1f;
    [SerializeField] private float patrolDistanceEachSide = 1f;
    [SerializeField] private Collider2D confinementArea;
    [SerializeField] private Bounds confinementBounds;
    [SerializeField] private bool useConfinementBounds = true;

    [SerializeField] private Animator animator;
    [SerializeField] private string walkAnimationParam = "IsWalking";
    [SerializeField] private string flattenAnimationTrigger = "Flatten";
    [SerializeField] private Sprite flattenedSprite;
    [SerializeField] private SpriteRenderer targetSpriteRenderer;

    private Sprite originalSprite;
    private bool animatorWasEnabledBeforeFlatten;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Vector2 movementDirection = Vector2.right;
    private float patrolCenterX;
    private float patrolLeftLimit;
    private float patrolRightLimit;

    private void Start()
    {
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
    }

    private void FixedUpdate()
    {
        if (currentState != VegetableState.Alive) return;

        HandleMovement();
    }

    private void HandleMovement()
    {
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
        if (animator != null)
        {
            animator.SetBool(walkAnimationParam, true);
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
        if (currentState == VegetableState.Flattened) return;

        ResolveSpriteRenderer();

        currentState = VegetableState.Flattened;
        rb.velocity = Vector2.zero;

        bool hasFlattenedSprite = spriteRenderer != null && flattenedSprite != null;
        if (animator != null)
        {
            animatorWasEnabledBeforeFlatten = animator.enabled;
            animator.SetBool(walkAnimationParam, false);

            if (!hasFlattenedSprite)
            {
                animator.SetTrigger(flattenAnimationTrigger);
            }
        }

        if (hasFlattenedSprite)
        {
            spriteRenderer.sprite = flattenedSprite;
            // Animator can overwrite SpriteRenderer every frame, so disable it for guaranteed flattened image.
            if (animator != null)
            {
                animator.enabled = false;
            }
        }

        // Disable movement
        this.enabled = false;
    }

    /// <summary>
    /// Revive the vegetable (for level restart)
    /// </summary>
    public void ReviveVegetable()
    {
        ResolveSpriteRenderer();

        currentState = VegetableState.Alive;
        InitializePatrolLimits();
        SelectNewDirection();
        this.enabled = true;

        if (animator != null)
        {
            animator.enabled = animatorWasEnabledBeforeFlatten;
            animator.SetBool(walkAnimationParam, false);
            animator.SetTrigger("Revive");
        }

        if (spriteRenderer != null && originalSprite != null)
        {
            spriteRenderer.sprite = originalSprite;
        }
    }

    public VegetableState CurrentState => currentState;
    public bool IsAlive => currentState == VegetableState.Alive;

    public void SetMovementSpeed(float speed)
    {
        movementSpeed = Mathf.Max(0.1f, speed);
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
}
