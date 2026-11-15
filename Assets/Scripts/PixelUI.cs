using System.Collections.Generic;
using UnityEngine;
using TMPro;

// Shared pieces of the game's pixel-art look, for UI that is made or restyled by code:
// the pixel font, the dialogue box colors, and rounded pixel rectangles.
public static class PixelUI
{
    public const string FontPath = "Fonts/BoldPixels SDF";

    // The blue of the dialogue box border, and the dark text color used on white.
    public static readonly Color FrameBlue = new Color(0.29f, 0.43f, 0.84f);
    public static readonly Color TextNavy = new Color(0.12f, 0.15f, 0.28f);

    // One corner of the rounded shape (the other three are mirrored).
    // '.' = see-through, 'B' = border, 'F' = fill.
    private static readonly string[] CornerPixels =
    {
        "...BB",
        ".BBBB",
        ".BBFF",
        "BBFFF",
        "BBFFF",
    };
    public const int BorderPixels = 2;

    private static TMP_FontAsset font;
    private static bool triedFont;
    private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        font = null;
        triedFont = false;
        sprites.Clear();
    }

    // The game's pixel font (Assets/Resources/Fonts/BoldPixels SDF), or null if it is missing.
    public static TMP_FontAsset Font
    {
        get
        {
            if (font == null && !triedFont)
            {
                triedFont = true;
                font = Resources.Load<TMP_FontAsset>(FontPath);
                if (font == null)
                    Debug.LogWarning($"PixelUI: the pixel font was not found at Assets/Resources/{FontPath}");
            }

            return font;
        }
    }

    // A rounded rectangle drawn pixel by pixel (sharp, not blurry). Use it on an Image with Type = Sliced:
    // it stretches to any size while the corners and the 2-pixel border keep their size.
    // pixelSize = how many UI units one of its pixels covers. Made once per look, then reused.
    public static Sprite RoundedSprite(Color border, Color fill, float pixelSize)
    {
        string key = ColorUtility.ToHtmlStringRGBA(border) + ColorUtility.ToHtmlStringRGBA(fill) + pixelSize;
        if (sprites.TryGetValue(key, out Sprite cached) && cached != null)
            return cached;

        int corner = CornerPixels.Length;
        int size = corner * 2 + 1;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                char pixel = PixelAt(x, y, size, corner);
                texture.SetPixel(x, y, pixel == 'B' ? border : pixel == 'F' ? fill : Color.clear);
            }
        }

        texture.Apply();

        // 100 / pixelSize pixels per unit: one texture pixel becomes pixelSize UI units.
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
            100f / Mathf.Max(pixelSize, 0.5f), 0, SpriteMeshType.FullRect, new Vector4(corner, corner, corner, corner));

        sprites[key] = sprite;
        return sprite;
    }

    // The corners come from CornerPixels (mirrored), the middle row and column are the straight edges,
    // and the very middle is fill.
    private static char PixelAt(int x, int y, int size, int corner)
    {
        int fromSideX = x < corner ? x : (x > corner ? size - 1 - x : -1);
        int fromSideY = y < corner ? y : (y > corner ? size - 1 - y : -1);

        if (fromSideX >= 0 && fromSideY >= 0)
            return CornerPixels[fromSideY][fromSideX];

        if (fromSideX < 0 && fromSideY < 0)
            return 'F';

        int fromEdge = fromSideX >= 0 ? fromSideX : fromSideY;
        return fromEdge < BorderPixels ? 'B' : 'F';
    }
}
