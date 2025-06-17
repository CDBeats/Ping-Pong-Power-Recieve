using UnityEngine;
using System;
using System.Collections;

public class BallController : MonoBehaviour
{
    // Reference to shared IMUManager, assigned by GameManager
    private IMUManager imuManager;

    // Physics settings
    private float ballMass;
    private float linearDamping;
    private float angularDamping;
    private float collisionImpulseMultiplier;
    private float closestDistanceThreshold;
    private float collisionCooldown;
    private float noCollisionTimeout;
    private float despawnDelayAfterBackTable;

    // Initial velocity vector
    private Vector3 initialVelocity;

    // Internal state
    private Rigidbody rb;
    private bool ballInPlay = false;
    private bool paddleHit = false;
    private float lastProcessedCollisionTime = 0f;
    private float lastCollisionTime = 0f;

    // Static flag for layer initialization
    private static bool layersInitialized = false;

    // Events to notify manager
    public delegate void BallEvent(BallController ball);
    public event BallEvent OnScored;  // invoked when this ball scores
    public event BallEvent OnLost;    // invoked when this ball is lost

    // Event to report a collision point
    public event Action<BallController, Vector3> OnCollisionPoint;

    [Header("Audio")]
    [Tooltip("AudioSource used to play collision sounds.")]
    [SerializeField] private AudioSource collisionAudioSource;
    [Tooltip("Collision sound to play on non-ground collisions.")]
    [SerializeField] private AudioClip collisionSound;
    [Tooltip("Minimum time between successive collision sounds (in seconds).")]
    [SerializeField] private float audioCooldown = 0.1f;
    [Tooltip("Minimum pitch for collision sounds.")]
    [SerializeField] private float minPitch = 0.8f;
    [Tooltip("Maximum pitch for collision sounds.")]
    [SerializeField] private float maxPitch = 1.2f;

    private float lastAudioTime = -999f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError($"[BallController:{name}] Missing Rigidbody.");
            enabled = false;
            return;
        }

        // Initialize ball physics layers only once
        if (!layersInitialized)
        {
            InitializeBallLayers();
            layersInitialized = true;
        }

        // Set ball to "Ball" layer
        int ballLayer = LayerMask.NameToLayer("Ball");
        if (ballLayer != -1)
        {
            gameObject.layer = ballLayer;
        }
        else
        {
            Debug.LogWarning($"[BallController:{name}] 'Ball' layer not found; ensure layer exists.");
        }

        // Start inactive until Setup() and Launch()
        rb.useGravity = false;
        rb.isKinematic = true;

        // Ensure collision detection mode is set properly
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    /// <summary>
    /// Initialize physics layers to prevent ball-ball collisions
    /// </summary>
    private void InitializeBallLayers()
    {
        int ballLayer = LayerMask.NameToLayer("Ball");
        if (ballLayer == -1)
        {
            Debug.LogError("[BallController] 'Ball' layer not found. Please create a 'Ball' physics layer.");
            return;
        }

        // Prevent balls from colliding with each other
        Physics.IgnoreLayerCollision(ballLayer, ballLayer, true);
        Debug.Log("Initialized ball physics layers - ball-ball collisions disabled");
    }

    /// <summary>
    /// Configure all settings
    /// </summary>
    public void Setup(
        IMUManager imuMgr,
        float mass, float linDamp, float angDamp,
        float impulseMult, float closeDistThresh, float collCooldown,
        float noCollTimeout, float despawnDelay,
        Vector3 initialVel)
    {
        imuManager = imuMgr;
        ballMass = mass;
        linearDamping = linDamp;
        angularDamping = angDamp;
        collisionImpulseMultiplier = impulseMult;
        closestDistanceThreshold = closeDistThresh;
        collisionCooldown = collCooldown;
        noCollisionTimeout = noCollTimeout;
        despawnDelayAfterBackTable = despawnDelay;
        initialVelocity = initialVel;

        // Apply Rigidbody settings
        rb.mass = ballMass;
        rb.linearDamping = linearDamping;
        rb.angularDamping = angularDamping;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Start inactive
        rb.useGravity = false;
        rb.isKinematic = true;

        ballInPlay = false;
        paddleHit = false;
        lastProcessedCollisionTime = 0f;
        lastCollisionTime = 0f;
        lastAudioTime = -999f;
    }

    void Update()
    {
        if (!ballInPlay)
            return;

        // No-collision-timeout check
        if (Time.time - lastCollisionTime > noCollisionTimeout)
        {
            Debug.Log($"[BallController:{name}] No collision within timeout ({noCollisionTimeout}s). Marking lost.");
            HandleLost();
        }
    }

    /// <summary>
    /// Launches this ball
    /// </summary>
    public void Launch()
    {
        // Reset physics state
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.WakeUp(); // Ensure rigidbody is active

        // Ensure visible
        SetVisible(true);

        // Set the velocity directly
        rb.linearVelocity = initialVelocity;

        ballInPlay = true;
        paddleHit = false;
        lastProcessedCollisionTime = 0f;
        lastCollisionTime = Time.time;
        lastAudioTime = -999f;

        Debug.Log($"[BallController:{name}] Launched with initialVelocity: {initialVelocity}");
    }

    /// <summary>
    /// Enable/disable visibility
    /// </summary>
    public void SetVisible(bool visible)
    {
        Renderer[] rends = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in rends)
            r.enabled = visible;
    }

    void OnCollisionEnter(Collision collision)
    {
        // Skip processing if colliding with another ball
        if (collision.gameObject.CompareTag("Ball"))
        {
            Debug.Log($"[BallController:{name}] Ignoring collision with other ball");
            return;
        }

        Vector3 contactPoint = (collision.contacts != null && collision.contacts.Length > 0)
            ? collision.contacts[0].point
            : Vector3.zero;

        // Enhanced logging with collision details
        Debug.Log($"[BallController:{name}] Collision with {collision.collider.name} " +
                  $"(Tag: {collision.collider.tag}, Layer: {LayerMask.LayerToName(collision.gameObject.layer)}) " +
                  $"at position {contactPoint}");

        // Notify any listeners about this collision point
        OnCollisionPoint?.Invoke(this, contactPoint);

        if (!ballInPlay)
        {
            Debug.Log($"[BallController:{name}] Collision ignored: ballInPlay=false");
            return;
        }

        // Update last collision time regardless of cooldown
        lastCollisionTime = Time.time;

        // Check collision cooldown for physics processing
        if (Time.time - lastProcessedCollisionTime < collisionCooldown)
        {
            Debug.Log($"[BallController:{name}] Ignoring collision ({collision.collider.tag}) - too soon after last");
            return;
        }
        lastProcessedCollisionTime = Time.time;

        string tag = collision.collider.tag;
        Debug.Log($"[BallController:{name}] Processing collision with {tag}");

        // --- Audio playback for collisions other than ground/floor with varying pitch ---
        bool shouldPlaySound = tag != "Floor" && tag != "Ground";
        if (shouldPlaySound && Time.time - lastAudioTime > audioCooldown)
        {
            if (collisionAudioSource != null && collisionSound != null)
            {
                // Randomize pitch
                float pitch = UnityEngine.Random.Range(minPitch, maxPitch);
                collisionAudioSource.pitch = pitch;
                collisionAudioSource.PlayOneShot(collisionSound);
                lastAudioTime = Time.time;
                Debug.Log($"[BallController:{name}] Played collision sound at pitch {pitch:F2}");
            }
            else
            {
                // Optional: warn if not set
                // Debug.LogWarning($"[BallController:{name}] AudioSource or AudioClip not assigned for collision sound.");
            }
        }

        // IMU-based impulse logic
        if (imuManager != null)
        {
            Transform colTrans = imuManager.GetCollisionTransform();
            if (colTrans != null &&
                (collision.collider.transform == colTrans ||
                 Vector3.Distance(collision.collider.transform.position, colTrans.position) < closestDistanceThreshold))
            {
                Vector3 v_col = imuManager.GetCollisionVelocity();
                Vector3 impulse = v_col * collisionImpulseMultiplier;
                Debug.Log($"[BallController:{name}] IMU-based collision matched. Applying impulse: {impulse}");
                rb.AddForce(impulse, ForceMode.Impulse);

                if (tag == "Paddle")
                {
                    paddleHit = true;
                    Debug.Log($"[BallController:{name}] Paddle hit detected - waiting for back table");
                }
            }
        }

        // Game-phase logic
        if (tag == "TableBack")
        {
            // Score if paddle has been hit at any time before this
            if (paddleHit)
            {
                Debug.Log($"[BallController:{name}] Scored point! Notifying manager and scheduling despawn.");
                HandleScored();
            }
        }
        else if (tag == "TableFront"){}
        else if ((tag == "Floor" || tag == "Ground"))
        {
            // Loss: hit floor
            Debug.Log($"[BallController:{name}] Hit floor. Marking lost.");
            HandleLost();
        }
    }

    /// <summary>
    /// Handle scoring
    /// </summary>
    private void HandleScored()
    {
        if (!ballInPlay) return;  // avoid double scoring
        ballInPlay = false;
        OnScored?.Invoke(this);

        Collider col = GetComponent<Collider>();
        if (col != null)
            col.enabled = false;

        StartCoroutine(DespawnAfterDelay());
    }

    private IEnumerator DespawnAfterDelay()
    {
        yield return new WaitForSeconds(despawnDelayAfterBackTable);
        Debug.Log($"[BallController:{name}] Despawning after score delay.");
        Destroy(gameObject);
    }

    /// <summary>
    /// Handle loss
    /// </summary>
    private void HandleLost()
    {
        if (!ballInPlay) return;
        ballInPlay = false;
        OnLost?.Invoke(this);

        Collider col = GetComponent<Collider>();
        if (col != null)
            col.enabled = false;

        Destroy(gameObject);
    }
}