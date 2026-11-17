using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// One story notification on screen ("Nanay will remember this."): a small dark pixel tag with a gold sparkle
// in the top-left corner. It slides in with a soft chime, stays a few seconds and fades away.
// Made by StoryToast.Show (see StoryNotifications); it removes itself. It pauses with the game.
public class StoryToast : MonoBehaviour
{
    private const float Margin = 14f;
    private const float Height = 34f;
    private const float Gap = 6f;
    private const float FadeInDuration = 0.35f;
    private const float HoldDuration = 3f;
    private const float FadeOutDuration = 0.7f;
    private const float SlideDistance = 22f;
    // Below the "Kalat: x / y" progress when it is on screen.
    private const float KalatClearance = 64f;

    private static readonly Color TextColor = new Color(1f, 0.96f, 0.86f, 1f);
    private static readonly Color BorderColor = new Color(1f, 0.8f, 0.42f, 0.9f);
    private static readonly Color FillColor = new Color(0.05f, 0.06f, 0.11f, 0.84f);

    private static readonly List<StoryToast> visible = new List<StoryToast>();

    private RectTransform rect;
    private CanvasGroup group;
    private RectTransform icon;
    private Vector2 restPosition;
    private float age;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        visible.Clear();
    }

    public static void Show(string message)
    {
        RectTransform canvas = PolishUI.FindSceneCanvas();
        if (canvas == null)
            return;

        RectTransform root = PolishUI.CreateRect("StoryNotification", canvas);
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.SetAsLastSibling();

        CanvasGroup canvasGroup = root.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Image background = PolishUI.CreateImage("Tag", root, PolishUI.Rounded(BorderColor, FillColor, 2f), Color.white);
        background.type = Image.Type.Sliced;
        PolishUI.Stretch(background.rectTransform);

        Image sparkle = PolishUI.CreateImage("Sparkle", root, PolishUI.Sparkle, Color.white);
        RectTransform iconRect = sparkle.rectTransform;
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(18f, 18f);
        iconRect.anchoredPosition = new Vector2(19f, 0f);

        TextMeshProUGUI text = PolishUI.CreateText("Line", root, message, 19f, TextColor, TextAlignmentOptions.MidlineLeft);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        float textWidth = Mathf.Ceil(text.GetPreferredValues(message, 2000f, Height).x);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(0f, 1f);
        textRect.pivot = new Vector2(0f, 0.5f);
        textRect.sizeDelta = new Vector2(textWidth + 4f, 0f);
        textRect.anchoredPosition = new Vector2(36f, 1f);

        root.sizeDelta = new Vector2(36f + textWidth + 16f, Height);

        StoryToast toast = root.gameObject.AddComponent<StoryToast>();
        toast.rect = root;
        toast.group = canvasGroup;
        toast.icon = iconRect;
        toast.restPosition = new Vector2(Margin, -Margin - TopOffset() - StackOffset());
        root.anchoredPosition = toast.restPosition - new Vector2(SlideDistance, 0f);
        visible.Add(toast);

        UISounds.Remember();
    }

    // Room taken by notifications that are still on screen, so a new one goes under them.
    private static float StackOffset()
    {
        float offset = 0f;
        foreach (StoryToast toast in visible)
        {
            if (toast != null)
                offset += Height + Gap;
        }

        return offset;
    }

    // Below the "Kalat" progress and the scene caption ("DAY 1 · MORNING") when they are on screen.
    private static float TopOffset()
    {
        GameObject kalatProgress = PolishUI.FindInScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Kalat_progress");
        float offset = kalatProgress != null && kalatProgress.activeInHierarchy ? KalatClearance : 0f;
        return offset + SceneCaption.VisibleHeight;
    }

    // A notification is on screen (the scene caption waits for it to leave the corner).
    public static bool AnyVisible
    {
        get
        {
            visible.RemoveAll(toast => toast == null);
            return visible.Count > 0;
        }
    }

    private void Update()
    {
        age += Time.deltaTime;

        if (age < FadeInDuration)
        {
            float progress = Mathf.Clamp01(age / FadeInDuration);
            float eased = 1f - (1f - progress) * (1f - progress);
            group.alpha = eased;
            rect.anchoredPosition = restPosition - new Vector2(SlideDistance * (1f - eased), 0f);
            icon.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, eased);
            return;
        }

        rect.anchoredPosition = restPosition;
        icon.localScale = Vector3.one;

        float fadeStart = FadeInDuration + HoldDuration;
        if (age < fadeStart)
        {
            group.alpha = 1f;
            return;
        }

        float fade = Mathf.Clamp01((age - fadeStart) / FadeOutDuration);
        group.alpha = 1f - fade;
        if (fade >= 1f)
            Destroy(gameObject);
    }

    private void OnDestroy()
    {
        visible.Remove(this);
    }
}
