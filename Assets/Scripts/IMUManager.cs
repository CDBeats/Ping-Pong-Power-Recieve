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

    public bool useSensorFusion = true;
    [Range(0f, 1f)]
    public float complementaryAlpha = 0.5f;
    public float madgwickBeta = 0.1f;

    [Header("Axis Inversion for Testing (Accel)")]
    public bool invertAccelX = false;
    public bool invertAccelY = false;
    public bool invertAccelZ = false;

    [Header("Axis Inversion for Testing (Gyro)")]
    public bool invertGyroX = false;
    public bool invertGyroY = false;
    public bool invertGyroZ = false;

    private Quaternion currentRotation = Quaternion.identity;
    private float lastUpdateTime;
    private bool hasImuData = false;
    private MadgwickAHRS madgwick;

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

        currentRotation = Quaternion.identity;
        paddleTransform.localRotation = Quaternion.identity;
        hasImuData = false;
        lastUpdateTime = Time.time;

        madgwick = new MadgwickAHRS(1f / 50f, madgwickBeta);
        bleManager.OnImuDataReceived += HandleImuData;
    }

    void Update()
    {
        if (hasImuData)
            paddleTransform.localRotation = currentRotation;
    }

    private void HandleImuData(string playerId, Vector3 accRaw, Vector3 gyroRaw)
    {
        // No filtering by playerId since only one device is expected.

        // 1. Remap raw sensor axes → Unity axes
        Vector3 accelRemapped = new Vector3(accRaw.x, accRaw.z, accRaw.y);
        Vector3 gyroRemapped = new Vector3(gyroRaw.x, gyroRaw.z, gyroRaw.y);

        // 2. Apply inversion toggles
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

        // 3. Delta time
        float now = Time.time;
        float dt = now - lastUpdateTime;
        if (dt <= 0f) return;
        lastUpdateTime = now;

        // 4. Raw gyro integration
        Vector3 deltaEulerRaw = gyroUnity * dt * sensitivity;
        Quaternion rawRotation = currentRotation * Quaternion.Euler(deltaEulerRaw);

        // 5. Madgwick fusion
        Quaternion fusionRotation = rawRotation;
        if (useSensorFusion)
        {
            madgwick.SamplePeriod = dt;
            madgwick.Beta = madgwickBeta;
            Vector3 gyroRad = gyroUnity * Mathf.Deg2Rad * sensitivity;
            madgwick.UpdateIMU(gyroRad.x, gyroRad.y, gyroRad.z,
                               accelUnity.x, accelUnity.y, accelUnity.z);
            fusionRotation = madgwick.Quaternion;
        }

        // 6. Blend or choose
        if (!hasImuData)
        {
            currentRotation = useSensorFusion ? fusionRotation : rawRotation;
            hasImuData = true;
        }
        else
        {
            currentRotation = useSensorFusion
                ? Quaternion.Slerp(rawRotation, fusionRotation, complementaryAlpha)
                : rawRotation;
        }
    }

    void OnDestroy()
    {
        if (bleManager != null)
            bleManager.OnImuDataReceived -= HandleImuData;
    }
}
