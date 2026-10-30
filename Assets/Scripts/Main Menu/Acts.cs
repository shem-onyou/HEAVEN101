using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Act selection screen. Act 1 is always open; every other act stays locked
// (non-interactable, so its Disabled Sprite shows) until the act before it is finished.
public class Acts : MonoBehaviour
{
    [Header("Buttons (auto-found by name if empty)")]
    public Button backButton;
    public Button act1Button;
    public Button act2Button;
    public Button act3Button;

    [Header("Scenes")]
    [SerializeField] private string mainMenuSceneName = "Main Menu";
    [SerializeField] private string act1SceneName = "Act One";
    [Tooltip("Leave empty until Act 2 exists; the button stays locked while empty.")]
    [SerializeField] private string act2SceneName = "";
    [Tooltip("Leave empty until Act 3 exists; the button stays locked while empty.")]
    [SerializeField] private string act3SceneName = "";

    void Start()
    {
        FindButtons();

        SetupButton(backButton, true, () => SceneManager.LoadScene(mainMenuSceneName));
        SetupButton(act1Button, true, StartAct1);
        SetupButton(act2Button, IsUnlocked(2, act2SceneName), () => SceneManager.LoadScene(act2SceneName));
        SetupButton(act3Button, IsUnlocked(3, act3SceneName), () => SceneManager.LoadScene(act3SceneName));
    }

    private void FindButtons()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include);

        foreach (Button button in buttons)
        {
            switch (button.gameObject.name)
            {
                case "Back": if (backButton == null) backButton = button; break;
                case "Act1": if (act1Button == null) act1Button = button; break;
                case "Act2": if (act2Button == null) act2Button = button; break;
                case "Act3": if (act3Button == null) act3Button = button; break;
            }
        }
    }

    // An act opens once the previous act is finished and its scene has been set.
    private static bool IsUnlocked(int actNumber, string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName) && PlayerDatabase.IsActCompleted(actNumber - 1);
    }

    private static void SetupButton(Button button, bool unlocked, UnityEngine.Events.UnityAction onClick)
    {
        if (button == null)
            return;

        button.interactable = unlocked;
        button.onClick.RemoveAllListeners();
        if (unlocked)
            button.onClick.AddListener(onClick);
    }

    // Starting Act 1 begins a new playthrough: old answers and points are cleared.
    private void StartAct1()
    {
        PlayerDatabase.StartNewGame();
        SceneManager.LoadScene(act1SceneName);
    }
}
