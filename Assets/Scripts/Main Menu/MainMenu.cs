using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    void Start()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsSortMode.None);

        foreach (Button button in buttons)
        {
            if (button.gameObject.name == "Start")
                button.onClick.AddListener(StartGame);
            else if (button.gameObject.name == "Exit" || button.gameObject.name == "Exit?")
                button.onClick.AddListener(ExitGame);
        }
    }

    public void StartGame()
    {
        SceneManager.LoadScene("Acts");
    }

    public void ExitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
