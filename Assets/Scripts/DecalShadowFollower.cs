using UnityEngine;

public class BallShadowDecal : MonoBehaviour
{
    public Transform ballTransform;     // Assign the ball's transform in the Inspector
    public float verticalOffset = 0.01f; // Slight lift above the table to avoid z-fighting

    void LateUpdate()
    {
        if (!ballTransform) return;

        // Set position to directly below the ball
        Vector3 newPosition = ballTransform.position;
        newPosition.y -= ballTransform.localScale.y / 2f - verticalOffset;
        transform.position = newPosition;

        // Lock rotation in world space (e.g., face upward)
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }
}
