using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

// Makes every scene fill any screen (phones, tablets, PC) without touching the scenes themselves.
// Runs by itself whenever a scene loads; nothing needs to be attached to any object.
//
// Why it is needed: every Canvas was saved with "Constant Pixel Size", so the UI and the backgrounds
// kept the size they had in the editor's Game view (about 922 x 518 pixels). On a 2400 x 1080 phone
// that left a small picture in the middle with empty space around it.
//
// What it does in each scene:
//  1. Canvas Scaler -> "Scale With Screen Size", so the UI grows and shrinks with the screen.
//  2. Backgrounds (objects whose name starts with "BG" or contains "Background") are scaled up to
//     cover the whole screen. On screens wider than 16:9 a little of the top and bottom is cropped.
//  3. Video backgrounds (the Main Menu) fill the screen too.
//  4. Any leftover gap shows black instead of the blue camera color.
public static class ScreenFitter
{
    // The size every scene was laid out in: the 1920x1080 backgrounds at scale 48 (19.2 x 10.8 units).
    private static readonly Vector2 DesignResolution = new Vector2(921.6f, 518.4f);

    // Backgrounds are made 3% bigger than the screen so moving them (shakes, running) never shows an edge.
    private const float BackgroundOverscan = 1.03f;

    // Each background's scale as it was saved in the scene, so fitting again (after a resize or a
    // rotation) always starts from the original size instead of growing it more and more.
    private static readonly System.Collections.Generic.Dictionary<Transform, Vector3> originalScales =
        new System.Collections.Generic.Dictionary<Transform, Vector3>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        originalScales.Clear();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        // A small hidden helper that re-fits the screen whenever its size changes
        // (a PC window being resized, a phone or tablet rotating).
        GameObject watcher = new GameObject("ScreenFitter (watcher)");
        watcher.hideFlags = HideFlags.HideAndDontSave;
        Object.DontDestroyOnLoad(watcher);
        watcher.AddComponent<ScreenSizeWatcher>();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Objects from the previous scene are gone.
        originalScales.Clear();
        Apply();
    }

    private class ScreenSizeWatcher : MonoBehaviour
    {
        private int lastWidth;
        private int lastHeight;

        private void Start()
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;
        }

        private void Update()
        {
            if (Screen.width == lastWidth && Screen.height == lastHeight)
                return;

            lastWidth = Screen.width;
            lastHeight = Screen.height;
            Apply();
        }
    }

    public static void Apply()
    {
        // Wider screens (phones) keep the full height; narrower ones (tablets) keep the full width.
        float screenAspect = (float)Screen.width / Mathf.Max(Screen.height, 1);
        float designAspect = DesignResolution.x / DesignResolution.y;
        float match = screenAspect >= designAspect ? 1f : 0f;

        foreach (CanvasScaler scaler in Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include))
        {
            Canvas canvas = scaler.GetComponent<Canvas>();
            if (canvas == null || !canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace)
                continue;

            ConfigureScaler(scaler);
        }

        foreach (Camera sceneCamera in Object.FindObjectsByType<Camera>())
        {
            if (sceneCamera.clearFlags == CameraClearFlags.SolidColor || sceneCamera.clearFlags == CameraClearFlags.Skybox)
            {
                sceneCamera.clearFlags = CameraClearFlags.SolidColor;
                sceneCamera.backgroundColor = Color.black;
            }
        }

        foreach (SpriteRenderer sprite in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include))
        {
            if (IsBackground(sprite.gameObject.name))
                CoverScreen(sprite, match);
        }

        foreach (Image image in Object.FindObjectsByType<Image>(FindObjectsInactive.Include))
        {
            if (IsBackground(image.gameObject.name))
                CoverScreen(image, match);
        }

        foreach (VideoPlayer video in Object.FindObjectsByType<VideoPlayer>(FindObjectsInactive.Include))
        {
            if (video.renderMode == VideoRenderMode.CameraFarPlane || video.renderMode == VideoRenderMode.CameraNearPlane)
                video.aspectRatio = VideoAspectRatio.FitOutside;
        }
    }

    // Sets a Canvas Scaler the same way as every scene's Canvas (used by the pause menu's own canvas too).
    public static void ConfigureScaler(CanvasScaler scaler)
    {
        float screenAspect = (float)Screen.width / Mathf.Max(Screen.height, 1);
        float designAspect = DesignResolution.x / DesignResolution.y;

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = DesignResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = screenAspect >= designAspect ? 1f : 0f;

        // The Canvas Scaler only applies new settings right before the frame is drawn, which is after
        // every Start(). Until then the canvas still has its old Constant Pixel Size: as wide as the
        // screen in pixels (2400 on a phone, not 921.6). Scene scripts measure the canvas in Start()
        // to place characters (the classmates' spacing, keeping them on screen), so on phones they
        // were spread far apart, past the dialogue box. Turning the scaler off and on applies it now.
        if (scaler.isActiveAndEnabled)
        {
            scaler.enabled = false;
            scaler.enabled = true;
        }
    }

    private static bool IsBackground(string objectName)
    {
        string lowerName = objectName.ToLowerInvariant();
        return lowerName.StartsWith("bg") || lowerName.Contains("background");
    }

    // The screen's size in the canvas's own units, once the Canvas Scaler above is applied.
    private static Vector2 CanvasAreaFor(float match)
    {
        Vector2 screen = new Vector2(Screen.width, Screen.height);
        float logWidth = Mathf.Log(screen.x / DesignResolution.x, 2f);
        float logHeight = Mathf.Log(screen.y / DesignResolution.y, 2f);
        float scaleFactor = Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, match));
        return screen / Mathf.Max(scaleFactor, 0.0001f);
    }

    // A background sprite grows (keeping its shape) until it covers the whole screen.
    private static void CoverScreen(SpriteRenderer background, float match)
    {
        if (background.sprite == null)
            return;

        Vector3 spriteSize = background.sprite.bounds.size;
        Vector2 area;
        Vector2 currentSize;

        Canvas canvas = background.GetComponentInParent<Canvas>(true);
        if (canvas != null && canvas.rootCanvas.renderMode != RenderMode.WorldSpace)
        {
            // Inside a Canvas: measure the sprite in canvas units.
            Transform canvasTransform = canvas.rootCanvas.transform;
            Vector3 size = canvasTransform.InverseTransformVector(background.transform.TransformVector(spriteSize));
            currentSize = new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y));
            area = CanvasAreaFor(match);
        }
        else
        {
            // A world sprite: measure it against what the camera sees.
            Camera sceneCamera = Camera.main;
            if (sceneCamera == null || !sceneCamera.orthographic)
                return;

            float viewHeight = sceneCamera.orthographicSize * 2f;
            area = new Vector2(viewHeight * ((float)Screen.width / Mathf.Max(Screen.height, 1)), viewHeight);
            Vector3 lossyScale = background.transform.lossyScale;
            currentSize = new Vector2(Mathf.Abs(spriteSize.x * lossyScale.x), Mathf.Abs(spriteSize.y * lossyScale.y));
        }

        ScaleToCover(background.transform, currentSize, area);
    }

    // A background UI Image with a fixed size grows until it covers the whole canvas.
    private static void CoverScreen(Image background, float match)
    {
        RectTransform rect = background.rectTransform;
        Canvas canvas = background.canvas;
        if (canvas == null || canvas.rootCanvas.renderMode == RenderMode.WorldSpace)
            return;

        // Stretched images already follow the canvas.
        if (rect.anchorMin != rect.anchorMax)
            return;

        Vector3 size = canvas.rootCanvas.transform.InverseTransformVector(rect.TransformVector(rect.rect.size));
        ScaleToCover(rect, new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y)), CanvasAreaFor(match));
    }

    // Grows (or shrinks back toward its saved size) so the background exactly covers the area.
    // It never becomes smaller than its saved size, so nothing that used to fit gets cut more.
    private static void ScaleToCover(Transform target, Vector2 currentSize, Vector2 area)
    {
        if (currentSize.x <= 0f || currentSize.y <= 0f)
            return;

        if (!originalScales.TryGetValue(target, out Vector3 originalScale))
        {
            originalScale = target.localScale;
            originalScales[target] = originalScale;
        }

        // currentSize was measured at the current scale; convert it back to the saved size.
        float currentToOriginal = Mathf.Abs(originalScale.x) > 0f ? Mathf.Abs(target.localScale.x / originalScale.x) : 1f;
        Vector2 originalSize = currentSize / Mathf.Max(currentToOriginal, 0.0001f);

        // A little extra (BackgroundOverscan) so screen shakes and the running bob never show an edge.
        float factor = Mathf.Max(area.x / originalSize.x, area.y / originalSize.y, 1f) * BackgroundOverscan;
        target.localScale = originalScale * factor;
    }
}
