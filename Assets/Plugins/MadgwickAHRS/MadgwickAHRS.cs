using UnityEngine;

public class MadgwickAHRS
{
    private float samplePeriod;
    private float beta;
    private Quaternion quaternion;

    // Public property for accessing the quaternion
    public Quaternion Quaternion
    {
        get => quaternion;
        set => quaternion = value.normalized;
    }

    // Public property for beta with clamping
    public float Beta
    {
        get => beta;
        set => beta = Mathf.Max(value, 0f); // Ensures beta is non-negative
    }

    // Public property for samplePeriod with clamping
    public float SamplePeriod
    {
        get => samplePeriod;
        set => samplePeriod = Mathf.Max(value, 0.001f); // Ensures samplePeriod is at least 0.001f
    }

    public MadgwickAHRS(float samplePeriod, float beta)
    {
        SamplePeriod = samplePeriod; // Uses the property to apply clamping
        Beta = beta;                 // Uses the property to apply clamping
        quaternion = Quaternion.identity;
    }

    public void Reset()
    {
        quaternion = Quaternion.identity;
    }

    public void UpdateIMU(float gx, float gy, float gz, float ax, float ay, float az)
    {
        float q1 = quaternion.w, q2 = quaternion.x, q3 = quaternion.y, q4 = quaternion.z;

        float norm = Mathf.Sqrt(ax * ax + ay * ay + az * az);
        if (norm < 0.0001f) return;

        ax /= norm; ay /= norm; az /= norm;

        float f1 = 2f * (q2 * q4 - q1 * q3) - ax;
        float f2 = 2f * (q1 * q2 + q3 * q4) - ay;
        float f3 = 2f * (0.5f - q2 * q2 - q3 * q3) - az;

        float s1 = 2f * q3 * f2 - 2f * q2 * f3;
        float s2 = 2f * q4 * f3 - 2f * q1 * f2;
        float s3 = 2f * q1 * f1 - 2f * q4 * f2;
        float s4 = 2f * q2 * f1 + 2f * q3 * f2;

        norm = Mathf.Sqrt(s1 * s1 + s2 * s2 + s3 * s3 + s4 * s4);
        if (norm > 0.0001f)
        {
            s1 /= norm; s2 /= norm; s3 /= norm; s4 /= norm;
        }

        float qDot1 = 0.5f * (-q2 * gx - q3 * gy - q4 * gz) - beta * s1;
        float qDot2 = 0.5f * (q1 * gx + q3 * gz - q4 * gy) - beta * s2;
        float qDot3 = 0.5f * (q1 * gy - q2 * gz + q4 * gx) - beta * s3;
        float qDot4 = 0.5f * (q1 * gz + q2 * gy - q3 * gx) - beta * s4;

        q1 += qDot1 * samplePeriod;
        q2 += qDot2 * samplePeriod;
        q3 += qDot3 * samplePeriod;
        q4 += qDot4 * samplePeriod;

        norm = Mathf.Sqrt(q1 * q1 + q2 * q2 + q3 * q3 + q4 * q4);
        if (norm > 0.0001f)
        {
            q1 /= norm; q2 /= norm; q3 /= norm; q4 /= norm;
        }

        quaternion = new Quaternion(q2, q3, q4, q1);
    }
}