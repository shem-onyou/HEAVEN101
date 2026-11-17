using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The picked choice lifts a little, glows blue and fades away, so the player sees what they chose.
// The scene scripts hide the choice buttons the moment one is clicked, so this is a look-alike copy
// (no Button on it) placed exactly over the real one. It removes itself.
public class ChoiceSelectFlash : MonoBehaviour
{
    private const float Duration = 0.45f;
    private const float GrowTo = 1.08f;
    private static readonly Color GlowTint = new Color(0.72f, 0.82f, 1f, 1f);

    private Image image;
    private CanvasGroup group;
    private float age;

    public static void Spawn(RectTransform source)
    {
        if (source == null)
            return;

        Canvas sourceCanvas = source.GetComponentInParent<Canvas>(true);
        if (sourceCanvas == null)
            return;

        RectTransform canvasRect = sourceCanvas.rootCanvas.transform as RectTransform;
        if (canvasRect == null)
            return;

        // Same place and size as the button looks right now (its pop or hover scale included).
        Vector3[] corners = new Vector3[4];
        source.GetWorldCorners(corners);
        Vector3 bottomLeft = canvasRect.InverseTransformPoint(corners[0]);
        Vector3 topRight = canvasRect.InverseTransformPoint(corners[2]);
        Vector2 size = new Vector2(Mathf.Abs(topRight.x - bottomLeft.x), Mathf.Abs(topRight.y - bottomLeft.y));
        if (size.x < 1f || size.y < 1f)
            return;

        RectTransform ghost = PolishUI.CreateRect("ChoicePicked", canvasRect);
        ghost.anchorMin = new Vector2(0.5f, 0.5f);
        ghost.anchorMax = new Vector2(0.5f, 0.5f);
        ghost.pivot = new Vector2(0.5f, 0.5f);
        ghost.sizeDelta = size;
        Vector3 middle = (bottomLeft + topRight) * 0.5f;
        ghost.localPosition = new Vector3(middle.x, middle.y, 0f);
        ghost.SetAsLastSibling();

        CanvasGroup canvasGroup = ghost.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Image sourceImage = source.GetComponent<Image>();
        Image ghostImage = PolishUI.CreateImage("Frame", ghost, sourceImage != null ? sourceImage.sprite : null, Color.white);
        ghostImage.type = sourceImage != null ? sourceImage.type : Image.Type.Simple;
        ghostImage.pixelsPerUnitMultiplier = sourceImage != null ? sourceImage.pixelsPerUnitMultiplier : 1f;
        PolishUI.Stretch(ghostImage.rectTransform);

        // The darker edge under the frame, like the real button.
        Shadow sourceEdge = source.GetComponent<Shadow>();
        if (sourceEdge != null)
        {
            Shadow edge = ghostImage.gameObject.AddComponent<Shadow>();
            edge.effectColor = sourceEdge.effectColor;
            edge.effectDistance = sourceEdge.effectDistance;
            edge.useGraphicAlpha = true;
        }

        TMP_Text sourceLabel = source.GetComponentInChildren<TMP_Text>(true);
        if (sourceLabel != null)
        {
            // The real label may be scaled inside the button (and the button may be mid-pop):
            // the copy uses the same size on screen.
            float ratio = 1f;
            float ghostScale = ghost.lossyScale.y;
            if (Mathf.Abs(ghostScale) > 0.0001f)
                ratio = Mathf.Abs(sourceLabel.transform.lossyScale.y / ghostScale);

            TextMeshProUGUI label = PolishUI.CreateText("Label", ghost, sourceLabel.text, sourceLabel.fontSize * ratio, sourceLabel.color);
            label.font = sourceLabel.font;
            label.margin = sourceLabel.margin * ratio;
            label.alignment = sourceLabel.alignment;
            label.enableAutoSizing = false;
            label.textWrappingMode = sourceLabel.textWrappingMode;
            PolishUI.Stretch(label.rectTransform);
        }

        ChoiceSelectFlash flash = ghost.gameObject.AddComponent<ChoiceSelectFlash>();
        flash.image = ghostImage;
        flash.group = canvasGroup;
        ghostImage.color = GlowTint;
    }

    private void Update()
    {
        // Real time: the effect finishes even if the pick opens something that pauses the game.
        age += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(age / Duration);
        float eased = 1f - (1f - progress) * (1f - progress);

        transform.localScale = Vector3.one * Mathf.Lerp(1f, GrowTo, eased);
        if (image != null)
            image.color = Color.Lerp(GlowTint, Color.white, Mathf.Clamp01(progress * 3f));
        if (group != null)
            group.alpha = progress < 0.3f ? 1f : 1f - Mathf.InverseLerp(0.3f, 1f, progress);

        if (progress >= 1f)
            Destroy(gameObject);
    }
}
