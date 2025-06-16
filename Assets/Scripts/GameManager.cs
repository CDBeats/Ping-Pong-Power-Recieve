// GameManager.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    [Header("Required Components")]
    [Tooltip("Shared IMUManager in the scene")]
    [SerializeField] private IMUManager imuManager;

    [Tooltip("Reference to the ball prefab (with BallController, Rigidbody, Collider, Renderer).")]
    [SerializeField] private GameObject ballPrefab;

    [Tooltip("Play Button to spawn & launch a new ball when IMU is ready.")]
    [SerializeField] private Button playButton;

    [Tooltip("Connection message GameObject (e.g., UI Panel) to show when IMU not ready.")]
    [SerializeField] private GameObject connectionMessage;

    [Tooltip("Text to show cumulative score.")]
    [SerializeField] private TMP_Text scoreText;

    [Header("Physics Settings (for each ball)")]
    [SerializeField] private float ballMass = 0.0027f;
    [SerializeField] private float linearDamping = 0.05f;
    [SerializeField] private float angularDamping = 0.1f;
    [SerializeField] private float collisionImpulseMultiplier = 1.5f;
    [SerializeField] private float closestDistanceThreshold = 0.5f;
    [SerializeField] private float collisionCooldown = 0.5f;

    [Header("Ball Behavior Settings")]
    [SerializeField] private float noCollisionTimeout = 1.0f;
    [SerializeField] private float despawnDelayAfterBackTable = 2.0f;

    [Header("Gravity Settings")]
    [SerializeField][Range(0f, 1f)] private float gravityScale = 1f;

    [Header("Launch Settings (angle-based, biased)")]
    [Tooltip("Overall desired launch speed magnitude.")]
    [SerializeField] private float desiredSpeed = 20f;

    [Tooltip("Middle orientation Euler angles (X=pitch, Y=yaw, Z=roll) in degrees.")]
    [SerializeField] private Vector3 middleEulerAngles = Vector3.zero;

    [Tooltip("Separation yaw angle in degrees between leftmost and rightmost launch directions.")]
    [SerializeField][Range(0f, 180f)] private float separationYawDegrees = 60f;

    [Tooltip("Bias power for yaw sampling: PDF ∝ |offset/halfYaw|^biasPower. Larger => more weight at extremes.")]
    [SerializeField][Min(0f)] private float biasPower = 1f;

    [Header("Spawn Settings")]
    [Tooltip("Optional spawn origin. If null, uses world origin (0,0,0) and world forward.")]
    [SerializeField] private Transform spawnOrigin;

    [Tooltip("Random offset radius around spawnOrigin to avoid exact overlap.")]
    [SerializeField] private float spawnRadius = 0.5f;

    [Header("Gizmo Settings")]
    [Tooltip("Number of sample rays to draw across the arc. 1 means only the middle direction.")]
    [SerializeField][Min(1)] private int gizmoSampleCount = 5;

    // Internal tracking
    private int totalScore = 0;
    private List<BallController> activeBalls = new List<BallController>();

    void Awake()
    {
        // Apply gravity scale globally
        Vector3 originalGravity = Physics.gravity;
        Physics.gravity = originalGravity * gravityScale;
        Debug.Log($"[GameManager] Gravity scaled to {gravityScale * 100f}%");
    }

    void Start()
    {
        // UI setup
        if (playButton != null)
        {
            playButton.onClick.AddListener(OnPlayButtonClicked);
            playButton.gameObject.SetActive(false);
        }
        if (connectionMessage != null)
            connectionMessage.SetActive(true);

        UpdateScoreDisplay();

        // IMU status listener
        if (imuManager != null)
        {
            imuManager.onImuStatusChanged.AddListener(HandleImuStatusChanged);
            bool initialReady = imuManager.HasValidData();
            HandleImuStatusChanged(initialReady);
        }
        else
        {
            // If no IMUManager, assume ready
            playButton?.gameObject.SetActive(true);
            connectionMessage?.SetActive(false);
        }
    }

    private void HandleImuStatusChanged(bool imuReady)
    {
        if (imuReady)
        {
            playButton?.gameObject.SetActive(true);
            connectionMessage?.SetActive(false);
            Debug.Log("[GameManager] IMU connected.");
        }
        else
        {
            playButton?.gameObject.SetActive(false);
            connectionMessage?.SetActive(true);
            Debug.Log("[GameManager] IMU disconnected.");
        }
    }

    private void OnPlayButtonClicked()
    {
        SpawnAndLaunchBall();
    }

    /// <summary>
    /// Instantiate a new ball, configure it, and launch it with an angle-based direction.
    /// The “middle” orientation is given by middleEulerAngles.
    /// The launch direction is rotated around Y by a biased random angle in [-halfYaw, +halfYaw],
    /// where halfYaw = separationYawDegrees/2, using biasPower to favor extremes.
    /// </summary>
    public void SpawnAndLaunchBall()
    {
        if (ballPrefab == null)
        {
            Debug.LogError("[GameManager] ballPrefab not assigned.");
            return;
        }

        // Determine spawn position & rotation
        Vector3 spawnPos = Vector3.zero;
        Quaternion spawnRot = Quaternion.identity;
        if (spawnOrigin != null)
        {
            Vector2 circle = Random.insideUnitCircle * spawnRadius;
            spawnPos = spawnOrigin.position + new Vector3(circle.x, 0f, circle.y);
            spawnRot = spawnOrigin.rotation;
        }
        else
        {
            spawnPos = Vector3.zero;
            spawnRot = Quaternion.identity;
        }

        // Ensure desiredSpeed is positive
        if (desiredSpeed <= 0f)
        {
            Debug.LogWarning("[GameManager] desiredSpeed must be > 0. Setting to 1.");
            desiredSpeed = 1f;
        }

        float halfYaw = separationYawDegrees * 0.5f;

        // Biased sampling for yaw offset:
        // We want magnitude m ∈ [0,1] with PDF ∝ m^biasPower, so extremes (m near 1) more likely.
        // Sample u ∈ [0,1]; set m = u^(1/(biasPower+1)). Then offset = m * halfYaw.
        float u = Random.value;
        float m;
        if (biasPower > 0f)
            m = Mathf.Pow(u, 1f / (biasPower + 1f));
        else
            m = u; // biasPower=0 => uniform in [0,1]
        // Random sign ±
        float sign = (Random.value < 0.5f) ? -1f : 1f;
        float yawOffset = sign * m * halfYaw;

        // Base orientation: spawnOrigin rotation or identity
        Quaternion baseRot = (spawnOrigin != null) ? spawnOrigin.rotation : Quaternion.identity;
        // Middle orientation
        Quaternion middleRot = Quaternion.Euler(middleEulerAngles);
        // Combine: first baseRot, then middleRot
        Quaternion baseWithMiddle = baseRot * middleRot;
        // Then yaw offset around Y:
        Quaternion yawRot = Quaternion.Euler(0f, yawOffset, 0f);
        Quaternion launchRot = baseWithMiddle * yawRot;

        // Direction vector
        Vector3 dir = launchRot * Vector3.forward; // unit length
        Vector3 initialVelocity = dir * desiredSpeed;

        Debug.Log($"[GameManager] Spawn direction: middleEuler={middleEulerAngles}, yawOffset={yawOffset:F2}°, dir={dir}, initialVelocity={initialVelocity}");

        // Instantiate and configure BallController
        GameObject newObj = Instantiate(ballPrefab, spawnPos, spawnRot);
        newObj.name = $"Ball_{activeBalls.Count + 1}";
        BallController bc = newObj.GetComponent<BallController>();
        if (bc == null)
        {
            Debug.LogError("[GameManager] Spawned prefab missing BallController script.");
            Destroy(newObj);
            return;
        }

        // Setup with initialVelocity
        bc.Setup(
            imuManager,
            ballMass, linearDamping, angularDamping,
            collisionImpulseMultiplier, closestDistanceThreshold, collisionCooldown,
            noCollisionTimeout, despawnDelayAfterBackTable,
            initialVelocity
        );
        bc.OnScored += HandleBallScored;
        bc.OnLost += HandleBallLost;

        activeBalls.Add(bc);
        Debug.Log($"[GameManager] Spawned {newObj.name} at {spawnPos}");

        // Launch it
        bc.Launch();
    }

    private void HandleBallScored(BallController ball)
    {
        totalScore++;
        UpdateScoreDisplay();
        Debug.Log($"[GameManager] Ball {ball.name} scored. Total score: {totalScore}");
        activeBalls.Remove(ball);
    }

    private void HandleBallLost(BallController ball)
    {
        Debug.Log($"[GameManager] Ball {ball.name} lost.");
        activeBalls.Remove(ball);
    }

    private void UpdateScoreDisplay()
    {
        if (scoreText != null)
            scoreText.text = $"Score: {totalScore}";
    }

    /// <summary>
    /// Reset everything: destroy all active balls and reset score if desired.
    /// </summary>
    public void ResetAll(bool resetScore = true)
    {
        foreach (var bc in activeBalls)
        {
            if (bc != null)
                Destroy(bc.gameObject);
        }
        activeBalls.Clear();
        if (resetScore)
        {
            totalScore = 0;
            UpdateScoreDisplay();
        }
        Debug.Log("[GameManager] All balls destroyed. Score reset: " + resetScore);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        // Visualize the arc of possible launch vectors from spawn origin, with sample count gizmoSampleCount.
        Gizmos.color = Color.yellow;
        Vector3 basePos = (spawnOrigin != null) ? spawnOrigin.position : transform.position;
        Quaternion baseRot = (spawnOrigin != null) ? spawnOrigin.rotation : Quaternion.identity;

        Quaternion middleRot = Quaternion.Euler(middleEulerAngles);
        Quaternion baseWithMiddle = baseRot * middleRot;

        float halfYaw = separationYawDegrees * 0.5f;

        if (gizmoSampleCount <= 1)
        {
            // Only middle direction
            Vector3 dirMid = (baseWithMiddle * Quaternion.Euler(0f, 0f, 0f)) * Vector3.forward;
            float drawScale = 0.1f;
            Gizmos.DrawLine(basePos, basePos + dirMid * desiredSpeed * drawScale);
            Gizmos.DrawSphere(basePos + dirMid * desiredSpeed * drawScale, 0.07f);
        }
        else
        {
            // Evenly spaced yaw offsets from -halfYaw to +halfYaw
            for (int i = 0; i < gizmoSampleCount; i++)
            {
                float t = (float)i / (gizmoSampleCount - 1); // 0 to 1
                float yawOffset = Mathf.Lerp(-halfYaw, halfYaw, t);
                Quaternion yawRot = Quaternion.Euler(0f, yawOffset, 0f);
                Vector3 dirSample = (baseWithMiddle * yawRot) * Vector3.forward;
                float drawScale = 0.1f;
                Gizmos.DrawLine(basePos, basePos + dirSample * desiredSpeed * drawScale);
                Gizmos.DrawSphere(basePos + dirSample * desiredSpeed * drawScale, 0.05f);
            }
            // Highlight extremes in red
            Gizmos.color = Color.red;
            Vector3 dirLeft = (baseWithMiddle * Quaternion.Euler(0f, -halfYaw, 0f)) * Vector3.forward;
            Vector3 dirRight = (baseWithMiddle * Quaternion.Euler(0f, halfYaw, 0f)) * Vector3.forward;
            float drawScale2 = 0.1f;
            Gizmos.DrawLine(basePos, basePos + dirLeft * desiredSpeed * drawScale2);
            Gizmos.DrawSphere(basePos + dirLeft * desiredSpeed * drawScale2, 0.07f);
            Gizmos.DrawLine(basePos, basePos + dirRight * desiredSpeed * drawScale2);
            Gizmos.DrawSphere(basePos + dirRight * desiredSpeed * drawScale2, 0.07f);
        }
    }
#endif
}
