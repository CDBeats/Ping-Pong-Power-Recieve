/*using UnityEngine;
using UnityEngine.UI;

public class PhysicsManager : MonoBehaviour
{
    [Header("Required Components")]
    [SerializeField] private IMUManager imuManager; // Assign in Inspector
    [SerializeField] private GameObject ballGameObject; // Assign in Inspector
    [SerializeField] private Button playPauseButton; // Assign in Inspector

    [Header("Physics Settings")]
    [SerializeField] private float ballMass = 0.0027f;
    [SerializeField] private float linearDamping = 0.05f;
    [SerializeField] private float angularDamping = 0.1f;
    [SerializeField] private float collisionImpulseMultiplier = 1.5f;
    [SerializeField] private float closestDistanceThreshold = 0.5f;

    private Rigidbody rb;
    private Vector3 lastLinearVelocity;
    private Vector3 lastAngularVelocity;
    private bool isGamePaused = false;

    void Awake()
    {

        rb.useGravity = false;
        rb.isKinematic = true;
        rb.mass = ballMass;
        rb.linearDamping = linearDamping;
        rb.angularDamping = angularDamping;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    void Start()
    {
        if (playPauseButton != null)
        {
            playPauseButton.onClick.AddListener(OnPlayPause);
        }

        ResumeGame(); // Start unpaused
    }

    public void EnablePhysics()
    {
        rb.isKinematic = false;
        rb.useGravity = true;
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
        if (!rb.isKinematic)
        {
            lastLinearVelocity = rb.linearVelocity;
            lastAngularVelocity = rb.angularVelocity;
        }
        rb.isKinematic = true;
        Debug.Log("PausePhysics: Kinematic = true, velocities cached.");
    }

    public void ResumePhysics()
    {
        rb.isKinematic = false;
        if (lastLinearVelocity != Vector3.zero || lastAngularVelocity != Vector3.zero)
        {
            rb.linearVelocity = lastLinearVelocity;
            rb.angularVelocity = lastAngularVelocity;
        }
        Debug.Log("ResumePhysics: Kinematic = false, velocities restored.");
    }

    public void OnPlayPause()
    {
        isGamePaused = !isGamePaused;

        if (isGamePaused)
            PauseGame();
        else
            ResumeGame();
    }

    void PauseGame()
    {
        PausePhysics();
        Debug.Log("Game paused");
    }

    void ResumeGame()
    {
        ResumePhysics();
        Debug.Log("Game resumed");
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (imuManager == null) return;

        Transform colliderTransform = collision.collider.transform;
        IMUManager.PlayerIMUData closestPlayer = null;
        float closestDistance = closestDistanceThreshold;

        foreach (var pd in imuManager.GetPlayerDataList())
        {
            if (pd.collisionTransform == null) continue;
            if (collision.collider.transform == pd.collisionTransform)
            {
                closestPlayer = pd;
                break;
            }
            float dist = Vector3.Distance(colliderTransform.position, pd.collisionTransform.position);
            if (dist < closestDistance)
            {
                closestDistance = dist;
                closestPlayer = pd;
            }
        }

        if (closestPlayer != null)
        {
            Vector3 v_col = closestPlayer.collisionVelocity;
            Vector3 impulse = v_col * collisionImpulseMultiplier;
            rb.AddForce(impulse, ForceMode.Impulse);
            Debug.Log($"Applied paddle-collision-object impulse: {impulse} from collisionVelocity {v_col}");
        }
    }
}*/