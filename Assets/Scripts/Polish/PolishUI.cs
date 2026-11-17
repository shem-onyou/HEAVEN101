using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Small shared pieces for the polish systems (story notifications, title cards, atmosphere, results):
// finding the scene's Canvas, making pixel-font text, and a few sprites drawn by code.
// Object names made here never match the names the scene scripts look for.
public static class PolishUI
{
    private static Sprite sparkle;
    private static Sprite softDot;
    private static Sprite vignette;
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        sparkle = null;
        softDot = null;
        vignette = null;
        cache.Clear();
    }

    // The scene's main Canvas: the root screen canvas named "Canvas", or else the first root screen canvas.
    public static RectTransform FindSceneCanvas(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        Canvas fallback = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace)
                    continue;

                if (canvas.gameObject.name == "Canvas")
                    return canvas.transform as RectTransform;
                if (fallback == null)
                    fallback = canvas;
            }
        }

        return fallback != null ? fallback.transform as RectTransform : null;
    }

    public static RectTransform FindSceneCanvas()
    {
        return FindSceneCanvas(SceneManager.GetActiveScene());
    }

    // Finds an object by name anywhere in the scene, even inactive.
    public static GameObject FindInScene(Scene scene, string objectName)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == objectName)
                return root;

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == objectName)
                    return child.gameObject;
            }
        }

        return null;
    }

    public static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    public static void Stretch(RectTransform rect, float overscan = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(-overscan, -overscan);
        rect.offsetMax = new Vector2(overscan, overscan);
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    // Pixel-font text (falls back to TMP's default font if the pixel font is missing).
    public static TextMeshProUGUI CreateText(string objectName, Transform parent, string message, float fontSize,
        Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        RectTransform rect = CreateRect(objectName, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (PixelUI.Font != null)
            text.font = PixelUI.Font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.text = message;
        return text;
    }

    public static Image CreateImage(string objectName, Transform parent, Sprite sprite, Color color)
    {
        RectTransform rect = CreateRect(objectName, parent);
        rect.gameObject.AddComponent<CanvasRenderer>();
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    // ------------------------------------------------------------------ sprites drawn by code

    // A small 9x9 pixel sparkle (the icon of story notifications).
    public static Sprite Sparkle
    {
        get
        {
            if (sparkle != null)
                return sparkle;

            string[] rows =
            {
                "....G....",
                "....G....",
                "...GWG...",
                "..GGWGG..",
                "GGWWWWWGG",
                "..GGWGG..",
                "...GWG...",
                "....G....",
                "....G....",
            };

            Color gold = new Color(1f, 0.8f, 0.38f, 1f);
            Color white = new Color(1f, 0.98f, 0.88f, 1f);
            sparkle = FromPixels(rows, ch => ch == 'G' ? gold : ch == 'W' ? white : Color.clear, 9f);
            return sparkle;
        }
    }

    // A soft round dot, bright in the middle and fading to nothing (dust and light particles).
    public static Sprite SoftDot
    {
        get
        {
            if (softDot == null)
                softDot = Radial("SoftDot", 32, distance => Mathf.Pow(Mathf.Clamp01(1f - distance), 2f));
            return softDot;
        }
    }

    // Clear in the middle and darker toward the edges and corners (tinted by the Image color).
    public static Sprite Vignette
    {
        get
        {
            if (vignette == null)
                vignette = Radial("Vignette", 64, distance => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.42f, distance)));
            return vignette;
        }
    }

    // A pixel rectangle sprite, made once per look (same idea as PixelUI.RoundedSprite).
    public static Sprite Rounded(Color border, Color fill, float pixelSize)
    {
        return PixelUI.RoundedSprite(border, fill, pixelSize);
    }

    private static Sprite FromPixels(string[] rows, System.Func<char, Color> colorOf, float pixelsPerUnit)
    {
        int height = rows.Length;
        int width = rows[0].Length;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < height; y++)
        {
            string row = rows[height - 1 - y];
            for (int x = 0; x < width; x++)
                texture.SetPixel(x, y, colorOf(row[x]));
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
    }

    // White texture whose alpha depends on the distance from the middle (0 = middle, 1 = edge, ~1.41 = corner).
    private static Sprite Radial(string spriteName, int size, System.Func<float, float> alphaAt)
    {
        if (cache.TryGetValue(spriteName, out Sprite cached) && cached != null)
            return cached;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alphaAt(distance))));
            }
        }

        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        cache[spriteName] = sprite;
        return sprite;
    }
}
