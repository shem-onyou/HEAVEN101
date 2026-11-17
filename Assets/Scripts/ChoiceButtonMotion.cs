using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The little motions of a dialogue choice button (added by ChoiceButtonStyle): a quick pop with a tiny overshoot
// when the choices appear, one button slightly after the other, and a small grow while the mouse is over it
// or a finger is on it. Only the scale and see-through amount change; the button's place and size are not touched.
// When it is picked, a copy of it lifts and fades away (ChoiceSelectFlash), even though the scene hides the
// real buttons right away.
public class ChoiceButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerClickHandler
{
    private const float PopDuration = 0.18f;
    private const float PopDelayPerButton = 0.07f;
    private const float StartScale = 0.85f;
    private const float OvershootScale = 1.06f;
    private const float HoverScale = 1.05f;

    private Button button;
    private CanvasGroup group;
    private Vector3 baseScale;
    private int createdFrame;
    private bool popping;
    private bool hovered;

    private void Awake()
    {
        button = GetComponent<Button>();
        group = GetComponent<CanvasGroup>();
        if (group == null)
            group = gameObject.AddComponent<CanvasGroup>();

        baseScale = transform.localScale;
        createdFrame = Time.frameCount;
    }

    private void OnEnable()
    {
        hovered = false;

        // While the scene is being set up the buttons are only prepared (and usually hidden right away).
        if (Time.frameCount == createdFrame)
            return;

        StartCoroutine(Pop());
    }

    private void OnDisable()
    {
        popping = false;
        hovered = false;
        transform.localScale = baseScale;
        group.alpha = 1f;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
    }

    // Remembered on press: by the time the click arrives, the scene may already have hidden or locked the button.
    private bool pressedWhileUsable;

    public void OnPointerDown(PointerEventData eventData)
    {
        pressedWhileUsable = button != null && button.IsInteractable();
    }

    // The Button's own click (the scene's choice) runs first; this only adds the picked effect.
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !pressedWhileUsable)
            return;

        pressedWhileUsable = false;
        ChoiceSelectFlash.Spawn(transform as RectTransform);
    }

    private IEnumerator Pop()
    {
        popping = true;
        group.alpha = 0f;
        transform.localScale = baseScale * StartScale;

        // choice_Button1 first, then choice_Button2 a moment later.
        float wait = PopDelayPerButton * Mathf.Min(transform.GetSiblingIndex(), 3);
        while (wait > 0f)
        {
            wait -= Time.unscaledDeltaTime;
            yield return null;
        }

        float elapsed = 0f;
        while (elapsed < PopDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / PopDuration);
            float scale = progress < 0.7f
                ? Mathf.Lerp(StartScale, OvershootScale, progress / 0.7f)
                : Mathf.Lerp(OvershootScale, 1f, (progress - 0.7f) / 0.3f);

            transform.localScale = baseScale * scale;
            group.alpha = Mathf.Clamp01(progress * 2f);
            yield return null;
        }

        transform.localScale = baseScale;
        group.alpha = 1f;
        popping = false;
    }

    // Grows a little under the mouse (or finger), and smoothly back.
    private void LateUpdate()
    {
        if (popping)
            return;

        bool grow = hovered && button != null && button.IsInteractable();
        Vector3 goal = baseScale * (grow ? HoverScale : 1f);
        if ((transform.localScale - goal).sqrMagnitude < 0.000001f)
            return;

        transform.localScale = Vector3.Lerp(transform.localScale, goal, 1f - Mathf.Exp(-25f * Time.unscaledDeltaTime));
    }
}
