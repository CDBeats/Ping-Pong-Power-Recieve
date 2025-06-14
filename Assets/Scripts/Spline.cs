using UnityEngine;

public class Spline : MonoBehaviour
{
    [SerializeField] private Transform[] controlPoints; // [Left, Center, Right]
    [SerializeField] private bool continuousMovement = true;
    [SerializeField] private float endpointMargin = 0.05f;

    public float GetTotalLength()
    {
        if (controlPoints == null || controlPoints.Length < 3)
            return 0f;

        return Vector3.Distance(controlPoints[0].position, controlPoints[1].position) +
               Vector3.Distance(controlPoints[1].position, controlPoints[2].position);
    }

    public Vector3 GetPoint(Vector2 uv)
    {
        if (controlPoints == null || controlPoints.Length < 3)
        {
            Debug.LogWarning("Spline requires at least 3 control points.");
            return transform.position;
        }

        float u = uv.x;
        Vector3 p0 = controlPoints[0].position;
        Vector3 p1 = controlPoints[1].position;
        Vector3 p2 = controlPoints[2].position;

        // Continuous movement beyond endpoints
        if (continuousMovement)
        {
            if (u < 0f)
            {
                float t = u * 2f; // extrapolate to the left
                return p0 + (p0 - p1) * t;
            }
            else if (u > 1f)
            {
                float t = (u - 1f) * 2f; // extrapolate to the right
                return p2 + (p2 - p1) * t;
            }
        }

        // Exact endpoints with margin
        if (u <= endpointMargin) return p0;
        if (u >= 1f - endpointMargin) return p2;

        // Clamp u to [0, 1]
        u = Mathf.Clamp01(u);

        // Quadratic Bézier interpolation
        float oneMinusU = 1f - u;
        return oneMinusU * oneMinusU * p0 +
               2f * oneMinusU * u * p1 +
               u * u * p2;
    }


    public Vector3 GetDirection(Vector2 uv)
    {
        float delta = 0.01f;
        Vector3 current = GetPoint(uv);
        Vector3 next = GetPoint(new Vector2(uv.x + delta, 0));
        return (next - current).normalized;
    }

    void OnDrawGizmos()
    {
        if (controlPoints == null || controlPoints.Length < 3) return;

        // Draw control points
        Gizmos.color = Color.yellow;
        foreach (var t in controlPoints)
        {
            if (t != null) Gizmos.DrawSphere(t.position, 0.1f);
        }

        // Draw spline
        Gizmos.color = Color.blue;
        Vector3 prev = GetPoint(Vector2.zero);
        for (int i = 1; i <= 20; i++)
        {
            float t = i / 20f;
            Vector3 curr = GetPoint(new Vector2(t, 0));
            Gizmos.DrawLine(prev, curr);
            prev = curr;
        }

        // Draw margins
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(GetPoint(new Vector2(endpointMargin, 0)), 0.15f);
        Gizmos.DrawWireSphere(GetPoint(new Vector2(1f - endpointMargin, 0)), 0.15f);

#if UNITY_EDITOR
        float length = GetTotalLength();
        UnityEditor.Handles.Label(
            (controlPoints[0].position + controlPoints[2].position) / 2,
            $"Length: {length:0.00}m"
        );
#endif
    }
}
