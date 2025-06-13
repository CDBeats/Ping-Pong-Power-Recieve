using UnityEngine;

public class BallPhysicsControl : MonoBehaviour
{
    private Rigidbody rb;
    private Vector3 lastLinearVelocity;
    private Vector3 lastAngularVelocity;

    private IMUManager imuManager;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.mass = 0.0027f;
        rb.linearDamping = 0.05f;
        rb.angularDamping = 0.1f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        ResetBallPhysics();

        var collider = GetComponent<SphereCollider>();
        if (collider != null && collider.material == null)
        {
            Debug.LogWarning("No PhysicMaterial assigned to ball's SphereCollider.");
        }

        imuManager = Object.FindFirstObjectByType<IMUManager>();
        if (imuManager == null)
        {
            Debug.LogWarning("IMUManager not found in scene. Paddle impulse may not apply correctly.");
        }
    }

    public void ResetBallPhysics()
    {
        rb.isKinematic = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        Debug.Log("ResetBallPhysics: Kinematic = true, velocities zeroed.");
    }

    public void EnablePhysics()
    {
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        Debug.Log("EnablePhysics: Kinematic = false, velocities zeroed.");
    }

    public void EnableGravity()
    {
        rb.useGravity = true;
        Debug.Log("EnableGravity: Gravity enabled.");
    }

    public void PausePhysics()
    {
        lastLinearVelocity = rb.linearVelocity;
        lastAngularVelocity = rb.angularVelocity;
        rb.isKinematic = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        Debug.Log("PausePhysics: Kinematic = true, velocities cached and zeroed.");
    }

    public void ResumePhysics()
    {
        rb.isKinematic = false;
        rb.linearVelocity = lastLinearVelocity;
        rb.angularVelocity = lastAngularVelocity;
        Debug.Log("ResumePhysics: Kinematic = false, velocities restored.");
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (imuManager == null)
        {
            imuManager = Object.FindFirstObjectByType<IMUManager>();
            if (imuManager == null) return;
        }

        // Identify which paddle this collision belongs to by proximity or tag:
        Transform colliderTransform = collision.collider.transform;
        IMUManager.PlayerIMUData closestPlayer = null;
        float closestDistance = 0.5f;

        foreach (var pd in imuManager.GetPlayerDataList())
        {
            if (pd.collisionTransform == null) continue;
            // You may optionally check collisionTransform directly:
            // If the collided collider GameObject is pd.collisionTransform or a child, detect by reference.
            if (collision.collider.transform == pd.collisionTransform)
            {
                closestPlayer = pd;
                break;
            }
            // Otherwise, fall back to proximity check on collisionTransform:
            float dist = Vector3.Distance(colliderTransform.position, pd.collisionTransform.position);
            if (dist < closestDistance)
            {
                closestDistance = dist;
                closestPlayer = pd;
            }
        }

        if (closestPlayer != null)
        {
            // Use the tracked collisionVelocity directly:
            Vector3 v_col = closestPlayer.collisionVelocity;

            float multiplier = 1.5f; // tweak as needed
            Vector3 impulse = v_col * multiplier;
            rb.AddForce(impulse, ForceMode.Impulse);

            Debug.Log($"Applied paddle-collision-object impulse: {impulse} from collisionVelocity {v_col}");
        }
    }
}