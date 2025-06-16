using UnityEngine;
using System;
using System.Collections;

public class BallController : MonoBehaviour
{
    // Reference to shared IMUManager, assigned by GameManager
    private IMUManager imuManager;

    // Physics settings (assigned by GameManager via Setup)
    private float ballMass;
    private float linearDamping;
    private float angularDamping;
    private float collisionImpulseMultiplier;
    private float closestDistanceThreshold;
    private float collisionCooldown;
    private float noCollisionTimeout;
    private float despawnDelayAfterBackTable;

    // Initial velocity vector (assigned by GameManager via Setup)
    private Vector3 initialVelocity;

    // Internal state
    private Rigidbody rb;
    private bool ballInPlay = false;
    private bool initialPhaseComplete = false;
    private bool waitingForBackTable = false;
    private float lastProcessedCollisionTime = 0f;
    private float lastCollisionTime = 0f;

    // Events to notify manager
    public delegate void BallEvent(BallController ball);
    public event BallEvent OnScored;  // invoked when this ball scores
    public event BallEvent OnLost;    // invoked when this ball is lost

    // NEW: Event to report a collision point. 
    // The GameManager can subscribe and update spawnOrigin accordingly.
    public event Action<BallController, Vector3> OnCollisionPoint;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError($"[BallController:{name}] Missing Rigidbody.");
            enabled = false;
            return;
        }
        // Start inactive until Setup() and Launch()
        rb.useGravity = false;
        rb.isKinematic = true;
    }

    /// <summary>
    /// Called by GameManager immediately after Instantiate, to configure all settings.
    /// Uses a single initialVelocity vector instead of separate launch speed/direction.
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
        initialPhaseComplete = false;
        waitingForBackTable = false;
        lastProcessedCollisionTime = 0f;
        lastCollisionTime = 0f;
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
    /// Launches this ball: enables physics, sets velocity to initialVelocity, ensures visibility.
    /// GameManager must call Setup(...) before calling Launch().
    /// </summary>
    public void Launch()
    {
        // Reset physics state
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Ensure visible
        SetVisible(true);

        // Set the velocity directly
        rb.linearVelocity = initialVelocity;

        ballInPlay = true;
        initialPhaseComplete = false;
        waitingForBackTable = false;
        lastProcessedCollisionTime = 0f;
        lastCollisionTime = Time.time;

        Debug.Log($"[BallController:{name}] Launched with initialVelocity: {initialVelocity}");
    }

    /// <summary>
    /// Reset this ball to a given position & rotation, deactivate physics, optionally hide it.
    /// Not used if we destroy on loss/score, but available if you want pooling.
    /// </summary>
    public void ResetToPosition(Vector3 position, Quaternion rotation, bool hide = false)
    {
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        transform.position = position;
        transform.rotation = rotation;

        ballInPlay = false;
        initialPhaseComplete = false;
        waitingForBackTable = false;
        lastProcessedCollisionTime = 0f;
        lastCollisionTime = 0f;

        if (hide)
            SetVisible(false);
        else
            SetVisible(true);

        Debug.Log($"[BallController:{name}] Reset to position {position}, visible={!hide}");
    }

    /// <summary>
    /// Enable/disable all Renderer components under this ball.
    /// Ensures visibility toggling even if prefab renderer was disabled.
    /// </summary>
    public void SetVisible(bool visible)
    {
        Renderer[] rends = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in rends)
            r.enabled = visible;
    }

    void OnCollisionEnter(Collision collision)
    {
        Vector3 contactPoint = (collision.contacts != null && collision.contacts.Length > 0)
            ? collision.contacts[0].point
            : Vector3.zero;
        Debug.Log($"[BallController:{name}] Collision with {collision.collider.name} (Tag: {collision.collider.tag}) at position {contactPoint}");

        // NEW: Notify any listeners about this collision point
        OnCollisionPoint?.Invoke(this, contactPoint);

        if (!ballInPlay)
        {
            Debug.Log($"[BallController:{name}] Collision ignored: ballInPlay=false");
            return;
        }

        if (Time.time - lastProcessedCollisionTime < collisionCooldown)
        {
            Debug.Log($"[BallController:{name}] Ignoring collision ({collision.collider.tag}) - too soon after last");
            return;
        }
        lastProcessedCollisionTime = Time.time;
        lastCollisionTime = Time.time;

        string tag = collision.collider.tag;
        Debug.Log($"[BallController:{name}] Processing collision with {tag}, initialPhaseComplete={initialPhaseComplete}, waitingForBackTable={waitingForBackTable}");

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
                    waitingForBackTable = true;
                    Debug.Log($"[BallController:{name}] Paddle hit detected — waitingForBackTable set true");
                }
            }
        }

        // Game-phase logic
        if (tag == "TableBack")
        {
            if (!initialPhaseComplete)
            {
                initialPhaseComplete = true;
                Debug.Log($"[BallController:{name}] First hit on back table — initialPhaseComplete set true");
            }
            else if (waitingForBackTable)
            {
                Debug.Log($"[BallController:{name}] Scored point! Notifying manager and scheduling despawn.");
                HandleScored();
            }
        }
        else if (tag == "TableFront")
        {
            if (initialPhaseComplete && !waitingForBackTable)
            {
                Debug.Log($"[BallController:{name}] Loss: Hit front table again before paddle — marking lost.");
                HandleLost();
            }
            else if (initialPhaseComplete && waitingForBackTable)
            {
                Debug.Log($"[BallController:{name}] Loss: Hit front table after paddle — marking lost.");
                HandleLost();
            }
            else if (!initialPhaseComplete)
            {
                Debug.Log($"[BallController:{name}] First hit on front table — starting Y-tracking.");
                imuManager?.StartTrackingBallY(transform);
            }
        }
        else if ((tag == "Floor" || tag == "Ground") && initialPhaseComplete)
        {
            Debug.Log($"[BallController:{name}] Loss: Hit floor after initial phase — marking lost.");
            HandleLost();
        }
    }

    /// <summary>
    /// Handle scoring: notify manager, disable further collisions, destroy after delay.
    /// </summary>
    private void HandleScored()
    {
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
    /// Handle loss: notify manager, disable collider, destroy immediately.
    /// </summary>
    private void HandleLost()
    {
        ballInPlay = false;
        OnLost?.Invoke(this);

        Collider col = GetComponent<Collider>();
        if (col != null)
            col.enabled = false;

        Destroy(gameObject);
    }
}
