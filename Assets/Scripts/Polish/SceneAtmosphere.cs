using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The mood of each story scene, added when the scene loads (nothing in the scenes needs to change):
//  - a soft vignette over the background: warm in the morning, orange in the afternoon, blue at night,
//    and a golden glow at the gates of Heaven;
//  - floating specks: dust in the morning light, rising light in Heaven;
//  - the "DAY 1 · MORNING / Bedroom" caption once the scene has faded in;
//  - the characters (Nanay, classmate1, classmate2, pulubi) keep the true shape of their drawings, and
//    breathe slowly while they stand there.
// The vignette and the specks sit just above the background and behind everything else in the Canvas.
// Scenes not listed in Moods get only the breathing.
public static class SceneAtmosphere
{
    private enum Light
    {
        Morning,
        Day,
        Afternoon,
        Evening,
        Night,
        Gloomy,
        Heaven
    }

    private class Mood
    {
        public string when;
        public string place;
        public Light light;
        public int specks;

        public Mood(string when, string place, Light light, int specks)
        {
            this.when = when;
            this.place = place;
            this.light = light;
            this.specks = specks;
        }
    }

    // Keep in sync with the story flow in CLAUDE.md. when/place null = no caption.
    private static readonly Dictionary<string, Mood> Moods = new Dictionary<string, Mood>
    {
        { "Act One",         new Mood(null, null, Light.Heaven, 22) },
        { "A1_Bedroom",      new Mood("DAY 1 · MORNING", "Bedroom", Light.Morning, 16) },
        { "A1_LivingRoom",   new Mood("DAY 1 · MORNING", "Living Room", Light.Morning, 14) },
        { "A1_School",       new Mood("DAY 1 · MORNING", "School Gate", Light.Day, 0) },
        { "A1_Classroom",    new Mood("DAY 1 · MORNING", "Classroom", Light.Day, 10) },
        { "A1_School_Uwian", new Mood("DAY 1 · AFTER CLASS", "School Gate", Light.Afternoon, 0) },
        { "A1_LR_umuwi",     new Mood("DAY 1 · AFTERNOON", "Home", Light.Afternoon, 12) },
        { "A1_BR_lastscene", new Mood("DAY 1 · AFTERNOON", "Bedroom", Light.Afternoon, 12) },

        { "A2_Broom",        new Mood("DAY 2 · MORNING", "Bedroom", Light.Morning, 16) },
        { "A2_Lroom",        new Mood("DAY 2 · MORNING", "Living Room", Light.Morning, 14) },
        { "A2_School",       new Mood("DAY 2 · MORNING", "School Gate", Light.Day, 0) },
        { "A2_Croom",        new Mood("DAY 2 · QUIZ DAY", "Classroom", Light.Day, 10) },
        { "A2_School_uwian", new Mood("DAY 2 · AFTER CLASS", "School Gate", Light.Afternoon, 0) },
        { "A2_Lroom_uwi",    new Mood("DAY 2 · EVENING", "Home", Light.Evening, 0) },
        { "A2_Broom_uwi",    new Mood("DAY 2 · NIGHT", "Bedroom", Light.Night, 0) },

        { "A3_Broom",        new Mood("DAY 3 · MORNING", "Bedroom", Light.Gloomy, 12) },
        { "A3_Lroom",        new Mood("DAY 3 · MORNING", "Living Room", Light.Gloomy, 12) },
        { "A3_School",       new Mood("DAY 3 · MORNING", "School Gate", Light.Day, 0) },
        { "A3_Croom",        new Mood("DAY 3", "Classroom", Light.Gloomy, 8) },
        { "A3_School_uwian", new Mood("DAY 3", "The Way Home", Light.Gloomy, 0) },
        { "A3_Lroom_uwi",    new Mood("DAY 3", "Home", Light.Gloomy, 0) },
        { "A3_Heaven",       new Mood(null, null, Light.Heaven, 26) },
    };

    // UI Images with these names are the characters (they keep their true shape, and they breathe).
    private static readonly string[] CharacterNames = { "Nanay", "classmate1", "classmate2", "pulubi" };

    // Every character Image in the scenes has the same box (6.04 x 5.22, wider than tall), but the drawings are
    // taller than wide (Perzifanie 410 x 503, the pulubi 447 x 534, Elijah 505 x 577, Nanay 493 x 536), and
    // "Preserve Aspect" was off, so each one was stretched sideways by a different amount (Perzifanie 1.42x,
    // pulubi 1.38x, Elijah 1.32x, Nanay 1.26x). On: each drawing keeps its real shape at the same height, centered
    // in its box (nothing moves). Set to false to go back to the old stretched look.
    private const bool KeepTrueProportions = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        try
        {
            Apply(scene);
        }
        catch (System.Exception exception)
        {
            // Decoration must never stop a scene from playing.
            Debug.LogException(exception);
        }
    }

    private static void Apply(Scene scene)
    {
        AddBreathing(scene);

        if (!Moods.TryGetValue(scene.name, out Mood mood))
            return;

        RectTransform canvas = PolishUI.FindSceneCanvas(scene);
        if (canvas == null)
            return;

        // Each one is put first in the Canvas (just above the background sprite, behind every other UI element).
        // The vignette goes in last, so it ends up first: the specks are drawn over it.
        if (mood.specks > 0)
            AddSpecks(canvas, mood);
        AddVignette(canvas, mood.light);

        if (!string.IsNullOrEmpty(mood.when) || !string.IsNullOrEmpty(mood.place))
            SceneCaption.Create(canvas, mood.when ?? "", mood.place ?? "", PolishUI.FindInScene(scene, "Kalat_progress"));
    }

    // ------------------------------------------------------------------ vignette

    private static void AddVignette(RectTransform canvas, Light light)
    {
        Image vignette = PolishUI.CreateImage("Atmosphere_Vignette", canvas, PolishUI.Vignette, VignetteColor(light));
        // A little bigger than the screen; it never moves, but this keeps the corners soft on any shape of screen.
        PolishUI.Stretch(vignette.rectTransform, 30f);
        vignette.transform.SetAsFirstSibling();
    }

    // The color of the edges, and how strong they are (alpha).
    private static Color VignetteColor(Light light)
    {
        switch (light)
        {
            case Light.Morning: return new Color(0.16f, 0.09f, 0.03f, 0.30f);
            case Light.Day: return new Color(0.08f, 0.07f, 0.05f, 0.24f);
            case Light.Afternoon: return new Color(0.24f, 0.11f, 0.02f, 0.32f);
            case Light.Evening: return new Color(0.04f, 0.04f, 0.14f, 0.42f);
            case Light.Night: return new Color(0.02f, 0.03f, 0.12f, 0.5f);
            case Light.Gloomy: return new Color(0.07f, 0.07f, 0.1f, 0.36f);
            case Light.Heaven: return new Color(1f, 0.93f, 0.74f, 0.26f);
            default: return new Color(0f, 0f, 0f, 0.25f);
        }
    }

    // ------------------------------------------------------------------ floating specks

    private static void AddSpecks(RectTransform canvas, Mood mood)
    {
        RectTransform area = PolishUI.CreateRect("Atmosphere_Specks", canvas);
        PolishUI.Stretch(area);
        area.SetAsFirstSibling();

        AmbientParticles particles = area.gameObject.AddComponent<AmbientParticles>();
        bool heaven = mood.light == Light.Heaven;
        Color tint = heaven ? new Color(1f, 0.9f, 0.62f, 1f)
            : mood.light == Light.Afternoon ? new Color(1f, 0.86f, 0.62f, 1f)
            : mood.light == Light.Gloomy ? new Color(0.9f, 0.9f, 0.92f, 1f)
            : new Color(1f, 0.96f, 0.86f, 1f);
        particles.Setup(heaven ? AmbientParticles.Kind.Light : AmbientParticles.Kind.Dust, mood.specks, tint);
    }

    // ------------------------------------------------------------------ characters: true shape and breathing

    private static void AddBreathing(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                if (System.Array.IndexOf(CharacterNames, image.gameObject.name) < 0)
                    continue;

                // Only the drawing inside the box changes shape; the box, its place and its scale stay the same,
                // so walking in and out, spacing, jitter and tints work exactly as before.
                if (KeepTrueProportions)
                    image.preserveAspect = true;

                if (image.GetComponent<CharacterBreath>() != null)
                    continue;

                CharacterBreath breath = image.gameObject.AddComponent<CharacterBreath>();
                breath.Setup(Random.Range(3.3f, 4.2f), 0.008f);
            }
        }
    }
}
