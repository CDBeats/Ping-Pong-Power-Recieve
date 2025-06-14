using UnityEngine;

public class FramerateLimiter : MonoBehaviour
{
    [Tooltip("Target frames per second. Set to -1 for unlimited.")]
    public int targetFPS = 60;

    void Awake()
    {
        Application.targetFrameRate = targetFPS;
        QualitySettings.vSyncCount = 0; // Disable VSync to allow manual FPS control
    }
}
