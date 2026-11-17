using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Main Menu and Acts buttons respond to the player: they grow a little under the mouse (or finger)
// and squash slightly while pressed, then settle back. Locked buttons (like an act not unlocked yet) stay still.
// Only the scale changes. Added by itself to every Button in those two scenes when they load,
// and the game logo floats gently (MenuLogoFloat).
public class MenuButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private static readonly string[] MenuScenes = { "Main Menu", "Acts" };

    private const float HoverScale = 1.06f;
    private const float PressedScale = 0.95f;
    private const float Sharpness = 18f;

    private Button button;
    private Vector3 baseScale = Vector3.one;
    private bool hovered;
    private bool pressed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (System.Array.IndexOf(MenuScenes, scene.name) < 0)
            return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Button menuButton in root.GetComponentsInChildren<Button>(true))
            {
                if (menuButton.GetComponent<MenuButtonMotion>() == null && menuButton.GetComponent<ChoiceButtonMotion>() == null)
                    menuButton.gameObject.AddComponent<MenuButtonMotion>();
            }

            foreach (SpriteRenderer sprite in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sprite.gameObject.name.StartsWith("GameLogo") && sprite.GetComponent<MenuLogoFloat>() == null)
                    sprite.gameObject.AddComponent<MenuLogoFloat>();
            }
        }
    }

    private void Awake()
    {
        button = GetComponent<Button>();
        baseScale = transform.localScale;
    }

    private void OnDisable()
    {
        hovered = false;
        pressed = false;
        transform.localScale = baseScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
    }

    private void LateUpdate()
    {
        bool usable = button != null && button.IsInteractable();
        float target = !usable ? 1f : pressed ? PressedScale : hovered ? HoverScale : 1f;
        Vector3 goal = baseScale * target;
        if ((transform.localScale - goal).sqrMagnitude < 0.0000001f)
            return;

        // Real time: the menus are never paused, but this keeps it smooth either way.
        transform.localScale = Vector3.Lerp(transform.localScale, goal, 1f - Mathf.Exp(-Sharpness * Time.unscaledDeltaTime));
    }
}
