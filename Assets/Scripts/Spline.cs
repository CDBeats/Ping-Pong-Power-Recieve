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
        Vector3[] points = new Vector3[controlPoints.Length];
        for (int i = 0; i < controlPoints.Length; i++)
        {
            points[i] = controlPoints[i].position;
        }

        // Continuous movement beyond endpoints
        if (continuousMovement)
        {
            if (u < 0f)
            {
                float t = u * 2f; // extrapolate to the left
                return points[0] + (points[0] - points[1]) * t;
            }
            else if (u > 1f)
            {
                float t = (u - 1f) * 2f; // extrapolate to the right
                return points[2] + (points[2] - points[1]) * t;
            }
        }

        // Exact endpoints with margin
        if (u <= endpointMargin) return points[0];
        if (u >= 1f - endpointMargin) return points[2];

        // Clamp u to [0, 1] for interpolation only
        u = Mathf.Clamp01(u);

        // Interpolate
        if (u < 0.5f)
        {
            float t = u / 0.5f;
            return Vector3.Lerp(points[0], points[1], t);
        }
        else
        {
            float t = (u - 0.5f) / 0.5f;
            return Vector3.Lerp(points[1], points[2], t);
        }
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
