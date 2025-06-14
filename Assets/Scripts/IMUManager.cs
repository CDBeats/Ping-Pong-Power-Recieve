using UnityEngine;

public class IMUManager : MonoBehaviour
{
    [Header("BLE Settings")]
    [SerializeField] private BLEManager bleManager;

    [Header("Paddle Transform")]
    [SerializeField] private Transform paddleTransform;

    [Header("Gyro/Accel Settings")]
    [Range(0.5f, 2f)]
    public float sensitivity = 1.0f;

    [Tooltip("Use Madgwick sensor fusion. If false, uses raw gyro integration only.")]
    public bool useSensorFusion = true;

    [Range(0f, 1f)]
    [Tooltip("Blend between raw gyro integration (0) and fusion (1).")]
    public float complementaryAlpha = 1.0f;

    [Tooltip("Madgwick beta parameter.")]
    public float madgwickBeta = 0.1f;

    [Header("Axis Inversion for Testing (Accel)")]
    public bool invertAccelX = false;
    public bool invertAccelY = false;
    public bool invertAccelZ = false;

    [Header("Axis Inversion for Testing (Gyro)")]
    public bool invertGyroX = false;
    public bool invertGyroY = false;
    public bool invertGyroZ = false;

    // Internal state
    private Quaternion initialPaddleRotation;          // The localRotation of paddle at first IMU data
    private bool firstImuReceived = false;

    // For raw gyro integration:
    private Quaternion sensorOrientationRaw = Quaternion.identity;

    // For fusion:
    private MadgwickAHRS madgwick;
    private Quaternion initialSensorOrientationFusion = Quaternion.identity; // captured at first reading

    private float lastUpdateTime;
    private bool hasImuData = false;

    void Start()
    {
        if (bleManager == null)
        {
            Debug.LogError("BLEManager not assigned");
            enabled = false;
            return;
        }
        if (paddleTransform == null)
        {
            Debug.LogError("Paddle Transform not assigned");
            enabled = false;
            return;
        }

        // Initialize but delay capturing initialPaddleRotation until first IMU reading
        hasImuData = false;
        firstImuReceived = false;
        lastUpdateTime = Time.time;

        // Initialize Madgwick (class assumed unchanged in your project)
        madgwick = new MadgwickAHRS(1f / 50f, madgwickBeta);

        // Subscribe
        bleManager.OnImuDataReceived += HandleImuData;
    }

    void Update()
    {
        // Only apply when we have IMU data
        if (hasImuData)
        {
            // currentPaddleRotation is updated in HandleImuData, so here we simply assign it
            paddleTransform.localRotation = currentPaddleRotation;
        }
    }

    // This holds the computed final paddle rotation each frame
    private Quaternion currentPaddleRotation = Quaternion.identity;

    private void HandleImuData(string playerId, Vector3 accRaw, Vector3 gyroRaw)
    {
        // 1. On first IMU data: capture initial paddle orientation and reset sensorOrientation state
        if (!firstImuReceived)
        {
            initialPaddleRotation = paddleTransform.localRotation;
            // Reset raw integration state
            sensorOrientationRaw = Quaternion.identity;
            // Reset Madgwick state
            madgwick.Reset();
            initialSensorOrientationFusion = Quaternion.identity; // will capture below after first fusion update
            firstImuReceived = true;
            // Reset timing
            lastUpdateTime = Time.time;
        }

        // 2. Remap raw sensor axes → Unity axes
        Vector3 accelRemapped = new Vector3(accRaw.x, accRaw.z, accRaw.y);
        Vector3 gyroRemapped = new Vector3(gyroRaw.x, gyroRaw.z, gyroRaw.y);

        // 3. Apply per-axis inversion toggles
        Vector3 accelUnity = new Vector3(
            invertAccelX ? -accelRemapped.x : accelRemapped.x,
            invertAccelY ? -accelRemapped.y : accelRemapped.y,
            invertAccelZ ? -accelRemapped.z : accelRemapped.z
        );
        Vector3 gyroUnity = new Vector3(
            invertGyroX ? -gyroRemapped.x : gyroRemapped.x,
            invertGyroY ? -gyroRemapped.y : gyroRemapped.y,
            invertGyroZ ? -gyroRemapped.z : gyroRemapped.z
        );

        // 4. Compute dt
        float now = Time.time;
        float dt = now - lastUpdateTime;
        if (dt <= 0f) return;
        lastUpdateTime = now;

        // 5. Raw gyro integration: update sensorOrientationRaw
        Vector3 deltaEulerRaw = gyroUnity * dt * sensitivity;
        Quaternion deltaRotationRaw = Quaternion.Euler(deltaEulerRaw);
        sensorOrientationRaw = sensorOrientationRaw * deltaRotationRaw; // accumulate

        // 6. Madgwick fusion: update madgwick quaternion
        Quaternion fusionQuat = sensorOrientationRaw; // fallback if first
        if (useSensorFusion)
        {
            madgwick.SamplePeriod = dt;
            madgwick.Beta = madgwickBeta;
            // Convert gyro to rad/s
            Vector3 gyroRad = gyroUnity * Mathf.Deg2Rad * sensitivity;
            madgwick.UpdateIMU(gyroRad.x, gyroRad.y, gyroRad.z,
                               accelUnity.x, accelUnity.y, accelUnity.z);
            fusionQuat = madgwick.Quaternion;

            // On the very first fusion update, capture initialSensorOrientationFusion
            if (initialSensorOrientationFusion == Quaternion.identity && !hasImuData)
            {
                // If paddle was initially flat or at any pose, fusionQuat represents that orientation.
                initialSensorOrientationFusion = fusionQuat;
            }
        }

        // 7. Determine sensorOrientation to apply onto initialPaddleRotation
        Quaternion sensorOrientationToApply;
        if (!useSensorFusion)
        {
            // Raw integration case: apply sensorOrientationRaw directly
            sensorOrientationToApply = sensorOrientationRaw;
        }
        else
        {
            // Fusion case: compute delta from initialSensorOrientationFusion
            // delta = inverse(initial) * current
            // This yields a rotation representing change from initial sensor orientation to current orientation
            Quaternion invInitial = Quaternion.Inverse(initialSensorOrientationFusion);
            sensorOrientationToApply = invInitial * fusionQuat;
        }

        // 8. Compute final paddle rotation: initialPaddleRotation * sensorOrientationToApply
        Quaternion rawApplied = initialPaddleRotation * sensorOrientationToApply;

        // 9. (Optional) blend rawApplied and fusionApplied? In this design, rawApplied already uses sensorOrientationToApply chosen above.
        // If you still want complementary blending between raw gyro integration and fusion:
        if (useSensorFusion && complementaryAlpha < 1f)
        {
            // Compute rawAppliedRaw = initialPaddleRotation * sensorOrientationRaw
            Quaternion rawAppliedRaw = initialPaddleRotation * sensorOrientationRaw;
            currentPaddleRotation = Quaternion.Slerp(rawAppliedRaw, rawApplied, complementaryAlpha);
        }
        else
        {
            currentPaddleRotation = rawApplied;
        }

        // 10. Mark that we have IMU data
        if (!hasImuData)
            hasImuData = true;
    }

    void OnDestroy()
    {
        if (bleManager != null)
            bleManager.OnImuDataReceived -= HandleImuData;
    }
}
