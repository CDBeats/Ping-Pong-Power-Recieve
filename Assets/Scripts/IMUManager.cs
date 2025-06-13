using System.Collections.Generic;
using UnityEngine;

public class IMUManager : MonoBehaviour
{
    [System.Serializable]
    public class PlayerIMUData
    {
        public string playerId;
        public Transform paddleTransform;

        // NEW: assign this in Inspector to the child GameObject that has the collider
        public Transform collisionTransform;

        [Range(0.5f, 2f)] public float sensitivity = 1.0f;
        [Range(0.0001f, 0.05f)] public float madgwickBeta = 0.001f; // Reduced for less accelerometer influence
        [Range(0f, 1f)] public float complementaryAlpha = 0.3f;
        [Range(0f, 1f)] public float smoothingFactor = 0.9f;
        public bool enableCalibration = true;
        [Range(1f, 5f)] public float calibrationTime = 3f;
        [Range(0.05f, 0.2f)] public float rotationDeadZone = 0.1f;
        [Range(0.5f, 5f)] public float positionSmoothing = 2.0f;
        [Range(0.001f, 2f)] public float accelStabilityThreshold = 0.2f;
        [Range(0.1f, 10f)] public float gyroStabilityThreshold = 1.5f;
        [Range(0.1f, 5f)] public float requiredStableDuration = 1.0f;

        [HideInInspector] public float lastUpdateTime;
        [HideInInspector] public float lastMotionTime;
        [HideInInspector] public float lastResetTime;
        [HideInInspector] public bool isStable;
        [HideInInspector] public float stableStartTime;
        [HideInInspector] public MadgwickAHRS madgwick;
        [HideInInspector] public Quaternion driftCompensation;
        [HideInInspector] public Quaternion gyroQuaternion;
        [HideInInspector] public Quaternion neutralRotation = Quaternion.identity;
        [HideInInspector] public Quaternion targetRotation = Quaternion.identity;
        [HideInInspector] public float avgDt = 0.01f;
        [HideInInspector] public Vector3 lastRawAcc;
        [HideInInspector] public bool hasImuData;
        [HideInInspector] public bool isCalibrating;
        [HideInInspector] public float calibrationStartTime;
        [HideInInspector] public float imuDataStartTime;
        [HideInInspector] public float currentU;

        // Velocity tracking fields for the collision object:
        [HideInInspector] public Vector3 collisionPreviousPosition;
        [HideInInspector] public Vector3 collisionVelocity;
    }

    [SerializeField] private BLEManager bleManager;
    [SerializeField] private Spline spline;
    [SerializeField] private List<PlayerIMUData> players;

    [Header("Stability")]
    [SerializeField] private float resetCooldown = 0.2f;
    [SerializeField] private float minStableTime = 0.5f;

    private Dictionary<string, PlayerIMUData> playerLookup = new Dictionary<string, PlayerIMUData>();

    // Expose the list so BallPhysicsControl (or others) can call:
    public List<PlayerIMUData> GetPlayerDataList() => players;

    void Start()
    {
        if (bleManager == null || spline == null)
        {
            Debug.LogError("BLEManager or Spline not assigned");
            enabled = false;
            return;
        }

        foreach (var p in players)
        {
            if (p.paddleTransform == null) continue;

            p.lastUpdateTime = Time.time;
            p.lastMotionTime = Time.time;
            p.lastResetTime = Time.time;
            p.isStable = true;
            p.hasImuData = false;
            p.imuDataStartTime = 0f;
            p.stableStartTime = Time.time;
            p.madgwick = new MadgwickAHRS(1f / 100f, p.madgwickBeta);
            p.gyroQuaternion = Quaternion.identity;
            p.driftCompensation = Quaternion.identity;
            p.targetRotation = Quaternion.identity;
            p.neutralRotation = Quaternion.identity;
            p.paddleTransform.localRotation = Quaternion.identity;
            p.currentU = 0.5f;
            p.lastRawAcc = Vector3.zero;
            p.isCalibrating = false;
            p.calibrationStartTime = 0f;

            // Initialize velocity tracking
            if (p.collisionTransform != null)
            {
                p.collisionPreviousPosition = p.collisionTransform.position;
                p.collisionVelocity = Vector3.zero;
            }
            else
            {
                Debug.LogWarning($"Player {p.playerId}: collisionTransform not assigned; collision velocity tracking disabled.");
                p.collisionVelocity = Vector3.zero;
                p.collisionPreviousPosition = Vector3.zero;
            }

            playerLookup[p.playerId] = p;
        }

        bleManager.OnImuDataReceived += HandleImuData;
    }

    void Update()
    {
        foreach (var p in players)
        {
            if (p.paddleTransform == null) continue;

            if (!p.hasImuData)
            {
                p.paddleTransform.localRotation = Quaternion.identity;
                if (p.collisionTransform != null)
                {
                    p.collisionVelocity = Vector3.zero;
                    p.collisionPreviousPosition = p.collisionTransform.position;
                }
                continue;
            }

            float rotSmoothing = 1f - Mathf.Pow(1f - p.smoothingFactor, Time.deltaTime * 200f);
            Quaternion before = p.paddleTransform.localRotation;
            p.paddleTransform.localRotation = Quaternion.Slerp(before, p.targetRotation, rotSmoothing);

            if (p.enableCalibration && p.isStable && Input.GetKeyDown(KeyCode.C))
            {
                p.isCalibrating = true;
                p.calibrationStartTime = Time.time;
            }
            if (p.isCalibrating && Time.time - p.calibrationStartTime >= p.calibrationTime)
            {
                p.neutralRotation = Quaternion.Inverse(p.targetRotation);
                p.isCalibrating = false;
                Debug.Log($"Player {p.playerId}: Calibrated neutralRotation to inverse of {p.targetRotation.eulerAngles}");
            }

            float sinceStart = Time.time - p.imuDataStartTime;
            float posLerp = Mathf.Clamp01(sinceStart / 0.5f);
            Vector3 splinePos = spline.GetPoint(new Vector2(p.currentU, 0));
            Vector3 currentPos = p.paddleTransform.position;
            Vector3 targetPos = new Vector3(splinePos.x, currentPos.y, currentPos.z);
            p.paddleTransform.position = Vector3.Lerp(currentPos, targetPos, Time.deltaTime * p.positionSmoothing * posLerp);

            if (p.collisionTransform != null)
            {
                Vector3 currColPos = p.collisionTransform.position;
                float dt = Time.deltaTime;
                if (dt > 0f)
                    p.collisionVelocity = (currColPos - p.collisionPreviousPosition) / dt;
                else
                    p.collisionVelocity = Vector3.zero;
                p.collisionPreviousPosition = currColPos;
            }
        }
    }

    private float SmoothDt(float newDt, ref float avgDt)
    {
        avgDt = Mathf.Lerp(avgDt, newDt, 0.1f);
        return Mathf.Clamp(avgDt, 0.001f, 0.1f);
    }

    private Quaternion ConvertIMUToUnity(Quaternion q)
    {
        // Map IMU (X right, Y up, Z back) to Unity (X right, Y up, Z forward)
        Quaternion remap = Quaternion.Euler(0, 180, 0);
        return remap * new Quaternion(q.x, q.y, q.z, q.w);
    }

    private void ResetRotation(PlayerIMUData p)
    {
        p.gyroQuaternion = Quaternion.identity;
        p.driftCompensation = Quaternion.identity;
        p.targetRotation = Quaternion.identity;
        p.neutralRotation = Quaternion.identity;
        p.currentU = 0.5f;
        p.lastResetTime = Time.time;
        Debug.Log($"Player {p.playerId}: Rotation reset");
    }

    private void UpdateOrientation(PlayerIMUData p, Vector3 acc, Vector3 gyro, float dt)
    {
        // Gyro axis transformations for gyro-only integration
        Vector3 dE = new Vector3(
            -gyro.x * dt * p.sensitivity,  // Pitch
            -gyro.z * dt * p.sensitivity,  // Yaw
            gyro.y * dt * p.sensitivity    // Roll
        );
        p.gyroQuaternion *= Quaternion.Euler(dE);

        // Apply consistent axis transformations to Madgwick inputs
        p.madgwick.SamplePeriod = dt;
        p.madgwick.UpdateIMU(
            -gyro.x * Mathf.Deg2Rad * p.sensitivity,  // gx
            gyro.y * Mathf.Deg2Rad * p.sensitivity,   // gy
            -gyro.z * Mathf.Deg2Rad * p.sensitivity,  // gz
            -acc.x,                                   // ax
            acc.y,                                    // ay
            -acc.z                                    // az
        );

        Quaternion madg = ConvertIMUToUnity(p.madgwick.Quaternion);
        madg = p.driftCompensation * madg;

        if (p.isStable)
        {
            var invG = Quaternion.Inverse(p.gyroQuaternion);
            p.driftCompensation = Quaternion.Slerp(
                p.driftCompensation,
                madg * invG,
                0.02f
            );
        }

        p.targetRotation = Quaternion.Slerp(
            p.gyroQuaternion,
            madg,
            p.complementaryAlpha
        );
    }

    private void UpdatePosition(PlayerIMUData p)
    {
        Quaternion current = p.enableCalibration
            ? p.neutralRotation * p.targetRotation
            : p.targetRotation;

        Vector3 forehandEuler = new Vector3(-50f, 50f, 50f);
        Vector3 backhandEuler = new Vector3(-50f, -50f, -50f);

        Quaternion foreQ = Quaternion.Euler(forehandEuler);
        Quaternion backQ = Quaternion.Euler(backhandEuler);

        Vector3 foreDir = foreQ * Vector3.forward;
        Vector3 backDir = backQ * Vector3.forward;
        Vector3 currentDir = current * Vector3.forward;

        Vector3 segment = foreDir - backDir;
        Vector3 projected = Vector3.Project(currentDir - backDir, segment);
        float u = Mathf.Clamp01(Vector3.Dot(projected, segment.normalized) / segment.magnitude);

        if (u > 0.5f - p.rotationDeadZone && u < 0.5f + p.rotationDeadZone)
        {
            u = 0.5f;
        }

        p.currentU = u;
    }

    private void HandleImuData(string playerId, Vector3 accRaw, Vector3 gyroRaw)
    {
        if (!playerLookup.TryGetValue(playerId, out var p)) return;

        float now = Time.time;
        if (!p.hasImuData)
        {
            p.hasImuData = true;
            p.imuDataStartTime = now;
            p.lastRawAcc = accRaw;
            p.lastResetTime = now;
            return;
        }

        float dt = now - p.lastUpdateTime;
        if (dt <= 0f) return;
        dt = SmoothDt(dt, ref p.avgDt);
        p.lastUpdateTime = now;

        float accelDelta = (accRaw - p.lastRawAcc).magnitude;
        float gyroMagnitude = gyroRaw.magnitude;
        p.lastRawAcc = accRaw;

        bool lowAccel = accelDelta < p.accelStabilityThreshold;
        bool lowGyro = gyroMagnitude < p.gyroStabilityThreshold;
        bool currentlyStable = lowAccel && lowGyro;

        if (currentlyStable)
        {
            if (!p.isStable)
                p.stableStartTime = now;
            p.isStable = true;
        }
        else
        {
            p.stableStartTime = now;
            p.isStable = false;
        }

        float stableDuration = now - p.stableStartTime;
        float timeSinceReset = now - p.lastResetTime;
        bool warmingUp = (now - p.imuDataStartTime) < minStableTime;

        if (!warmingUp && p.isStable && stableDuration >= p.requiredStableDuration && timeSinceReset >= resetCooldown)
        {
            ResetRotation(p);
        }
        else if (!warmingUp)
        {
            UpdateOrientation(p, accRaw, gyroRaw, dt);
            UpdatePosition(p);
        }

        // Add accelerometer magnitude check when stable
        if (p.isStable)
        {
            float accMagnitude = accRaw.magnitude;
            if (Mathf.Abs(accMagnitude - 1f) > 0.1f)
            {
                Debug.LogWarning($"Player {playerId}: Stable accelerometer magnitude is {accMagnitude}, expected ~1. Possible calibration issue.");
            }
        }
    }

    public PlayerIMUData GetPlayerData(string playerId)
    {
        playerLookup.TryGetValue(playerId, out var pd);
        return pd;
    }

    void OnDestroy()
    {
        if (bleManager != null)
            bleManager.OnImuDataReceived -= HandleImuData;
    }
}