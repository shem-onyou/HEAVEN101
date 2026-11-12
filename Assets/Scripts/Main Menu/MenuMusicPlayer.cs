using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// The Main Menu music, kept alive across the menu screens (Main Menu and Acts) so it never cuts
// when the player presses Start or Back. It fades out and removes itself once an act begins.
// Created by MainMenu; nothing needs to be attached to any object.
//
// What plays: the opening sound once (fading out at 7 seconds), then the angelic voices take turns
// (1, 2, 1, 2...), only the first 10 seconds of each, crossfading so it sounds like one piece.
public class MenuMusicPlayer : MonoBehaviour
{
    public static MenuMusicPlayer Instance { get; private set; }

    [Tooltip("Scenes where the menu music keeps playing. Any other scene (an act) fades it out.")]
    public string[] menuSceneNames = { "Main Menu", "Acts" };
    public bool waitForOpeningToFinish = true;
    public float openingFadeStartTime = 7f;
    public float openingFadeOutDuration = 3f;
    public float firstVoiceFadeInDuration = 4f;
    public float voiceLength = 10f;
    public float crossfadeDuration = 2.5f;
    [Tooltip("How long the music takes to fade away when an act starts.")]
    public float leaveFadeDuration = 1.5f;

    private AudioSource openingPlayer;
    private float openingVolume = 1f;
    private AudioSource[] voicePlayers;
    private readonly List<AudioClip> voiceClips = new List<AudioClip>();
    private readonly List<float> voiceVolumes = new List<float>();
    private bool leaving;

    public static MenuMusicPlayer Create()
    {
        GameObject musicObject = new GameObject("MenuMusic");
        DontDestroyOnLoad(musicObject);
        Instance = musicObject.AddComponent<MenuMusicPlayer>();
        return Instance;
    }

    // Copies the clips and volumes from the Main Menu's AudioSources (those stay silent),
    // so the music lives on this object and survives scene changes.
    public void Play(AudioSource opening, List<AudioSource> voices)
    {
        if (opening != null && opening.clip != null)
        {
            openingPlayer = CreatePlayer(opening);
            openingPlayer.clip = opening.clip;
            openingVolume = opening.volume;
        }

        foreach (AudioSource voice in voices)
        {
            if (voice == null || voice.clip == null)
                continue;

            voiceClips.Add(voice.clip);
            voiceVolumes.Add(voice.volume);
        }

        if (voiceClips.Count > 0)
            voicePlayers = new[] { CreatePlayer(voices[0]), CreatePlayer(voices[0]) };

        StartCoroutine(PlayMusic());
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // Still in the menus: keep playing. An act started: fade out and go away.
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (leaving || System.Array.IndexOf(menuSceneNames, scene.name) >= 0)
            return;

        leaving = true;
        StopAllCoroutines();
        StartCoroutine(FadeOutAndDestroy());
    }

    private IEnumerator FadeOutAndDestroy()
    {
        foreach (AudioSource player in GetComponents<AudioSource>())
        {
            if (player.isPlaying)
                StartCoroutine(FadeVolume(player, 0f, leaveFadeDuration, stopAtEnd: true));
        }

        yield return new WaitForSecondsRealtime(leaveFadeDuration + 0.1f);
        Destroy(gameObject);
    }

    private IEnumerator PlayMusic()
    {
        if (openingPlayer != null)
        {
            openingPlayer.volume = openingVolume;
            openingPlayer.Play();

            // At 7 seconds (by default) the opening starts to fade away...
            if (waitForOpeningToFinish && voiceClips.Count > 0)
            {
                float openingLength = openingPlayer.clip.length;
                float fadeStart = openingFadeStartTime > 0f
                    ? Mathf.Min(openingFadeStartTime, openingLength)
                    : Mathf.Max(0f, openingLength - openingFadeOutDuration);
                yield return WaitUntilTime(openingPlayer, fadeStart);
            }
        }

        if (voiceClips.Count == 0)
            yield break;

        int playerIndex = 0;
        int voiceIndex = 0;

        // ...while the first angelic voice slowly grows louder underneath it.
        AudioSource current = voicePlayers[playerIndex];
        StartVoice(current, voiceIndex, firstVoiceFadeInDuration);
        if (openingPlayer != null && openingPlayer.isPlaying)
            StartCoroutine(FadeVolume(openingPlayer, 0f, openingFadeOutDuration, stopAtEnd: true));

        // Two players take turns, so one voice fades out while the next fades in.
        while (true)
        {
            float length = SegmentLength(current.clip);
            yield return WaitUntilTime(current, length - CrossfadeFor(length));

            AudioSource previous = current;
            playerIndex = 1 - playerIndex;
            voiceIndex = (voiceIndex + 1) % voiceClips.Count;
            current = voicePlayers[playerIndex];

            StartVoice(current, voiceIndex, -1f);
            StartCoroutine(FadeVolume(previous, 0f, CrossfadeFor(length), stopAtEnd: true));
        }
    }

    // An AudioSource on this object that copies the scene sound's settings (mixer, 2D/3D, priority).
    private AudioSource CreatePlayer(AudioSource template)
    {
        AudioSource player = gameObject.AddComponent<AudioSource>();
        player.playOnAwake = false;
        player.loop = false;
        player.outputAudioMixerGroup = template.outputAudioMixerGroup;
        player.spatialBlend = template.spatialBlend;
        player.priority = template.priority;
        player.pitch = template.pitch;
        player.volume = 0f;
        return player;
    }

    // Starts a voice from the beginning at silence and fades it up to its volume.
    // fadeInDuration < 0 uses the normal crossfade length.
    private void StartVoice(AudioSource player, int voiceIndex, float fadeInDuration)
    {
        AudioClip clip = voiceClips[voiceIndex];
        float duration = fadeInDuration >= 0f ? fadeInDuration : CrossfadeFor(SegmentLength(clip));

        player.clip = clip;
        player.time = 0f;
        player.volume = 0f;
        player.Play();
        StartCoroutine(FadeVolume(player, voiceVolumes[voiceIndex], duration, stopAtEnd: false));
    }

    private float SegmentLength(AudioClip clip)
    {
        return voiceLength > 0f ? Mathf.Min(voiceLength, clip.length) : clip.length;
    }

    // The crossfade never takes more than half of a voice.
    private float CrossfadeFor(float length)
    {
        return Mathf.Clamp(crossfadeDuration, 0f, length * 0.5f);
    }

    private static IEnumerator FadeVolume(AudioSource audioSource, float targetVolume, float duration, bool stopAtEnd)
    {
        float startVolume = audioSource.volume;
        float elapsed = 0f;
        while (elapsed < duration && audioSource != null)
        {
            // Paused with the app (phone in the background): the fade waits too.
            if (Application.isFocused)
                elapsed += Time.unscaledDeltaTime;

            audioSource.volume = Mathf.Lerp(startVolume, targetVolume, Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(duration, 0.01f)));
            yield return null;
        }

        if (audioSource == null)
            yield break;

        audioSource.volume = targetVolume;
        if (stopAtEnd)
            audioSource.Stop();
    }

    // Waits until the sound reaches the given second (or ends). It follows the sound itself,
    // so a phone going to the background doesn't make it skip ahead.
    private static IEnumerator WaitUntilTime(AudioSource audioSource, float seconds)
    {
        yield return null;
        while (audioSource != null && audioSource.time < seconds && (audioSource.isPlaying || !Application.isFocused))
            yield return null;
    }
}
