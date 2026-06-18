using UnityEngine;

public class Vegetable : MonoBehaviour
{
    public enum VegetableState { Alive, Flattened }
    private VegetableState currentState = VegetableState.Alive;

    [SerializeField] private float movementSpeed = 2f;
    [SerializeField] private float pauseTime = 1f;
    [SerializeField] private float moveDistance = 3f;

    [SerializeField] private Collider2D confinementArea;
    [SerializeField] private Bounds confinementBounds;
    [SerializeField] private bool useConfinementBounds = true;

    [SerializeField] private Animator animator;
    [SerializeField] private string walkAnimationParam = "IsWalking";
    [SerializeField] private string flattenAnimationTrigger = "Flatten";

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Vector2 movementDirection = Vector2.one.normalized;
    private float pauseCounter = 0f;
    private bool isPaused = false;
    private float moveTimer = 0f;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }
        rb.gravityScale = 0;

        spriteRenderer = GetComponent<SpriteRenderer>();

        if (confinementArea != null)
        {
            confinementBounds = confinementArea.bounds;
            useConfinementBounds = true;
        }

        SelectNewDirection();
    }

    private void FixedUpdate()
    {
        if (currentState != VegetableState.Alive) return;

        HandleMovement();
    }

    private void HandleMovement()
    {
        if (isPaused)
        {
            pauseCounter -= Time.fixedDeltaTime;
            if (pauseCounter <= 0)
            {
                isPaused = false;
                SelectNewDirection();
                moveTimer = 0f;
            }
            else
            {
                rb.velocity = Vector2.zero;
                if (animator != null)
                {
                    animator.SetBool(walkAnimationParam, false);
                }
                return;
            }
        }

        moveTimer += Time.fixedDeltaTime;
        if (moveTimer >= moveDistance / movementSpeed)
        {
            isPaused = true;
            pauseCounter = pauseTime;
            rb.velocity = Vector2.zero;
            if (animator != null)
            {
                animator.SetBool(walkAnimationParam, false);
            }
            return;
        }

        // Check boundaries before moving
        Vector2 nextPosition = (Vector2)transform.position + (movementDirection * movementSpeed * Time.fixedDeltaTime);
        
        if (IsWithinConfinement(nextPosition))
        {
            rb.velocity = movementDirection * movementSpeed;
            if (animator != null)
            {
                animator.SetBool(walkAnimationParam, true);
            }
        }
        else
        {
            // Bounce - select new direction
            isPaused = true;
            pauseCounter = pauseTime;
            rb.velocity = Vector2.zero;
            if (animator != null)
            {
                animator.SetBool(walkAnimationParam, false);
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
        // Random direction (8 directions)
        int directionChoice = Random.Range(0, 8);
        movementDirection = directionChoice switch
        {
            0 => Vector2.right,
            1 => Vector2.left,
            2 => Vector2.up,
            3 => Vector2.down,
            4 => (Vector2.right + Vector2.up).normalized,
            5 => (Vector2.left + Vector2.up).normalized,
            6 => (Vector2.right + Vector2.down).normalized,
            7 => (Vector2.left + Vector2.down).normalized,
            _ => Vector2.right
        };
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

        currentState = VegetableState.Flattened;
        rb.velocity = Vector2.zero;

        if (animator != null)
        {
            animator.SetBool(walkAnimationParam, false);
            animator.SetTrigger(flattenAnimationTrigger);
        }

        // Disable movement
        this.enabled = false;
    }

    /// <summary>
    /// Revive the vegetable (for level restart)
    /// </summary>
    public void ReviveVegetable()
    {
        currentState = VegetableState.Alive;
        isPaused = false;
        moveTimer = 0f;
        pauseCounter = 0f;
        this.enabled = true;

        if (animator != null)
        {
            animator.SetBool(walkAnimationParam, false);
            animator.SetTrigger("Revive");
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
        }
    }
}
