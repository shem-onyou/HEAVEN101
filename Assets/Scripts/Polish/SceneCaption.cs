using UnityEngine;
using UnityEngine.UI;
using TMPro;

// "DAY 2 · MORNING / Living Room" in the top-left corner, once the scene has faded in from black.
// It waits for the scene's own FadeIn to clear (so it never shows over a black screen or an act title),
// stays a few seconds and fades away. It gives up quietly if the screen never clears.
// Made by SceneAtmosphere.
public class SceneCaption : MonoBehaviour
{
    private const float MinimumWait = 0.8f;
    private const float ClearFor = 0.5f;
    private const float GiveUpAfter = 40f;
    private const float FadeInDuration = 0.7f;
    private const float HoldDuration = 3.2f;
    private const float FadeOutDuration = 0.9f;
    private const float SlideDistance = 16f;
    public const float Height = 64f;

    // The caption's height while it is on screen, so story notifications can go under it.
    public static float VisibleHeight { get; private set; }

    private CanvasGroup group;
    private RectTransform rect;
    private Vector2 restPosition;
    private Image fadeIn;
    private Image fadeOut;
    private GameObject kalatProgress;
    private float age;
    private float clearTime;
    private float shownAge = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        VisibleHeight = 0f;
    }

    // kalatProgress: the "Kalat: x / y" object of the scene, if any; the caption goes under it while it shows.
    public static SceneCaption Create(RectTransform canvas, string dayAndTime, string place, GameObject kalatProgress)
    {
        RectTransform root = PolishUI.CreateRect("SceneCaption", canvas);
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.sizeDelta = new Vector2(320f, Height);
        root.SetAsLastSibling();

        CanvasGroup canvasGroup = root.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // A soft dark glow behind the words, so they read over any background.
        Image shade = PolishUI.CreateImage("Shade", root, PolishUI.SoftDot, new Color(0f, 0f, 0f, 0.55f));
        RectTransform shadeRect = shade.rectTransform;
        shadeRect.anchorMin = new Vector2(0f, 0.5f);
        shadeRect.anchorMax = new Vector2(0f, 0.5f);
        shadeRect.pivot = new Vector2(0.5f, 0.5f);
        shadeRect.sizeDelta = new Vector2(380f, 150f);
        shadeRect.anchoredPosition = new Vector2(90f, 0f);

        TextMeshProUGUI top = PolishUI.CreateText("When", root, dayAndTime, 17f, new Color(1f, 0.84f, 0.52f, 1f), TextAlignmentOptions.BottomLeft);
        top.characterSpacing = 6f;
        top.textWrappingMode = TextWrappingModes.NoWrap;
        RectTransform topRect = top.rectTransform;
        topRect.anchorMin = new Vector2(0f, 0.55f);
        topRect.anchorMax = new Vector2(1f, 1f);
        topRect.offsetMin = new Vector2(2f, 0f);
        topRect.offsetMax = Vector2.zero;

        Image line = PolishUI.CreateImage("Line", root, null, new Color(1f, 0.84f, 0.52f, 0.8f));
        RectTransform lineRect = line.rectTransform;
        lineRect.anchorMin = new Vector2(0f, 0.53f);
        lineRect.anchorMax = new Vector2(0f, 0.53f);
        lineRect.pivot = new Vector2(0f, 0.5f);
        lineRect.sizeDelta = new Vector2(46f, 2f);
        lineRect.anchoredPosition = new Vector2(3f, 0f);

        TextMeshProUGUI bottom = PolishUI.CreateText("Where", root, place, 26f, Color.white, TextAlignmentOptions.TopLeft);
        bottom.textWrappingMode = TextWrappingModes.NoWrap;
        RectTransform bottomRect = bottom.rectTransform;
        bottomRect.anchorMin = new Vector2(0f, 0f);
        bottomRect.anchorMax = new Vector2(1f, 0.5f);
        bottomRect.offsetMin = new Vector2(2f, 0f);
        bottomRect.offsetMax = Vector2.zero;

        SceneCaption caption = root.gameObject.AddComponent<SceneCaption>();
        caption.group = canvasGroup;
        caption.rect = root;
        caption.kalatProgress = kalatProgress;
        caption.restPosition = new Vector2(22f, -18f);
        root.anchoredPosition = caption.restPosition;
        caption.fadeIn = FindImage(canvas, "FadeIn");
        caption.fadeOut = FindImage(canvas, "FadeOut");
        return caption;
    }

    private static Image FindImage(Transform canvas, string objectName)
    {
        foreach (Image image in canvas.GetComponentsInChildren<Image>(true))
        {
            if (image.gameObject.name == objectName)
                return image;
        }

        return null;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        age += deltaTime;

        if (shownAge < 0f)
        {
            // Waiting for the scene to fade in from black.
            if (age > GiveUpAfter)
            {
                Destroy(gameObject);
                return;
            }

            // The corner must be free too (a story notification may be showing there).
            clearTime = ScreenIsClear() && !StoryToast.AnyVisible ? clearTime + deltaTime : 0f;
            if (age < MinimumWait || clearTime < ClearFor)
                return;

            shownAge = 0f;
            rect.SetAsLastSibling();

            // Under the "Kalat: x / y" progress if it is on screen right now.
            if (kalatProgress != null && kalatProgress.activeInHierarchy)
                restPosition = new Vector2(restPosition.x, restPosition.y - 64f);
        }

        // The scene is fading out to black: leave quickly with it.
        if (Covered(fadeOut))
            shownAge = Mathf.Max(shownAge, FadeInDuration + HoldDuration);

        shownAge += deltaTime;
        VisibleHeight = Height + 6f;

        if (shownAge < FadeInDuration)
        {
            float progress = shownAge / FadeInDuration;
            float eased = 1f - (1f - progress) * (1f - progress);
            group.alpha = eased;
            rect.anchoredPosition = restPosition - new Vector2(SlideDistance * (1f - eased), 0f);
            return;
        }

        rect.anchoredPosition = restPosition;
        float fadeStart = FadeInDuration + HoldDuration;
        if (shownAge < fadeStart)
        {
            group.alpha = 1f;
            return;
        }

        float fade = Mathf.Clamp01((shownAge - fadeStart) / FadeOutDuration);
        group.alpha = 1f - fade;
        if (fade >= 1f)
            Destroy(gameObject);
    }

    private bool ScreenIsClear()
    {
        return !Covered(fadeIn) && !Covered(fadeOut);
    }

    // A full-screen fade image that is on screen and mostly opaque.
    private static bool Covered(Image image)
    {
        if (image == null || !image.isActiveAndEnabled)
            return false;

        float alpha = image.color.a;
        CanvasGroup canvasGroup = image.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
            alpha *= canvasGroup.alpha;
        return alpha > 0.15f;
    }

    private void OnDestroy()
    {
        VisibleHeight = 0f;
    }
}
