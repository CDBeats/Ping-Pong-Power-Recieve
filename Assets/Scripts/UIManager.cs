using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject guiPanel;     // Canvas > GUI (includes Play/Pause button)

    [Header("Buttons")]
    public Button playPauseButton;

    private bool isGamePaused = false;

    void Start()
    {
        guiPanel.SetActive(true);
        playPauseButton.onClick.AddListener(OnPlayPause);
        ResumeGame(); // Start unpaused
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
        Time.timeScale = 0f;
        Debug.Log("Game paused");
    }

    void ResumeGame()
    {
        Time.timeScale = 1f;
        Debug.Log("Game resumed");
    }
}
