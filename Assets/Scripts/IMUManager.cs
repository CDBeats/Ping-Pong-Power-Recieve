using UnityEngine;
using UnityEngine.Events;

public class IMUManager : MonoBehaviour
{
    [Header("Transforms")]
    [SerializeField] private Transform paddleTransform;
    [SerializeField] private Transform collisionTransform;

    [Header("Rotation Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float complementaryAlpha = 0.98f;
    [Range(0f, 1f)]
    [SerializeField] private float smoothingFactor = 0.9f;

    [Header("One Euro Filter Settings")]
    [SerializeField] private bool useOneEuroSmoothing = false;
    [SerializeField] private float oneEuroMinCutoff = 1.0f;
    [SerializeField] private float oneEuroBeta = 0.0f;

    [Header("Calibration")]
    [SerializeField] private bool enableCalibration = true;
    [Range(1f, 5f)]
    [SerializeField] private float calibrationTime = 3f;

    [Header("Position Smoothing")]
    [Range(0.5f, 5f)]
    [SerializeField] private float positionSmoothing = 2.0f;

    [Header("Stability Detection")]
    [Range(0.001f, 2f)]
    [SerializeField] private float accelStabilityThreshold = 0.2f;
    [Range(0.1f, 10f)]
    [SerializeField] private float gyroStabilityThreshold = 1.5f;
    [Range(0.1f, 5f)]
    [SerializeField] private float requiredStableDuration = 1.0f;

    [Header("Drift Detection")]
    [Range(0f, 45f)]
    [SerializeField] private float driftAngleThreshold = 10f;

    [Header("Assign References")]
    [SerializeField] private Spline spline;
    [SerializeField] private BLEManager bleManager;

    [Header("Global Stability Settings")]
    [SerializeField] private float resetCooldown = 0.2f;
    [SerializeField] private float minStableTime = 0.5f;

    // Internal runtime state
    private bool hasImuData = false;
    private float lastUpdateTime = 0f;
    private float lastResetTime = 0f;
    private bool isStable = false;
    private float stableStartTime = 0f;
    private Quaternion neutralRotation = Quaternion.identity;
    private float imuDataStartTime = 0f;
    private float avgDt = 0.01f;
    private Vector3 lastAlignedAcc = Vector3.zero;
    private Vector3 gyroBias = Vector3.zero;
    private Quaternion currentRotation = Quaternion.identity;
    private Quaternion oneEuroFilteredRotation = Quaternion.identity;
    private float lastGyroRadMag = 0f;
    private float currentU = 0.5f;
    private Vector3 collisionPreviousPosition = Vector3.zero;
    private Vector3 collisionVelocity = Vector3.zero;
    private bool isCalibrating = false;
    private float calibrationStartTime = 0f;
    private float biasAlpha = 0.01f;

    // IMU status event
    public UnityEvent<bool> onImuStatusChanged = new UnityEvent<bool>();

    void Start()
    {
        if (bleManager == null || spline == null || paddleTransform == null)
        {
            Debug.LogError("IMUManager: Missing references (BLEManager, Spline, or paddleTransform). Disabling.");
            enabled = false;
            return;
        }

        InitializeState();
        bleManager.OnImuDataReceived += HandleImuData;
    }

    private void InitializeState()
    {
        hasImuData = false;
        lastUpdateTime = Time.time;
        lastResetTime = Time.time;
        stableStartTime = Time.time;
        imuDataStartTime = 0f;
        neutralRotation = Quaternion.identity;
        currentRotation = Quaternion.identity;
        oneEuroFilteredRotation = Quaternion.identity;
        lastAlignedAcc = Vector3.zero;
        gyroBias = Vector3.zero;
        currentU = 0.5f;
        isCalibrating = false;

        if (collisionTransform != null)
        {
            collisionPreviousPosition = collisionTransform.position;
            collisionVelocity = Vector3.zero;
        }
        else
        {
            Debug.LogWarning("IMUManager: collisionTransform not assigned; collision velocity disabled.");
        }
    }

    void Update()
    {
        if (!hasImuData)
        {
            ResetPaddleState();
            return;
        }

        ProcessRotation();
        HandleCalibration();
        UpdatePaddlePosition();
        UpdateCollisionVelocity();
    }

    private void ResetPaddleState()
    {
        paddleTransform.localRotation = Quaternion.identity;
        if (collisionTransform != null)
        {
            collisionVelocity = Vector3.zero;
            collisionPreviousPosition = collisionTransform.position;
        }
    }

    private void ProcessRotation()
    {
        Quaternion targetRot = neutralRotation * currentRotation;

        if (useOneEuroSmoothing)
        {
            ApplyOneEuroSmoothing(ref targetRot);
        }

        ApplyRotationSmoothing(targetRot);
    }

    private void ApplyOneEuroSmoothing(ref Quaternion targetRot)
    {
        float dt = Time.deltaTime;
        float cutoff = oneEuroMinCutoff + oneEuroBeta * lastGyroRadMag;
        float tau = 1f / (2f * Mathf.PI * cutoff);
        float alpha = Mathf.Clamp01(dt / (tau + dt));
        oneEuroFilteredRotation = Quaternion.Slerp(oneEuroFilteredRotation, targetRot, alpha);
        oneEuroFilteredRotation.Normalize();
        targetRot = oneEuroFilteredRotation;
    }

    private void ApplyRotationSmoothing(Quaternion targetRot)
    {
        float rotSmoothing = 1f - Mathf.Pow(1f - smoothingFactor, Time.deltaTime * 200f);
        paddleTransform.localRotation = Quaternion.Slerp(
            paddleTransform.localRotation,
            targetRot,
            rotSmoothing
        );
    }

    private void HandleCalibration()
    {
        if (!enableCalibration) return;

        if (isStable && Input.GetKeyDown(KeyCode.C))
        {
            isCalibrating = true;
            calibrationStartTime = Time.time;
        }

        if (isCalibrating && Time.time - calibrationStartTime >= calibrationTime)
        {
            neutralRotation = Quaternion.Inverse(currentRotation);
            isCalibrating = false;
            Debug.Log("IMUManager: Calibration set.");
        }
    }

    private void UpdatePaddlePosition()
    {
        float sinceStart = Time.time - imuDataStartTime;
        float posLerp = Mathf.Clamp01(sinceStart / 0.5f);
        Vector3 splinePos = spline.GetPoint(new Vector2(currentU, 0));
        Vector3 curPos = paddleTransform.position;

        float lerpFactor = Time.deltaTime * positionSmoothing * posLerp;

        // Simply move to spline position (both x and y)
        paddleTransform.position = Vector3.Lerp(
            curPos,
            splinePos,
            lerpFactor
        );
    }

    private void UpdateCollisionVelocity()
    {
        if (collisionTransform == null) return;

        Vector3 currColPos = collisionTransform.position;
        float dtCol = Time.deltaTime;
        collisionVelocity = dtCol > 0f
            ? (currColPos - collisionPreviousPosition) / dtCol
            : Vector3.zero;
        collisionPreviousPosition = currColPos;
    }

    private void AlignImuData(Vector3 rawAcc, Vector3 rawGyro, out Vector3 alignedAcc, out Vector3 alignedGyro)
    {
        // Unified alignment considering imu mounting
        alignedGyro = new Vector3(-rawGyro.x, -rawGyro.z, rawGyro.y);
        alignedAcc = new Vector3(-rawAcc.x, -rawAcc.z, rawAcc.y);
    }

    private float SmoothDt(float newDt, ref float avg)
    {
        avg = Mathf.Lerp(avg, newDt, 0.1f);
        return Mathf.Clamp(avg, 0.001f, 0.1f);
    }

    private void ResetOrientation()
    {
        Vector3 bodyGravity = lastAlignedAcc.normalized;
        Quaternion gravityAlignment = Quaternion.FromToRotation(bodyGravity, Vector3.down);
        neutralRotation = Quaternion.Inverse(gravityAlignment);
        currentRotation = gravityAlignment;
        currentU = 0.5f;
        lastResetTime = Time.time;
        oneEuroFilteredRotation = gravityAlignment;
        Debug.Log("IMUManager: Orientation reset after stability.");
    }

    private void UpdatePositionFromOrientation(Quaternion orientation)
    {
        Vector3 forehandEuler = new Vector3(-50f, 50f, 50f);
        Vector3 backhandEuler = new Vector3(-50f, -50f, -50f);
        Quaternion foreQ = Quaternion.Euler(forehandEuler);
        Quaternion backQ = Quaternion.Euler(backhandEuler);

        Vector3 foreDir = foreQ * Vector3.forward;
        Vector3 backDir = backQ * Vector3.forward;
        Vector3 currentDir = orientation * Vector3.forward;

        Vector3 segment = foreDir - backDir;
        Vector3 projected = Vector3.Project(currentDir - backDir, segment);
        currentU = Mathf.Clamp01(Vector3.Dot(projected, segment.normalized) / segment.magnitude);
    }

    private void HandleImuData(string incomingId, Vector3 accRaw, Vector3 gyroRaw)
    {
        AlignImuData(accRaw, gyroRaw, out Vector3 accAligned, out Vector3 gyroAligned);

        float now = Time.time;
        if (!hasImuData)
        {
            InitializeFirstIMUData(now, accAligned);
            return;
        }

        ProcessIMUUpdate(now, accAligned, gyroAligned);
    }

    private void InitializeFirstIMUData(float timestamp, Vector3 accAligned)
    {
        hasImuData = true;
        onImuStatusChanged?.Invoke(true); // Notify that IMU is ready
        imuDataStartTime = timestamp;
        lastAlignedAcc = accAligned;
        lastResetTime = timestamp;

        Vector3 bodyGravity = accAligned.normalized;
        Quaternion gravityAlignment = Quaternion.FromToRotation(bodyGravity, Vector3.down);
        neutralRotation = Quaternion.Inverse(gravityAlignment);
        currentRotation = gravityAlignment;
        oneEuroFilteredRotation = gravityAlignment;

        lastUpdateTime = timestamp;
    }

    private void ProcessIMUUpdate(float now, Vector3 accAligned, Vector3 gyroAligned)
    {
        float dt = now - lastUpdateTime;
        if (dt <= 0f) return;
        dt = SmoothDt(dt, ref avgDt);
        lastUpdateTime = now;

        // Stability detection
        float accelDelta = (accAligned - lastAlignedAcc).magnitude;
        float gyroMag = gyroAligned.magnitude;
        lastAlignedAcc = accAligned;

        bool currentlyStable = accelDelta < accelStabilityThreshold &&
                              gyroMag < gyroStabilityThreshold;

        UpdateStabilityState(now, currentlyStable, gyroAligned);
        CheckAutoReset(now);

        if (!ShouldSkipFilterUpdate(now))
        {
            UpdateRotation(gyroAligned, accAligned, dt);
        }

        // Drift detection is still calculated but doesn't log warnings
        CheckDrift(accAligned);
        UpdatePositionFromOrientation(currentRotation);
    }

    private void UpdateStabilityState(float timestamp, bool currentlyStable, Vector3 gyroAligned)
    {
        if (currentlyStable)
        {
            if (!isStable) stableStartTime = timestamp;
            isStable = true;
            gyroBias = Vector3.Lerp(gyroBias, gyroAligned, biasAlpha);
        }
        else
        {
            stableStartTime = timestamp;
            isStable = false;
        }
    }

    private void CheckAutoReset(float timestamp)
    {
        float stableDuration = timestamp - stableStartTime;
        float timeSinceReset = timestamp - lastResetTime;
        bool warmingUp = (timestamp - imuDataStartTime) < minStableTime;

        if (!warmingUp && isStable &&
            stableDuration >= requiredStableDuration &&
            timeSinceReset >= resetCooldown)
        {
            ResetOrientation();
        }
    }

    private bool ShouldSkipFilterUpdate(float timestamp)
    {
        bool warmingUp = (timestamp - imuDataStartTime) < minStableTime;
        return warmingUp || (isStable && (timestamp - stableStartTime) >= requiredStableDuration);
    }

    private void UpdateRotation(Vector3 gyroAligned, Vector3 accAligned, float dt)
    {
        // Convert to radians and apply bias correction
        Vector3 gyroRad = (gyroAligned - (isStable ? gyroBias : Vector3.zero)) * Mathf.Deg2Rad;
        lastGyroRadMag = gyroRad.magnitude;

        // Create rotation from gyro
        Quaternion deltaRotation = Quaternion.Euler(
            gyroRad.x * Mathf.Rad2Deg * dt,
            gyroRad.y * Mathf.Rad2Deg * dt,
            gyroRad.z * Mathf.Rad2Deg * dt
        );

        // Apply rotation
        currentRotation *= deltaRotation;

        // Apply accelerometer correction to pitch/roll only
        ApplyAccelerometerCorrection(accAligned);
    }

    private void ApplyAccelerometerCorrection(Vector3 accAligned)
    {
        if (accAligned.sqrMagnitude < 0.01f) return;

        Vector3 measuredGravity = accAligned.normalized;
        Vector3 currentGravity = currentRotation * Vector3.up;

        // Isolate horizontal components
        Vector3 measuredHorizontal = new Vector3(measuredGravity.x, 0, measuredGravity.z).normalized;
        Vector3 currentHorizontal = new Vector3(currentGravity.x, 0, currentGravity.z).normalized;

        if (measuredHorizontal.sqrMagnitude > 0.01f &&
            currentHorizontal.sqrMagnitude > 0.01f)
        {
            float angle = Vector3.SignedAngle(currentHorizontal, measuredHorizontal, Vector3.up);
            Quaternion correction = Quaternion.AngleAxis(angle * (1f - complementaryAlpha), Vector3.up);
            currentRotation = correction * currentRotation;
            currentRotation.Normalize();
        }
    }

    private void CheckDrift(Vector3 accAligned)
    {
        // Drift detection is still calculated but doesn't log warnings
        if (!isStable) return;

        Vector3 accNorm = accAligned.normalized;
        Vector3 expectedBodyGravity = currentRotation * Vector3.up;

        // Calculation still happens but results aren't used
        float dot = Mathf.Clamp(Vector3.Dot(accNorm, expectedBodyGravity), -1f, 1f);
        float angleDeg = Mathf.Acos(dot) * Mathf.Rad2Deg;
        float accMag = accAligned.magnitude;
    }

    public Transform GetCollisionTransform() => collisionTransform;
    public Vector3 GetCollisionVelocity() => collisionVelocity;
    public bool HasValidData() => hasImuData;

    void OnDestroy()
    {
        onImuStatusChanged?.RemoveAllListeners();
        if (bleManager != null)
            bleManager.OnImuDataReceived -= HandleImuData;
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying || !hasImuData) return;

        // Measured gravity (red)
        Vector3 measuredBodyGravity = lastAlignedAcc.normalized;
        Vector3 worldGravityMeas = paddleTransform.TransformDirection(measuredBodyGravity);
        Gizmos.color = Color.red;
        Gizmos.DrawRay(paddleTransform.position, worldGravityMeas * 0.5f);

        // Expected gravity (green)
        Vector3 expectedBodyGravity = currentRotation * Vector3.up;
        Vector3 worldGravityExpected = paddleTransform.TransformDirection(expectedBodyGravity);
        Gizmos.color = Color.green;
        Gizmos.DrawRay(paddleTransform.position, worldGravityExpected * 0.5f);
    }
}