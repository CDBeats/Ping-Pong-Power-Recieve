using UnityEngine;
using UnityEngine.UI;

public class PhysicsManager : MonoBehaviour
{
    [Header("Required Components")]
    [SerializeField] private IMUManager imuManager;
    [SerializeField] private GameObject ballGameObject; // initial instance in scene
    [SerializeField] private GameObject ballPrefab;     // assign the ball prefab in Inspector
    [SerializeField] private Button playPauseButton;
    [SerializeField] private Button resetButton;

    [Header("Physics Settings")]
    [SerializeField] private float ballMass = 0.0027f;
    [SerializeField] private float linearDamping = 0.05f;
    [SerializeField] private float angularDamping = 0.1f;
    [SerializeField] private float collisionImpulseMultiplier = 1.5f;
    [SerializeField] private float closestDistanceThreshold = 0.5f;

    [Header("Gravity Settings")]
    [SerializeField][Range(0f, 1f)] private float gravityScale = 1f;
    private Vector3 originalGravity;

    [Header("Launch Settings")]
    [SerializeField] private float launchSpeed = 2.0f;
    [SerializeField] private Vector3 launchDirection = new Vector3(1f, 1f, 2f);

    // Sequence tracking
    // The expected tags in order. Adjust tags/names to match your setup.
    private readonly string[] expectedCollisionSequence = new string[]
    {
        "TableBack",   // 1st collision
        "TableFront",  // 2nd
        "Paddle",      // 3rd
        "TableFront",  // 4th
        "TableBack"    // 5th
    };
    private int collisionSequenceIndex = 0;

    private Rigidbody rb;
    private Vector3 lastLinearVelocity;
    private Vector3 lastAngularVelocity;
    private Vector3 initialBallPosition;
    private Quaternion initialBallRotation;
    private bool isGamePaused = true;
    private bool firstLaunch = true;

    void Awake()
    {
        // Store original gravity and apply scaling
        originalGravity = Physics.gravity;
        Physics.gravity = originalGravity * gravityScale;

        if (ballGameObject == null || (rb = ballGameObject.GetComponent<Rigidbody>()) == null)
        {
            Debug.LogError("PhysicsManager: Missing Rigidbody or ballGameObject. Disabling.");
            enabled = false;
            return;
        }

        if (ballPrefab == null)
        {
            Debug.LogError("PhysicsManager: ballPrefab not assigned. Disabling.");
            enabled = false;
            return;
        }

        // Store initial transform for resets/spawns
        initialBallPosition = ballGameObject.transform.position;
        initialBallRotation = ballGameObject.transform.rotation;

        // Initial physics setup
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.mass = ballMass;
        rb.linearDamping = linearDamping;
        rb.angularDamping = angularDamping;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    void OnDestroy()
    {
        // Restore original gravity when destroyed
        Physics.gravity = originalGravity;
    }

    void Start()
    {
        if (playPauseButton != null)
            playPauseButton.onClick.AddListener(TogglePlayPause);
        else
            Debug.LogWarning("PhysicsManager: playPauseButton not assigned.");

        if (resetButton != null)
            resetButton.onClick.AddListener(ResetBall);
        else
            Debug.LogWarning("PhysicsManager: resetButton not assigned.");
    }

    /// <summary>
    /// Toggle between paused and running. On first run, applies launch velocity.
    /// </summary>
    public void TogglePlayPause()
    {
        isGamePaused = !isGamePaused;

        if (isGamePaused)
        {
            if (!rb.isKinematic)
            {
                lastLinearVelocity = rb.linearVelocity;
                lastAngularVelocity = rb.angularVelocity;
            }
            rb.isKinematic = true;
            Debug.Log("Game Paused.");
        }
        else
        {
            rb.isKinematic = false;
            rb.useGravity = true;

            if (firstLaunch)
            {
                Vector3 launchVelocity = launchDirection.normalized * launchSpeed;
                rb.linearVelocity = launchVelocity;
                rb.angularVelocity = Vector3.zero;
                firstLaunch = false;
                Debug.Log("First launch with velocity: " + launchVelocity);
            }
            else
            {
                rb.linearVelocity = lastLinearVelocity;
                rb.angularVelocity = lastAngularVelocity;
                Debug.Log("Game Resumed with previous velocities.");
            }
        }
    }

    /// <summary>
    /// Manual reset: resets current ball instance back to initial position, paused.
    /// Does not affect sequence index.
    /// </summary>
    public void ResetBall()
    {
        // Reset transform and physics
        ballGameObject.transform.position = initialBallPosition;
        ballGameObject.transform.rotation = initialBallRotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.isKinematic = true;
        rb.useGravity = false;
        isGamePaused = true;
        firstLaunch = true;

        Debug.Log("Ball reset to initial position");
        // Note: We do NOT reset the collision sequence here by default.
        // If you want ResetBall to also clear the sequence, uncomment:
        // collisionSequenceIndex = 0;
    }

    /// <summary>
    /// Called when the full collision sequence is detected.
    /// Destroys current ball instance and spawns a fresh one at the initial pose.
    /// Resets internal state (pause, firstLaunch, sequence index).
    /// </summary>
    private void SpawnNewBall()
    {
        Debug.Log("Collision sequence complete: spawning new ball.");

        // Destroy current ballGameObject
        Destroy(ballGameObject);

        // Instantiate new ball from prefab
        GameObject newBall = Instantiate(ballPrefab, initialBallPosition, initialBallRotation);
        ballGameObject = newBall;

        // Reassign Rigidbody
        rb = ballGameObject.GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Spawned ball prefab has no Rigidbody!");
            enabled = false;
            return;
        }

        // Apply same physics settings
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.mass = ballMass;
        rb.linearDamping = linearDamping;
        rb.angularDamping = angularDamping;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Reset state
        isGamePaused = true;
        firstLaunch = true;
        collisionSequenceIndex = 0;

        // If you have UI button text/icon, you might want to update it here to "Play"
        Debug.Log("New ball instantiated and reset. Awaiting Play.");
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (imuManager == null) return;

        // FIRST: existing impulse logic
        Transform colTrans = imuManager.GetCollisionTransform();
        if (colTrans != null)
        {
            Transform other = collision.collider.transform;
            float dist = Vector3.Distance(other.position, colTrans.position);
            if (other == colTrans || dist < closestDistanceThreshold)
            {
                Vector3 v_col = imuManager.GetCollisionVelocity();
                Vector3 impulse = v_col * collisionImpulseMultiplier;
                rb.AddForce(impulse, ForceMode.Impulse);
                Debug.Log($"Applied impulse: {impulse} from collisionVelocity {v_col}");
            }
        }

        // SECOND: sequence detection logic
        // Identify this collision by tag. Replace with your identification logic if not using tags.
        string tag = collision.collider.tag;

        // Expected tag at current index:
        string expectedTag = expectedCollisionSequence[collisionSequenceIndex];

        if (tag == expectedTag)
        {
            collisionSequenceIndex++;
            Debug.Log($"Collision sequence progress: matched '{tag}'. Index now {collisionSequenceIndex}/{expectedCollisionSequence.Length}.");

            if (collisionSequenceIndex >= expectedCollisionSequence.Length)
            {
                // Sequence complete
                SpawnNewBall();
                // After spawning, exit to avoid further processing on destroyed rb
                return;
            }
        }
        else
        {
            // Mismatch: reset sequence index.
            // Optionally: if this collision matches the first element, start at 1; else 0.
            if (tag == expectedCollisionSequence[0])
            {
                collisionSequenceIndex = 1;
                Debug.Log($"Collision sequence reset but '{tag}' matches start; index set to 1.");
            }
            else
            {
                if (collisionSequenceIndex != 0)
                {
                    Debug.Log($"Collision sequence broken at tag '{tag}'. Resetting sequence index to 0.");
                }
                collisionSequenceIndex = 0;
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (ballGameObject == null) return;

        Vector3 start = ballGameObject.transform.position;
        Vector3 velocityVector = launchDirection.normalized * launchSpeed;
        Vector3 end = start + velocityVector;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawSphere(end, 0.01f);

        // Draw initial position 
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(initialBallPosition, 0.05f);
    }

    // Optional: Public method to adjust gravity at runtime
    public void SetGravityScale(float scale)
    {
        gravityScale = Mathf.Clamp01(scale);
        Physics.gravity = originalGravity * gravityScale;
        Debug.Log($"Gravity scaled to: {gravityScale * 100f}%");
    }
}