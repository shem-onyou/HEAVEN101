using System.Collections.Generic;
using UnityEngine;

// Soft interface sounds made by code (no audio files needed): the chime of a story notification,
// the low swell under an act title, and the little ticks of the results screen.
// They pause with the pause menu like every other sound.
//
// To use real recordings instead, put a file in Assets/Resources/Sounds with one of these names and it is
// played instead of the made-up sound: "ui_remember", "ui_title", "ui_tick".
public static class UISounds
{
    private const int SampleRate = 44100;

    private static GameObject soundObject;
    private static readonly Dictionary<string, AudioSource> sources = new Dictionary<string, AudioSource>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        soundObject = null;
        sources.Clear();
    }

    // "Nanay will remember this.": two soft bell notes.
    public static void Remember()
    {
        Play("ui_remember", () => Bell("ui_remember", 1.4f, 0.2f,
            new[] { new Note(784f, 0f, 1f), new Note(1174.7f, 0.12f, 0.85f) }), 1f, 0.55f);
    }

    // Under an act title or the "Previously" recap: a slow, warm swell.
    public static void TitleSwell()
    {
        Play("ui_title", () => Pad("ui_title", 3.2f), 1f, 0.5f);
    }

    // Results screen, one per line: higher for good points, lower for bad ones.
    public static void Tick(int points)
    {
        float pitch = points > 0 ? 1.5f : points < 0 ? 0.75f : 1f;
        Play("ui_tick", () => Bell("ui_tick", 0.25f, 0.05f, new[] { new Note(880f, 0f, 1f) }), pitch, 0.35f);
    }

    private static void Play(string soundName, System.Func<AudioClip> makeClip, float pitch, float volume)
    {
        AudioSource source = GetSource(soundName, makeClip);
        if (source == null)
            return;

        source.pitch = pitch;
        source.volume = volume;
        source.PlayOneShot(source.clip);
    }

    private static AudioSource GetSource(string soundName, System.Func<AudioClip> makeClip)
    {
        if (sources.TryGetValue(soundName, out AudioSource existing) && existing != null)
            return existing;

        // A real recording with the same name wins over the made-up sound.
        AudioClip clip = Resources.Load<AudioClip>("Sounds/" + soundName);
        if (clip == null)
            clip = makeClip();
        if (clip == null)
            return null;

        if (soundObject == null)
        {
            // Hidden on purpose (same as SoundEffects): the scene scripts search every AudioSource for
            // keywords and stop or play what they find. Hidden objects are skipped by those searches.
            soundObject = new GameObject("UISounds");
            soundObject.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(soundObject);

            Application.quitting -= DestroyAll;
            Application.quitting += DestroyAll;
        }

        AudioSource source = soundObject.AddComponent<AudioSource>();
        source.hideFlags = HideFlags.HideAndDontSave;
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.clip = clip;

        sources[soundName] = source;
        return source;
    }

    private static void DestroyAll()
    {
        if (soundObject != null)
            Object.Destroy(soundObject);

        soundObject = null;
        sources.Clear();
    }

    // ------------------------------------------------------------------ making the sounds

    private struct Note
    {
        public float frequency;
        public float start;
        public float loudness;

        public Note(float frequency, float start, float loudness)
        {
            this.frequency = frequency;
            this.start = start;
            this.loudness = loudness;
        }
    }

    // Bell-like notes: a sine with a softer octave and a faint "metal" overtone, fading out.
    private static AudioClip Bell(string clipName, float length, float decay, Note[] notes)
    {
        int count = Mathf.CeilToInt(length * SampleRate);
        float[] samples = new float[count];

        foreach (Note note in notes)
        {
            int first = Mathf.FloorToInt(note.start * SampleRate);
            for (int i = first; i < count; i++)
            {
                float t = (i - first) / (float)SampleRate;
                float attack = Mathf.Clamp01(t / 0.006f);
                float envelope = attack * Mathf.Exp(-t / Mathf.Max(decay, 0.01f)) * note.loudness;
                float phase = 2f * Mathf.PI * note.frequency * t;
                samples[i] += envelope * (Mathf.Sin(phase) + 0.22f * Mathf.Sin(phase * 2f) + 0.06f * Mathf.Sin(phase * 2.76f));
            }
        }

        return MakeClip(clipName, samples, 0.32f);
    }

    // A soft chord that swells in and slowly fades: a low G with its fifth and octave, gently wavering.
    private static AudioClip Pad(string clipName, float length)
    {
        int count = Mathf.CeilToInt(length * SampleRate);
        float[] samples = new float[count];
        float[] frequencies = { 196f, 293.7f, 392f };
        float[] loudness = { 1f, 0.6f, 0.35f };

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            float swellIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.9f));
            float fadeOut = Mathf.Clamp01((length - t) / 1.8f);
            float envelope = swellIn * fadeOut * fadeOut;
            float wobble = 1f + 0.003f * Mathf.Sin(2f * Mathf.PI * 4.5f * t);

            float value = 0f;
            for (int n = 0; n < frequencies.Length; n++)
                value += loudness[n] * Mathf.Sin(2f * Mathf.PI * frequencies[n] * wobble * t);

            samples[i] = value * envelope;
        }

        return MakeClip(clipName, samples, 0.28f);
    }

    // Scales the samples so the loudest point is "peak", then makes the clip.
    private static AudioClip MakeClip(string clipName, float[] samples, float peak)
    {
        float loudest = 0f;
        foreach (float sample in samples)
            loudest = Mathf.Max(loudest, Mathf.Abs(sample));

        if (loudest > 0f)
        {
            float scale = peak / loudest;
            for (int i = 0; i < samples.Length; i++)
                samples[i] *= scale;
        }

        AudioClip clip = AudioClip.Create(clipName, samples.Length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
