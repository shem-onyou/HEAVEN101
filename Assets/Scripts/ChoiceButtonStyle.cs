using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Gives every dialogue choice button (choice_Button1, choice_Button2, ...) in every act the game's pixel look:
// a rounded pixel frame like the dialogue box, a darker blue edge under it so it looks raised, dark text in
// the pixel font, a light blue tint when hovered or pressed, and a small pop when the choices appear
// (ChoiceButtonMotion).
//
// Runs by itself when a scene loads, before the scene scripts' Start, so nothing in the scenes needs to change
// (and a button a script copies later, like Act 3's second choice, is already styled).
public static class ChoiceButtonStyle
{
    private const string ButtonNamePrefix = "choice_Button";

    // One pixel of the frame = this many UI units (the same as the Acts screen panels).
    private const float PixelSize = 3f;

    private static readonly Color HoverTint = new Color(0.84f, 0.9f, 1f);
    private static readonly Color PressedTint = new Color(0.68f, 0.78f, 1f);
    private static readonly Color DisabledTint = new Color(0.78f, 0.78f, 0.78f, 0.8f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (Button button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include))
        {
            if (button.gameObject.scene == scene && button.gameObject.name.StartsWith(ButtonNamePrefix))
                Apply(button);
        }
    }

    public static void Apply(Button button)
    {
        // Already styled (e.g. a copy of a styled button).
        if (button == null || button.GetComponent<ChoiceButtonMotion>() != null)
            return;

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = PixelUI.RoundedSprite(PixelUI.FrameBlue, Color.white, PixelSize);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = Color.white;

            // A darker blue copy of the frame just under it: a raised pixel button.
            Shadow edge = image.GetComponent<Shadow>();
            if (edge == null)
                edge = image.gameObject.AddComponent<Shadow>();
            edge.effectColor = new Color(PixelUI.FrameBlue.r * 0.55f, PixelUI.FrameBlue.g * 0.55f, PixelUI.FrameBlue.b * 0.55f, 1f);
            edge.effectDistance = new Vector2(0f, -PixelSize * 2f);
            edge.useGraphicAlpha = true;

            if (button.targetGraphic == null)
                button.targetGraphic = image;
        }

        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = HoverTint;
        colors.pressedColor = PressedTint;
        // Not left tinted after a click.
        colors.selectedColor = Color.white;
        colors.disabledColor = DisabledTint;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            if (PixelUI.Font != null)
                label.font = PixelUI.Font;
            label.color = PixelUI.TextNavy;
            label.alignment = TextAlignmentOptions.Center;

            // Keeps the words off the pixel border; a long answer shrinks to fit instead of spilling out.
            float side = PixelSize * 4f;
            float topBottom = PixelSize * 2.5f;
            label.margin = new Vector4(side, topBottom, side, topBottom);
            float fullSize = label.enableAutoSizing ? label.fontSizeMax : label.fontSize;
            label.enableAutoSizing = true;
            label.fontSizeMax = fullSize;
            label.fontSizeMin = Mathf.Min(12f, fullSize);
        }

        button.gameObject.AddComponent<ChoiceButtonMotion>();
    }
}
