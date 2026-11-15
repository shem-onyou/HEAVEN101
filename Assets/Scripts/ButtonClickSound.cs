using UnityEngine;
using UnityEngine.UI;

// The click sound of the menu buttons (Main Menu, Acts, and the pause menu).
//
// The sound is Assets/Resources/Sounds/clickedMenu_button.mp3 and is loaded by itself: nothing needs to
// be added to any scene. It plays from its own object that is kept between scenes, so a button that
// changes the scene (Start, Back, an Act, Menu) does not cut it off, and it still plays while the game
// is paused (the pause menu pauses every other sound).
public static class ButtonClickSound
{
    private const string ClipPath = "Sounds/clickedMenu_button";

    private static AudioSource source;
    private static AudioClip clip;
    private static bool triedLoading;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        source = null;
        clip = null;
        triedLoading = false;
    }

    // How long the click lasts, in seconds (0 if the sound is missing).
    public static float Length => LoadClip() != null ? clip.length : 0f;

    public static void Play()
    {
        AudioSource player = GetSource();
        if (player != null && player.clip != null)
            player.PlayOneShot(player.clip);
    }

    // Makes a button click when pressed. Calling it again on the same button does not add a second click.
    public static void AddTo(Button button)
    {
        if (button == null)
            return;

        Preload();
        button.onClick.RemoveListener(Play);
        button.onClick.AddListener(Play);
    }

    // Loads the sound ahead of time, so the very first click is not late.
    public static void Preload()
    {
        GetSource();
    }

    private static AudioClip LoadClip()
    {
        if (clip == null && !triedLoading)
        {
            triedLoading = true;
            clip = Resources.Load<AudioClip>(ClipPath);

            if (clip == null)
                Debug.LogWarning($"ButtonClickSound: no sound found at Assets/Resources/{ClipPath}.mp3");
            else
                clip.LoadAudioData();
        }

        return clip;
    }

    private static AudioSource GetSource()
    {
        if (source != null)
            return source;

        AudioClip loadedClip = LoadClip();
        if (loadedClip == null)
            return null;

        // Made inactive first, so nothing can play before it is set up.
        GameObject soundObject = new GameObject("ButtonClickSound");
        soundObject.SetActive(false);

        // Hidden on purpose: the act scenes find their own click sound by searching every AudioSource for
        // the word "click". If they found this one instead, the scene's own click (e.g. "next_click", which
        // plays on awake) would never be stopped and would sound as soon as the scene starts.
        // Hidden objects (HideFlags.DontSave) are skipped by FindObjectsByType, so those searches never see it.
        soundObject.hideFlags = HideFlags.HideAndDontSave;
        Object.DontDestroyOnLoad(soundObject);

        source = soundObject.AddComponent<AudioSource>();
        source.hideFlags = HideFlags.HideAndDontSave;
        source.playOnAwake = false;
        source.clip = loadedClip;
        source.loop = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        soundObject.SetActive(true);

        Application.quitting -= DestroySource;
        Application.quitting += DestroySource;
        return source;
    }

    // Hidden objects are not cleaned up by Unity on their own (in the Editor they would stay after Play mode).
    private static void DestroySource()
    {
        if (source != null)
            Object.Destroy(source.gameObject);

        source = null;
    }
}
