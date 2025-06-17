// GameManager.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    [Header("Required Components")]
    [Tooltip("Shared IMUManager in the scene")]
    [SerializeField] private IMUManager imuManager;

    [Tooltip("Reference to the ball prefab (with BallController, Rigidbody, Collider, Renderer).")]
    [SerializeField] private GameObject ballPrefab;

    [Header("UI Components")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text ballsText;
    [SerializeField] private TMP_Text highScoreText;
    [SerializeField] private Button playButton;
    [SerializeField] private GameObject connectionMessage;

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
    [Tooltip("Optional spawn origin. If null, uses world origin (0,0,0) and world forward).")]
    [SerializeField] private Transform spawnOrigin;

    [Tooltip("Random offset radius around spawnOrigin to avoid exact overlap.")]
    [SerializeField] private float spawnRadius = 0.5f;

    [Header("Game Settings")]
    [Tooltip("Total number of balls to launch in one session.")]
    [SerializeField] private int totalBalls = 20;

    [Tooltip("Initial fixed delay (in seconds) before the first ball is launched.")]
    [SerializeField] private float initialDelay = 3f;

    [Tooltip("Fixed delay (in seconds) between each ball launch.")]
    [SerializeField] private float betweenBallDelay = 2f;

    // Internal tracking
    private int totalScore = 0;
    private int highScore = 0;
    private int ballsRemaining = 0;
    private int ballsLaunched = 0;
    private List<BallController> activeBalls = new List<BallController>();
    private const string HighScoreKey = "HighScore";
    private Vector3 savedSpawnPosition;
    private Quaternion savedSpawnRotation;
    private bool gameActive = false;

    void Awake()
    {
        // Apply gravity scale globally
        Vector3 originalGravity = Physics.gravity;
        Physics.gravity = originalGravity * gravityScale;
        Debug.Log($"[GameManager] Gravity scaled to {gravityScale * 100f}%");

        // Load high score from PlayerPrefs
        highScore = PlayerPrefs.GetInt(HighScoreKey, 0);

        // Save original spawn position/rotation
        if (spawnOrigin != null)
        {
            savedSpawnPosition = spawnOrigin.position;
            savedSpawnRotation = spawnOrigin.rotation;
        }
        else
        {
            savedSpawnPosition = Vector3.zero;
            savedSpawnRotation = Quaternion.identity;
        }
    }

    void Start()
    {
        // Initialize UI
        ballsRemaining = totalBalls;
        UpdateUI();

        // Set play button initially invisible
        if (playButton != null)
        {
            playButton.gameObject.SetActive(false);
            playButton.onClick.AddListener(OnPlayButtonClicked);
        }

        if (connectionMessage != null)
            connectionMessage.SetActive(true);

        // IMU status listener
        if (imuManager != null)
        {
            imuManager.onImuStatusChanged.AddListener(HandleImuStatusChanged);
            HandleImuStatusChanged(imuManager.HasValidData());
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
        if (playButton != null)
            playButton.gameObject.SetActive(false);

        ResetSessionState();
        gameActive = true;
        StartCoroutine(BallLaunchSequence());
    }

    private void ResetSessionState()
    {
        // Reset score and ball counters for a fresh session
        totalScore = 0;
        ballsLaunched = 0;
        ballsRemaining = totalBalls;
        UpdateUI();

        // Destroy any lingering active balls
        foreach (var bc in activeBalls)
        {
            if (bc != null)
                Destroy(bc.gameObject);
        }
        activeBalls.Clear();
    }

    private IEnumerator BallLaunchSequence()
    {
        // Initial fixed delay before first ball
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        // Launch balls at fixed intervals from the previous launch
        while (ballsLaunched < totalBalls && gameActive)
        {
            SpawnAndLaunchBall();
            ballsLaunched++;
            ballsRemaining--;
            UpdateUI();

            // Wait exactly betweenBallDelay before next launch
            if (ballsLaunched < totalBalls && gameActive)
            {
                float delay = betweenBallDelay;
                if (delay < 0f) delay = 0f;
                Debug.Log($"[GameManager] Waiting {delay:F2}s before next launch (ball #{ballsLaunched + 1}).");
                yield return new WaitForSeconds(delay);
            }
        }

        Debug.Log($"[GameManager] All {totalBalls} balls launched. Waiting for remaining balls to finish.");
        // After launching all, wait for any still-active balls to finish before ending session
        yield return StartCoroutine(WaitForBallCompletion());

        Debug.Log("[GameManager] All active balls completed. Game complete.");
        CheckHighScore();

        // Show play button again after game ends, if still connected
        if (playButton != null && imuManager != null && imuManager.HasValidData())
            playButton.gameObject.SetActive(true);

        gameActive = false;
    }

    private IEnumerator WaitForBallCompletion()
    {
        // Wait until all active balls are gone
        while (activeBalls.Count > 0)
        {
            yield return null;
        }
    }

    /// <summary>
    /// Instantiate a new ball, configure it, and launch it with an angle-based direction.
    /// </summary>
    public void SpawnAndLaunchBall()
    {
        if (ballPrefab == null)
        {
            Debug.LogError("[GameManager] ballPrefab not assigned.");
            return;
        }

        // Determine spawnPos/spawnRot
        Vector3 spawnPos = savedSpawnPosition;
        Quaternion spawnRot = savedSpawnRotation;
        if (spawnRadius > 0f)
        {
            Vector2 circle = Random.insideUnitCircle * spawnRadius;
            spawnPos += new Vector3(circle.x, 0f, circle.y);
        }

        // Compute initialVelocity BEFORE instantiating, so it's in scope
        // Ensure desiredSpeed > 0
        if (desiredSpeed <= 0f)
        {
            Debug.LogWarning("[GameManager] desiredSpeed must be > 0. Setting to 1.");
            desiredSpeed = 1f;
        }

        float halfYaw = separationYawDegrees * 0.5f;
        float u = Random.value;
        float m = biasPower > 0f ? Mathf.Pow(u, 1f / (biasPower + 1f)) : u;
        float sign = (Random.value < 0.5f) ? -1f : 1f;
        float yawOffset = sign * m * halfYaw;

        Quaternion baseRot = spawnRot;
        Quaternion middleRot = Quaternion.Euler(middleEulerAngles);
        Quaternion baseWithMiddle = baseRot * middleRot;
        Quaternion yawRot = Quaternion.Euler(0f, yawOffset, 0f);
        Quaternion launchRot = baseWithMiddle * yawRot;

        Vector3 dir = launchRot * Vector3.forward;
        Vector3 initialVelocity = dir * desiredSpeed;  // computed here

        // Instantiate prefab
        GameObject newObj = Instantiate(ballPrefab, spawnPos, spawnRot);
        newObj.name = $"Ball_{ballsLaunched + 1}";

        // Enable Colliders on the instantiated ball if the prefab had them disabled
        Collider[] colliders = newObj.GetComponentsInChildren<Collider>(true);
        foreach (var col in colliders)
        {
            col.enabled = true;
            // If prefab used isTrigger = true, you could also set col.isTrigger = false;
        }

        // Ensure correct layer so physics behave as intended (if you use a “Ball” layer)
        int ballLayer = LayerMask.NameToLayer("Ball");
        if (ballLayer != -1)
        {
            SetLayerRecursively(newObj, ballLayer);
        }

        BallController bc = newObj.GetComponent<BallController>();
        if (bc == null)
        {
            Debug.LogError("[GameManager] Spawned prefab missing BallController script.");
            Destroy(newObj);
            return;
        }

        // Setup and launch with computed initialVelocity
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
        Debug.Log($"[GameManager] Spawned {newObj.name} at {spawnPos} with initialVelocity {initialVelocity}");
        bc.Launch();
    }

    private void HandleBallScored(BallController ball)
    {
        totalScore++;
        UpdateUI();
        Debug.Log($"[GameManager] Ball {ball.name} scored. Total score: {totalScore}");
        activeBalls.Remove(ball);
    }

    private void HandleBallLost(BallController ball)
    {
        Debug.Log($"[GameManager] Ball {ball.name} lost.");
        activeBalls.Remove(ball);
    }

    private void UpdateUI()
    {
        if (scoreText != null)
            scoreText.text = $"Score: {totalScore}";

        if (ballsText != null)
            ballsText.text = $"Balls: {ballsRemaining}";

        if (highScoreText != null)
            highScoreText.text = $"High Score: {highScore}";
    }

    private void CheckHighScore()
    {
        if (totalScore > highScore)
        {
            highScore = totalScore;
            PlayerPrefs.SetInt(HighScoreKey, highScore);
            PlayerPrefs.Save();
            UpdateUI();
            Debug.Log($"New high score: {highScore}");
        }
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
            ballsLaunched = 0;
            ballsRemaining = totalBalls;
            UpdateUI();
        }

        Debug.Log("[GameManager] All balls destroyed. Score reset: " + resetScore);
    }

    /// <summary>
    /// Recursively set layer on GameObject and all its children.
    /// </summary>
    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}
