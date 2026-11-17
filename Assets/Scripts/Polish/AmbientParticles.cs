using UnityEngine;
using UnityEngine.UI;

// Slow floating specks over the background, behind the characters: dust in the morning light of the house
// and the classroom, or rising golden light at the gates of Heaven.
// All the specks are drawn by this one UI element (one draw call, no extra objects), and they freeze
// with the pause menu. Made by SceneAtmosphere.
[RequireComponent(typeof(CanvasRenderer))]
public class AmbientParticles : MaskableGraphic
{
    public enum Kind
    {
        Dust,
        Light
    }

    private struct Speck
    {
        public Vector2 position;   // 0..1 across the area
        public Vector2 velocity;   // area units per second
        public float size;
        public float alpha;
        public float twinkleSpeed;
        public float phase;
        public float swayAmount;
    }

    private Kind kind;
    private Speck[] specks = new Speck[0];
    private float time;

    public override Texture mainTexture => PolishUI.SoftDot != null ? PolishUI.SoftDot.texture : s_WhiteTexture;

    public void Setup(Kind particleKind, int count, Color tint)
    {
        kind = particleKind;
        color = tint;
        raycastTarget = false;

        specks = new Speck[Mathf.Max(0, count)];
        for (int i = 0; i < specks.Length; i++)
            specks[i] = NewSpeck(true);

        SetVerticesDirty();
    }

    private Speck NewSpeck(bool anywhere)
    {
        Speck speck = new Speck();
        bool light = kind == Kind.Light;

        // New specks start just below the area (they drift upward), the first ones anywhere.
        speck.position = new Vector2(Random.value, anywhere ? Random.value : -0.05f);
        speck.velocity = new Vector2(Random.Range(-0.006f, 0.006f), light ? Random.Range(0.02f, 0.045f) : Random.Range(0.006f, 0.018f));
        speck.size = light ? Random.Range(5f, 11f) : Random.Range(3f, 6f);
        speck.alpha = light ? Random.Range(0.3f, 0.7f) : Random.Range(0.15f, 0.45f);
        speck.twinkleSpeed = light ? Random.Range(1.5f, 3f) : Random.Range(0.6f, 1.4f);
        speck.phase = Random.Range(0f, Mathf.PI * 2f);
        speck.swayAmount = Random.Range(0.004f, 0.012f);
        return speck;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f || specks.Length == 0)
            return;

        time += deltaTime;
        for (int i = 0; i < specks.Length; i++)
        {
            Speck speck = specks[i];
            speck.position += speck.velocity * deltaTime;

            if (speck.position.y > 1.05f || speck.position.x < -0.05f || speck.position.x > 1.05f)
                speck = NewSpeck(false);

            specks[i] = speck;
        }

        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (specks == null || specks.Length == 0)
            return;

        Rect area = rectTransform.rect;
        Color32 tint = color;

        for (int i = 0; i < specks.Length; i++)
        {
            Speck speck = specks[i];
            float twinkle = 0.55f + 0.45f * Mathf.Sin(time * speck.twinkleSpeed + speck.phase);
            float sway = Mathf.Sin(time * 0.7f + speck.phase) * speck.swayAmount;

            // Fade in near the bottom and out near the top, so specks never pop in or out.
            float edge = Mathf.Clamp01(speck.position.y / 0.12f) * Mathf.Clamp01((1.05f - speck.position.y) / 0.15f);

            Color32 vertexColor = tint;
            vertexColor.a = (byte)Mathf.Clamp(tint.a * speck.alpha * twinkle * edge, 0f, 255f);
            if (vertexColor.a == 0)
                continue;

            float x = area.xMin + (speck.position.x + sway) * area.width;
            float y = area.yMin + speck.position.y * area.height;
            float half = speck.size * 0.5f;

            int start = vh.currentVertCount;
            vh.AddVert(new Vector3(x - half, y - half), vertexColor, new Vector2(0f, 0f));
            vh.AddVert(new Vector3(x - half, y + half), vertexColor, new Vector2(0f, 1f));
            vh.AddVert(new Vector3(x + half, y + half), vertexColor, new Vector2(1f, 1f));
            vh.AddVert(new Vector3(x + half, y - half), vertexColor, new Vector2(1f, 0f));
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
