using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Act selection screen. Act 1 is always open; every other act stays locked
// (non-interactable, so its Disabled Sprite shows) until the act before it is finished.
//
// Pressing an act:
//  - the act the player was in the middle of (e.g. they quit mid-game): continues from the scene they were in;
//  - an act they already finished: a warning asks first, because playing it again erases that act's answers
//    and starts it over from the beginning;
//  - an act they haven't played yet: starts it from the beginning.
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
    [Tooltip("First scene of Act 2. Empty means \"A2_Broom\".")]
    [SerializeField] private string act2SceneName = "A2_Broom";
    [Tooltip("First scene of Act 3. Empty means \"A3_Broom\".")]
    [SerializeField] private string act3SceneName = "A3_Broom";

    [Header("Replay Warning")]
    [Tooltip("The pixel font of the dialogue boxes. Empty = loaded from Assets/Resources/Fonts/BoldPixels SDF.")]
    [SerializeField] private TMP_FontAsset warningFont;
    [SerializeField] private Color frameColor = new Color(0.29f, 0.43f, 0.84f);
    [SerializeField] private Color textColor = new Color(0.12f, 0.15f, 0.28f);
    [SerializeField] private Color replayButtonColor = new Color(0.82f, 0.31f, 0.31f);
    [Tooltip("How big one pixel of the rounded corners and borders is, in UI units.")]
    [SerializeField] private float pixelSize = 3f;
    [SerializeField] private float warningFadeDuration = 0.15f;

    private static readonly string[] RomanNumerals = { "", "I", "II", "III" };

    private GameObject warningObject;

    void Start()
    {
        // The Acts scene was saved while Act 2 had no scene yet, so an empty value means the default.
        if (string.IsNullOrEmpty(act2SceneName))
            act2SceneName = "A2_Broom";
        if (string.IsNullOrEmpty(act3SceneName))
            act3SceneName = "A3_Broom";

        FindButtons();

        SetupButton(backButton, true, () => SceneManager.LoadScene(mainMenuSceneName));
        SetupButton(act1Button, true, () => PlayAct(1, act1SceneName));
        SetupButton(act2Button, IsUnlocked(2, act2SceneName), () => PlayAct(2, act2SceneName));
        SetupButton(act3Button, IsUnlocked(3, act3SceneName), () => PlayAct(3, act3SceneName));
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
        {
            ButtonClickSound.AddTo(button);
            button.onClick.AddListener(onClick);
        }
    }

    private void PlayAct(int actNumber, string firstSceneName)
    {
        if (warningObject != null)
            return;

        string act = ActLabel(actNumber);
        string savedScene = PlayerDatabase.Data.currentScene;

        // The player left in the middle of this act (past its first scene): continue, or start over?
        // A saved scene that no longer exists in the build (e.g. from an old version) is treated as no saved place.
        if (PlayerDatabase.CurrentAct == actNumber && savedScene != firstSceneName && Application.CanStreamedLevelBeLoaded(savedScene))
        {
            ShowChoicePanel($"Continue {act}?",
                $"You stopped in the middle of {act}.\nContinue where you left off, or restart {act} from the beginning?\n(Restarting erases your choices in {act}.)",
                "Restart", replayButtonColor, () => StartActOver(actNumber, firstSceneName),
                "Continue", frameColor, () => SceneManager.LoadScene(savedScene));
            return;
        }

        if (PlayerDatabase.IsActCompleted(actNumber))
        {
            string message = $"You already finished {act}.\nPlaying it again will erase your choices in {act} and start it over from the beginning.";

            int currentAct = PlayerDatabase.CurrentAct;
            if (currentAct > 0 && currentAct != actNumber && savedScene != PlayerDatabase.FirstSceneOfAct(currentAct))
                message += $"\nYou will also leave your place in {ActLabel(currentAct)}.";

            ShowChoicePanel($"Replay {act}?", message,
                "Cancel", frameColor, null,
                "Replay", replayButtonColor, () => StartActOver(actNumber, firstSceneName));
            return;
        }

        // Not played yet (or the saved place is the act's very first scene): start it.
        StartActOver(actNumber, firstSceneName);
    }

    private static void StartActOver(int actNumber, string firstSceneName)
    {
        PlayerDatabase.RestartAct(actNumber);
        SceneManager.LoadScene(firstSceneName);
    }

    private static string ActLabel(int actNumber)
    {
        return "Act " + (actNumber > 0 && actNumber < RomanNumerals.Length ? RomanNumerals[actNumber] : actNumber.ToString());
    }

    // ------------------------------------------------------------------ choice panel

    // A rounded pixel panel over a dimmed screen with a title, a message and two buttons, e.g.
    // "Replay Act I?" (Cancel / Replay) or "Continue Act II?" (Restart / Continue).
    // A null action just closes the panel. Tapping the dimmed screen outside the panel closes it too.
    private void ShowChoicePanel(string titleText, string message,
        string leftLabel, Color leftColor, System.Action leftAction,
        string rightLabel, Color rightColor, System.Action rightAction)
    {
        Canvas sceneCanvas = act1Button != null ? act1Button.GetComponentInParent<Canvas>() : FindAnyObjectByType<Canvas>();
        if (sceneCanvas == null)
        {
            Debug.LogWarning("Acts: no Canvas found to show the choice panel on.");
            return;
        }

        Canvas rootCanvas = sceneCanvas.rootCanvas;
        TMP_FontAsset font = GetWarningFont();

        // A dark layer over the whole screen: it dims the acts and stops clicks on them.
        RectTransform layer = CreateRect("ChoicePanel", rootCanvas.transform);
        Stretch(layer, Vector2.zero, Vector2.zero);
        warningObject = layer.gameObject;
        layer.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

        // Tapping the dark area (outside the panel) closes it without choosing anything.
        Button outside = layer.gameObject.AddComponent<Button>();
        outside.transition = Selectable.Transition.None;
        outside.onClick.AddListener(CloseWarning);

        // Drawn above everything else in the scene.
        Canvas layerCanvas = layer.gameObject.AddComponent<Canvas>();
        layerCanvas.overrideSorting = true;
        layerCanvas.sortingLayerID = rootCanvas.sortingLayerID;
        layerCanvas.sortingOrder = 100;
        layer.gameObject.AddComponent<GraphicRaycaster>();
        CanvasGroup group = layer.gameObject.AddComponent<CanvasGroup>();

        // The shapes: the panel has its blue border and white inside drawn in; the buttons are white
        // with a grey border, so their color tints both (the border comes out a darker shade).
        Sprite panelSprite = PixelUI.RoundedSprite(frameColor, Color.white, pixelSize);
        Sprite buttonSprite = PixelUI.RoundedSprite(new Color(0.62f, 0.62f, 0.62f), Color.white, pixelSize);
        Vector2 dropShadow = new Vector2(pixelSize * 2f, -pixelSize * 2f);

        RectTransform panel = CreateRect("Panel", layer);
        panel.sizeDelta = new Vector2(600f, 270f);
        CreatePixelImage("Shadow", panel, panelSprite, new Color(0f, 0f, 0f, 0.35f), dropShadow).raycastTarget = false;
        Image frame = CreatePixelImage("Frame", panel, panelSprite, Color.white, Vector2.zero);

        // A tap on the panel itself must not reach the dark layer behind it (which would close it).
        Button panelBlocker = frame.gameObject.AddComponent<Button>();
        panelBlocker.transition = Selectable.Transition.None;
        panelBlocker.interactable = false;

        // Everything inside the border.
        RectTransform box = CreateRect("Box", panel);
        float inset = pixelSize * (PixelUI.BorderPixels + 2);
        Stretch(box, new Vector2(inset, inset), new Vector2(-inset, -inset));

        TMP_Text title = CreateText("Title", box, font, titleText, 34f, textColor);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.sizeDelta = new Vector2(-20f, 44f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -6f);

        TMP_Text body = CreateText("Message", box, font, message, 22f, textColor);
        Stretch(body.rectTransform, new Vector2(16f, 74f), new Vector2(-16f, -54f));
        body.enableAutoSizing = true;
        body.fontSizeMin = 14f;
        body.fontSizeMax = 22f;
        body.lineSpacing = 8f;

        Button leftButton = CreateButton(leftLabel, box, font, leftLabel, buttonSprite, leftColor, new Vector2(-110f, 34f));
        Button rightButton = CreateButton(rightLabel, box, font, rightLabel, buttonSprite, rightColor, new Vector2(110f, 34f));
        SetupChoiceButton(leftButton, leftAction, leftButton, rightButton, outside);
        SetupChoiceButton(rightButton, rightAction, leftButton, rightButton, outside);

        StartCoroutine(FadeInWarning(group, panel));
    }

    // null action = close the panel. Otherwise the buttons lock (no double taps) and the action runs.
    private void SetupChoiceButton(Button button, System.Action action, params Button[] lockOnChoose)
    {
        ButtonClickSound.AddTo(button);

        if (action == null)
        {
            button.onClick.AddListener(CloseWarning);
            return;
        }

        button.onClick.AddListener(() =>
        {
            foreach (Button other in lockOnChoose)
                other.interactable = false;
            action();
        });
    }

    private TMP_FontAsset GetWarningFont()
    {
        if (warningFont == null)
            warningFont = PixelUI.Font;

        if (warningFont != null)
            return warningFont;

        // The pixel font is missing (PixelUI already warned): use the scene's font instead.
        TMP_Text anyText = FindAnyObjectByType<TMP_Text>(FindObjectsInactive.Include);
        return anyText != null ? anyText.font : null;
    }

    private void CloseWarning()
    {
        if (warningObject != null)
            Destroy(warningObject);

        warningObject = null;
    }

    // Fades in with a tiny "pop", like the About panel.
    private IEnumerator FadeInWarning(CanvasGroup group, RectTransform panel)
    {
        float duration = Mathf.Max(warningFadeDuration, 0.01f);
        float elapsed = 0f;
        while (elapsed < duration && group != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            group.alpha = progress;
            panel.localScale = Vector3.one * Mathf.Lerp(0.95f, 1f, progress);
            yield return null;
        }

        if (group != null)
        {
            group.alpha = 1f;
            panel.localScale = Vector3.one;
        }
    }

    // ------------------------------------------------------------------ pixel shapes

    // An Image showing a rounded pixel sprite (PixelUI.RoundedSprite), stretched over its parent and moved by offset.
    private static Image CreatePixelImage(string objectName, Transform parent, Sprite sprite, Color color, Vector2 offset)
    {
        RectTransform rect = CreateRect(objectName, parent);
        Stretch(rect, offset, offset);

        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;
        image.color = color;
        return image;
    }

    // ------------------------------------------------------------------ UI helpers

    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject uiObject = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = (RectTransform)uiObject.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    // Fills the parent, with offsets from its edges (bottom-left, top-right).
    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static TMP_Text CreateText(string objectName, Transform parent, TMP_FontAsset font, string text,
        float fontSize, Color color)
    {
        RectTransform rect = CreateRect(objectName, parent);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null)
            label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    // A rounded pixel button with white text and a darker pixel shadow under it, placed from the bottom middle.
    private Button CreateButton(string objectName, Transform parent, TMP_FontAsset font, string label,
        Sprite sprite, Color color, Vector2 position)
    {
        RectTransform rect = CreateRect(objectName, parent);
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(190f, 52f);
        rect.anchoredPosition = position;

        Color shadowColor = new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f, 1f);
        CreatePixelImage("Shadow", rect, sprite, shadowColor, new Vector2(0f, -pixelSize * 2f)).raycastTarget = false;

        Image face = CreatePixelImage("Face", rect, sprite, color, Vector2.zero);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = face;

        TMP_Text text = CreateText("Text (TMP)", face.transform, font, label, 26f, Color.white);
        Stretch(text.rectTransform, Vector2.zero, Vector2.zero);
        return button;
    }
}
