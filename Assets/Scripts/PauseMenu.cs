using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// The pause button and the "Paused" panel (Continue / Menu).
//
// Put this script on the object that holds your pause button and your pause panel, make that object a
// prefab named "PauseMenu" inside Assets/Resources, and it appears by itself in every game scene
// (not in the Main Menu or the Acts screen). A scene that already has a PauseMenu keeps its own.
//
// While paused: the whole game stops (dialogue, fades, timers, sounds) and nothing behind the panel
// can be clicked. Continue resumes; Menu goes back to the Main Menu (progress is already saved).
// On phones, the Back button opens and closes the pause menu too.
public class PauseMenu : MonoBehaviour
{
    private const string PrefabName = "PauseMenu";
    private static readonly string[] ScenesWithoutPause = { "Main Menu", "Acts" };

    [Header("Parts (auto-found if empty)")]
    [Tooltip("The small button that opens the pause menu. Found by \"pause\" in its name.")]
    public Button pauseButton;
    [Tooltip("The panel with the \"Paused\" title and the buttons. Found as the panel holding the Continue button.")]
    public GameObject pausePanel;
    [Tooltip("Found by \"continue\" or \"resume\" in its name or label.")]
    public Button continueButton;
    [Tooltip("Found by \"menu\" in its name or label.")]
    public Button menuButton;

    [Header("Settings")]
    [SerializeField] private string mainMenuSceneName = "Main Menu";
    [Tooltip("A dark see-through layer behind the panel, which also stops clicks on the game behind it.")]
    [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private float popDuration = 0.15f;
    [Tooltip("Color of the \"Paused\" title, so it stands out over the dimmed game.")]
    [SerializeField] private Color titleColor = Color.white;
    [Tooltip("On: no panel picture behind the buttons, only the dimmed game. Off: show the panel's sprite.")]
    [SerializeField] private bool hidePanelPicture = true;

    public static bool IsPaused { get; private set; }

    private GameObject dimLayer;
    private int lastActionFrame = -1;
    private Vector3 panelScale = Vector3.one;
    private Coroutine popCoroutine;

    // ------------------------------------------------------------------ automatic setup in every scene

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // A new scene never starts paused.
        Resume();

        if (System.Array.IndexOf(ScenesWithoutPause, scene.name) >= 0)
            return;

        // The scene already has its own pause menu.
        if (FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include) != null)
            return;

        GameObject prefab = Resources.Load<GameObject>(PrefabName);
        if (prefab == null)
            return;

        // Placed in the scene's own Canvas, the same kind of Canvas it was designed in (Act One),
        // so its sprite panel shows up. Start() then puts it in front of everything.
        Transform parent = FindSceneCanvas(scene);
        if (parent == null)
        {
            // No canvas in this scene: make one.
            GameObject canvasObject = new GameObject("PauseMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            ScreenFitter.ConfigureScaler(canvasObject.GetComponent<CanvasScaler>());
            parent = canvasObject.transform;
        }

        GameObject pauseMenu = Instantiate(prefab, parent, false);
        pauseMenu.name = PrefabName;
        pauseMenu.transform.SetAsLastSibling();
        RectTransform rect = pauseMenu.transform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    // The scene's main Canvas (the one named "Canvas", or else the first screen canvas).
    private static Transform FindSceneCanvas(Scene scene)
    {
        Canvas fallback = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace)
                    continue;

                if (canvas.gameObject.name == "Canvas")
                    return canvas.transform;
                if (fallback == null)
                    fallback = canvas;
            }
        }

        return fallback != null ? fallback.transform : null;
    }

    // ------------------------------------------------------------------ this pause menu

    // Drawing order (bigger = in front): the game's UI (0) < the dark layer < the panel sprite < the buttons.
    private const int DimOrder = 497;
    private const int PanelSpriteOrder = 498;
    private const int ButtonsOrder = 500;

    void Start()
    {
        FindParts();
        BringToFront();

        if (pausePanel != null)
        {
            panelScale = pausePanel.transform.localScale;
            if (hidePanelPicture)
                HidePanelPicture();
            ColorTitle();
            CreateDimLayer();
            pausePanel.SetActive(false);
        }
        else
            Debug.LogWarning("PauseMenu: no pause panel was found. Drag it into the Pause Panel field.");

        Debug.Log($"PauseMenu ready: pause button = {NameOf(pauseButton)}, panel = {(pausePanel != null ? pausePanel.name : "none")}, " +
                  $"continue = {NameOf(continueButton)}, menu = {NameOf(menuButton)}");

        Bind(pauseButton, OpenPause, "pause button");
        Bind(continueButton, ClosePause, "Continue button");
        Bind(menuButton, GoToMainMenu, "Menu button");
    }

    private void FindParts()
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            string words = ButtonWords(button);

            // "pause" is checked before "menu", so a button named e.g. "PauseMenuButton" is the pause button.
            if (continueButton == null && (words.Contains("continue") || words.Contains("resume")))
                continueButton = button;
            else if (pauseButton == null && words.Contains("pause"))
                pauseButton = button;
            else if (menuButton == null && words.Contains("menu"))
                menuButton = button;
        }

        // The panel is the part of this object that holds the Continue (or Menu) button.
        if (pausePanel == null)
        {
            Button inside = continueButton != null ? continueButton : menuButton;
            if (inside != null)
            {
                Transform part = inside.transform;
                while (part.parent != null && part.parent != transform)
                    part = part.parent;

                if (part.parent == transform && part != inside.transform)
                    pausePanel = part.gameObject;
            }
        }

        // Last try: a child named like "PausePanel" / "Paused".
        if (pausePanel == null)
        {
            foreach (Transform child in transform)
            {
                string lowerName = child.name.ToLowerInvariant();
                if (lowerName.Contains("pause") && child.GetComponent<Button>() == null)
                {
                    pausePanel = child.gameObject;
                    break;
                }
            }
        }

        // The pause button must not be inside the panel (it would be hidden with it).
        if (pauseButton != null && pausePanel != null && pauseButton.transform.IsChildOf(pausePanel.transform))
            pauseButton = null;
    }

    // Puts the pause menu in front of everything in the scene (dialogue box, fades, end screens)
    // and makes sure its buttons are the first thing a click reaches.
    private void BringToFront()
    {
        Canvas sceneCanvas = transform.parent != null ? transform.parent.GetComponentInParent<Canvas>() : null;
        int sortingLayer = sceneCanvas != null ? sceneCanvas.rootCanvas.sortingLayerID : 0;

        // Its own drawing layer inside the scene's Canvas.
        Canvas ownCanvas = GetComponent<Canvas>();
        if (ownCanvas == null || ownCanvas.isRootCanvas)
        {
            if (ownCanvas == null)
                ownCanvas = gameObject.AddComponent<Canvas>();
        }

        if (!ownCanvas.isRootCanvas)
        {
            ownCanvas.overrideSorting = true;
            ownCanvas.sortingLayerID = sortingLayer;
            ownCanvas.sortingOrder = ButtonsOrder;
        }

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        // The panel picture is a sprite: draw it just behind the buttons, in front of the game.
        foreach (SpriteRenderer sprite in GetComponentsInChildren<SpriteRenderer>(true))
        {
            sprite.sortingLayerID = sortingLayer;
            sprite.sortingOrder = PanelSpriteOrder;
        }

        // Animations inside the pause menu keep playing while the game is paused.
        foreach (Animator animator in GetComponentsInChildren<Animator>(true))
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
    }

    // The "Paused" title (any text in the panel that is not a button label) becomes white
    // with a thin dark outline, so it can be read over any background.
    // Only the color is changed: the panel is still hidden at this point, and changing the outline of a
    // hidden text throws an error (that error used to stop the rest of the setup, like the dim layer).
    private void ColorTitle()
    {
        foreach (TMP_Text text in pausePanel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.GetComponentInParent<Button>(true) != null)
                continue;

            text.color = titleColor;
        }
    }

    // The panel's own picture (a sprite with an animation) is switched off:
    // the pause screen is only the dimmed game with the title and the buttons.
    private void HidePanelPicture()
    {
        foreach (SpriteRenderer sprite in pausePanel.GetComponentsInChildren<SpriteRenderer>(true))
            sprite.enabled = false;

        foreach (Animator animator in pausePanel.GetComponentsInChildren<Animator>(true))
            animator.enabled = false;

        // A UI Image on the panel object itself (not on the buttons) is hidden too.
        Image panelImage = pausePanel.GetComponent<Image>();
        if (panelImage != null)
            panelImage.enabled = false;
    }

    private static string NameOf(Button button)
    {
        return button != null ? button.gameObject.name : "none";
    }

    // A button's name plus its label, e.g. "button (1) continue".
    private static string ButtonWords(Button button)
    {
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        string labelText = label != null ? label.text : "";
        return (button.gameObject.name + " " + labelText).ToLowerInvariant();
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action, string description)
    {
        if (button == null)
        {
            Debug.LogWarning($"PauseMenu: no {description} was found.");
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    // A full-screen dark layer just behind the panel: it dims the game and blocks clicks on it
    // (so the dialogue can't be skipped while paused).
    private void CreateDimLayer()
    {
        dimLayer = new GameObject("PauseDim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform dimRect = dimLayer.GetComponent<RectTransform>();
        dimRect.SetParent(pausePanel.transform.parent, false);
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = new Vector2(-2000f, -2000f);
        dimRect.offsetMax = new Vector2(2000f, 2000f);
        dimRect.SetSiblingIndex(pausePanel.transform.GetSiblingIndex());
        dimLayer.GetComponent<Image>().color = dimColor;

        // Drawn behind the panel sprite but in front of the game, and it catches clicks meant for the game.
        Canvas ownCanvas = GetComponent<Canvas>();
        Canvas dimCanvas = dimLayer.AddComponent<Canvas>();
        dimCanvas.overrideSorting = true;
        dimCanvas.sortingLayerID = ownCanvas != null ? ownCanvas.sortingLayerID : 0;
        dimCanvas.sortingOrder = DimOrder;
        dimLayer.AddComponent<GraphicRaycaster>();

        dimLayer.SetActive(false);
    }

    void Update()
    {
        if (BackPressed())
        {
            if (pausePanel != null && pausePanel.activeSelf)
                ClosePause();
            else
                OpenPause();
            return;
        }

        // Backup for the buttons: even if something else in the scene catches the click first,
        // a tap/click released on top of a pause button still works.
        if (!PointerReleased(out Vector2 pointer))
            return;

        if (IsPointerOver(pauseButton, pointer))
            OpenPause();
        else if (IsPointerOver(continueButton, pointer))
            ClosePause();
        else if (IsPointerOver(menuButton, pointer))
            GoToMainMenu();
    }

    private static bool IsPointerOver(Button button, Vector2 screenPoint)
    {
        if (button == null || !button.gameObject.activeInHierarchy || !button.interactable)
            return false;

        RectTransform rect = button.transform as RectTransform;
        Canvas canvas = button.GetComponentInParent<Canvas>();
        Camera eventCamera = null;
        if (canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            eventCamera = canvas.rootCanvas.worldCamera != null ? canvas.rootCanvas.worldCamera : Camera.main;

        return rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, eventCamera);
    }

    // A mouse click or a finger lifted this frame, and where.
    private static bool PointerReleased(out Vector2 position)
    {
        position = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame)
        {
            position = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }
        if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            position = Mouse.current.position.ReadValue();
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Ended)
        {
            position = Input.GetTouch(0).position;
            return true;
        }
        if (Input.GetMouseButtonUp(0))
        {
            position = Input.mousePosition;
            return true;
        }
#endif
        return false;
    }

    // Escape on a keyboard, the Back button on Android.
    private static bool BackPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape))
            return true;
#endif
        return false;
    }

    public void OpenPause()
    {
        // The button and the backup check can both fire on the same tap; only act once per frame.
        if (pausePanel == null || pausePanel.activeSelf || lastActionFrame == Time.frameCount)
            return;

        lastActionFrame = Time.frameCount;
        Debug.Log("PauseMenu: paused.");

        if (dimLayer != null) dimLayer.SetActive(true);
        pausePanel.SetActive(true);
        if (pauseButton != null) pauseButton.gameObject.SetActive(false);

        Time.timeScale = 0f;
        AudioListener.pause = true;
        IsPaused = true;

        if (popCoroutine != null) StopCoroutine(popCoroutine);
        popCoroutine = StartCoroutine(Pop());
    }

    public void ClosePause()
    {
        if (lastActionFrame == Time.frameCount)
            return;

        lastActionFrame = Time.frameCount;
        if (pausePanel != null) pausePanel.SetActive(false);
        if (dimLayer != null) dimLayer.SetActive(false);
        if (pauseButton != null) pauseButton.gameObject.SetActive(true);
        Resume();
    }

    public void GoToMainMenu()
    {
        Resume();
        SceneManager.LoadScene(string.IsNullOrEmpty(mainMenuSceneName) ? "Main Menu" : mainMenuSceneName);
    }

    private static void Resume()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        IsPaused = false;
    }

    // The panel pops in a little (uses real time, since the game time is stopped).
    private IEnumerator Pop()
    {
        Transform panelTransform = pausePanel.transform;
        float elapsed = 0f;
        float duration = Mathf.Max(popDuration, 0.01f);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            panelTransform.localScale = Vector3.Lerp(panelScale * 0.9f, panelScale, progress);
            yield return null;
        }

        panelTransform.localScale = panelScale;
        popCoroutine = null;
    }

    private void OnDestroy()
    {
        // Leaving the scene while paused must not leave the next scene frozen.
        if (IsPaused)
            Resume();
    }
}
