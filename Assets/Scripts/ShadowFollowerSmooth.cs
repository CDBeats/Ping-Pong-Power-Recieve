using UnityEngine;

public class ShadowFollowerSmooth : MonoBehaviour
{
    public Transform paddle;
    public float floorY = 0f;
    public Vector3 offset = Vector3.zero;
    public float smoothSpeed = 10f;

    void Update()
    {
        Vector3 targetPos = new Vector3(paddle.position.x, floorY, paddle.position.z) + offset;
        transform.position = Vector3.Lerp(transform.position, targetPos, smoothSpeed * Time.deltaTime);
    }
}
