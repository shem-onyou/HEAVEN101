using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [Header("Buttons (auto-found by name if empty)")]
    public Button startButton;
    public Button exitButton;
    public Button continueButton;

    [Header("Scenes")]
    [SerializeField] private string actsSceneName = "Acts";

    void Start()
    {
        FindButtons();

        if (startButton != null)
        {
            startButton.onClick.RemoveListener(StartGame);
            startButton.onClick.AddListener(StartGame);
        }
        else
            Debug.LogWarning("MainMenu: no button named \"Start\" was found.");

        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(ExitGame);
            exitButton.onClick.AddListener(ExitGame);
        }
        else
            Debug.LogWarning("MainMenu: no button named \"Exit\" or \"Exit?\" was found.");

        if (continueButton != null)
        {
            // Only clickable when there is saved progress to go back to.
            continueButton.interactable = PlayerDatabase.HasSavedProgress;
            continueButton.onClick.RemoveListener(ContinueGame);
            continueButton.onClick.AddListener(ContinueGame);
        }
    }

    private void FindButtons()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include);

        foreach (Button button in buttons)
        {
            string buttonName = button.gameObject.name;

            if (startButton == null && buttonName == "Start")
                startButton = button;
            else if (exitButton == null && (buttonName == "Exit" || buttonName == "Exit?"))
                exitButton = button;
            else if (continueButton == null && buttonName == "Continue")
                continueButton = button;
        }
    }

    // Goes to the Acts screen. A new game (clearing old answers) starts when Act 1 is picked there.
    public void StartGame()
    {
        SceneManager.LoadScene(actsSceneName);
    }

    public void ContinueGame()
    {
        if (PlayerDatabase.HasSavedProgress)
            SceneManager.LoadScene(PlayerDatabase.Data.currentScene);
    }

    // Closes the game. Application.Quit does nothing inside the Editor, so Play mode is stopped there instead.
    public void ExitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
