using System.Collections.Generic;
using UnityEngine;

// Short sound effects that are not placed in any scene. They are loaded by file name from
// Assets/Resources/Sounds, e.g. SoundEffects.Play("Char_bump", 0.8f) plays Char_bump.mp3 a little lower.
// They pause with the pause menu like every other sound.
public static class SoundEffects
{
    private const string Folder = "Sounds/";

    private static GameObject soundObject;
    private static readonly Dictionary<string, AudioSource> sources = new Dictionary<string, AudioSource>();
    private static readonly HashSet<string> missing = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        soundObject = null;
        sources.Clear();
        missing.Clear();
    }

    // pitch: 1 = as recorded, below 1 = lower/heavier, above 1 = higher. volume: 0 to 1.
    public static void Play(string soundName, float pitch = 1f, float volume = 1f)
    {
        AudioSource source = GetSource(soundName);
        if (source == null)
            return;

        source.pitch = pitch;
        source.volume = volume;
        source.Play();
    }

    // Each sound has its own AudioSource, so one sound's pitch never changes another's.
    private static AudioSource GetSource(string soundName)
    {
        if (string.IsNullOrEmpty(soundName) || missing.Contains(soundName))
            return null;

        if (sources.TryGetValue(soundName, out AudioSource existing) && existing != null)
            return existing;

        AudioClip clip = Resources.Load<AudioClip>(Folder + soundName);
        if (clip == null)
        {
            missing.Add(soundName);
            Debug.LogWarning($"SoundEffects: no sound found at Assets/Resources/{Folder}{soundName}");
            return null;
        }

        clip.LoadAudioData();

        if (soundObject == null)
        {
            // Hidden on purpose (same as ButtonClickSound): the scene scripts search every AudioSource for
            // keywords and stop or play what they find. Hidden objects are skipped by those searches.
            soundObject = new GameObject("SoundEffects");
            soundObject.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(soundObject);

            Application.quitting -= DestroyAll;
            Application.quitting += DestroyAll;
        }

        // The clip is set after the AudioSource is added, so it has nothing to play on awake.
        AudioSource source = soundObject.AddComponent<AudioSource>();
        source.hideFlags = HideFlags.HideAndDontSave;
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.clip = clip;

        sources[soundName] = source;
        return source;
    }

    // Hidden objects are not cleaned up by Unity on their own (in the Editor they would stay after Play mode).
    private static void DestroyAll()
    {
        if (soundObject != null)
            Object.Destroy(soundObject);

        soundObject = null;
        sources.Clear();
    }
}
