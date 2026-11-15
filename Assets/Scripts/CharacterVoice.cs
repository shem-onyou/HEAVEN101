using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// The "talking" sound (Assets/Resources/Sounds/Char_Speaking.mp3) that plays while a character's line
// types out. Nothing needs to be added to any scene.
//
// Every dialogue script calls Speak() for each letter it shows. The sound starts with the speaker's pitch,
// keeps going while letters keep coming, and fades out by itself shortly after the typing stops
// (the line finished, was skipped, or was replaced). It pauses with the pause menu like every other sound.
//
// The player ("You"), God/Lord, and text messages ("Elijah (text)") have no talking sound.
// Only letters and numbers make a character talk, so a line of only "..." stays silent.
public static class CharacterVoice
{
    private const string ClipPath = "Sounds/Char_Speaking";

    // How loud the talking is, from 0 to 1.
    private const float Volume = 0.6f;

    // Pitch of each voice. 1 = the sound as it is; higher = a higher voice.
    private const float StandardPitch = 1f;   // Elijah, the pulubi ("???" too), and anyone not listed below
    private const float WomanPitch = 1.2f;    // Nanay, Ma'am, Aling Rosa
    private const float GirlPitch = 1.4f;     // Perzifanie (classmate 2)

    // Act 3: Nanay is sick (coughing, fever, still weak the morning after), so her voice is lower and weaker.
    private const float SickNanayPitch = 1.05f;
    private const float SickNanayVolume = 0.4f;

    // The talking stops when no new letter has appeared for this long (or 3 letters' time, if slower).
    private const float MinSilenceBeforeStop = 0.15f;
    private const float FadeOutDuration = 0.08f;

    private static readonly string[] MenuScenes = { "Main Menu", "Acts" };

    private static VoicePlayer player;
    private static AudioClip clip;
    private static bool triedLoading;

    // The act of the current scene (3 = Nanay is sick), worked out once per scene instead of per letter.
    private static int currentAct;

    // The last speaker name and its lower-case form, so typing a line doesn't make a new string for every letter.
    private static string lastSpeakerName;
    private static string lastLowerName = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Initialize()
    {
        player = null;
        clip = null;
        triedLoading = false;
        currentAct = 0;
        lastSpeakerName = null;
        lastLowerName = "";
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // A new scene never starts with someone still talking. In the story scenes the sound is
    // loaded right away, so the first line isn't late.
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (player != null)
            player.StopNow();

        currentAct = PlayerDatabase.ActOfScene(scene.name);

        if (System.Array.IndexOf(MenuScenes, scene.name) < 0)
            GetPlayer();
    }

    // Called for each letter typed. speakerName is the name shown in the dialogue box, message is the
    // dialogue text, and visibleCharacters is how many of its characters are showing now.
    public static void Speak(string speakerName, TMP_Text message, int visibleCharacters)
    {
        if (speakerName != lastSpeakerName)
        {
            lastSpeakerName = speakerName;
            lastLowerName = string.IsNullOrEmpty(speakerName) ? "" : speakerName.Trim().ToLowerInvariant();
        }

        string lowerName = lastLowerName;

        if (!HasVoice(lowerName))
        {
            // e.g. the player answers right after Nanay: her voice must not keep going.
            Stop();
            return;
        }

        // Dots, spaces and other marks don't make a sound by themselves.
        if (!IsSpokenCharacter(message, visibleCharacters - 1))
            return;

        bool sick = IsSickNanay(lowerName);
        VoicePlayer voice = GetPlayer();
        if (voice != null)
            voice.Speak(lowerName, sick ? SickNanayPitch : PitchFor(lowerName), sick ? SickNanayVolume : Volume);
    }

    // Nanay is sick all through Act 3 (the scenes named "A3_...").
    private static bool IsSickNanay(string lowerName)
    {
        return currentAct == 3 && lowerName.Contains("nanay");
    }

    // Quickly fades the talking out (used when a line is skipped).
    public static void Stop()
    {
        if (player != null)
            player.FadeOut();
    }

    private static bool HasVoice(string lowerName)
    {
        if (lowerName.Length == 0 || lowerName.Contains("(text)"))
            return false;

        return lowerName != "you" && lowerName != "god" && lowerName != "lord";
    }

    // The character that just appeared is a letter or a number (rich-text tags are not counted).
    private static bool IsSpokenCharacter(TMP_Text message, int index)
    {
        if (message == null || index < 0)
            return false;

        TMP_TextInfo info = message.textInfo;
        if (info == null || index >= info.characterCount || index >= info.characterInfo.Length)
            return false;

        return char.IsLetterOrDigit(info.characterInfo[index].character);
    }

    private static float PitchFor(string lowerName)
    {
        if (lowerName.Contains("perzifanie") || lowerName.Contains("classmate2") || lowerName.Contains("classmate 2"))
            return GirlPitch;

        if (lowerName.Contains("nanay") || lowerName.Contains("ma'am") || lowerName.Contains("ma’am") ||
            lowerName.Contains("maam") || lowerName.Contains("teacher") || lowerName.Contains("aling"))
            return WomanPitch;

        return StandardPitch;
    }

    private static AudioClip LoadClip()
    {
        if (clip == null && !triedLoading)
        {
            triedLoading = true;
            clip = Resources.Load<AudioClip>(ClipPath);

            if (clip == null)
                Debug.LogWarning($"CharacterVoice: no sound found at Assets/Resources/{ClipPath}.mp3");
            else
                clip.LoadAudioData();
        }

        return clip;
    }

    private static VoicePlayer GetPlayer()
    {
        if (player != null)
            return player;

        AudioClip loadedClip = LoadClip();
        if (loadedClip == null)
            return null;

        // Made inactive first, so nothing can play before it is set up.
        GameObject voiceObject = new GameObject("CharacterVoice");
        voiceObject.SetActive(false);

        // Hidden on purpose (same as ButtonClickSound): the scene scripts search every AudioSource for
        // keywords and stop or play what they find. Hidden objects are skipped by those searches.
        voiceObject.hideFlags = HideFlags.HideAndDontSave;
        Object.DontDestroyOnLoad(voiceObject);

        AudioSource source = voiceObject.AddComponent<AudioSource>();
        source.hideFlags = HideFlags.HideAndDontSave;
        source.playOnAwake = false;
        source.clip = loadedClip;
        source.loop = true;
        source.spatialBlend = 0f;

        player = voiceObject.AddComponent<VoicePlayer>();
        player.hideFlags = HideFlags.HideAndDontSave;
        player.source = source;
        voiceObject.SetActive(true);

        Application.quitting -= DestroyPlayer;
        Application.quitting += DestroyPlayer;
        return player;
    }

    // Hidden objects are not cleaned up by Unity on their own (in the Editor they would stay after Play mode).
    private static void DestroyPlayer()
    {
        if (player != null)
            Object.Destroy(player.gameObject);

        player = null;
    }

    private class VoicePlayer : MonoBehaviour
    {
        public AudioSource source;

        private string currentSpeaker;
        private bool speaking;
        private bool fading;
        private float lastLetterTime;
        private float letterInterval = 0.03f;
        private float volume = Volume;

        public void Speak(string speaker, float pitch, float voiceVolume)
        {
            float now = Time.time;

            if (speaking && speaker == currentSpeaker)
            {
                float interval = now - lastLetterTime;
                if (interval > 0f)
                    letterInterval = interval;

                lastLetterTime = now;
                return;
            }

            // A new speaker (or the talking had stopped): start from a random spot so lines don't all sound the same.
            currentSpeaker = speaker;
            speaking = true;
            fading = false;
            lastLetterTime = now;
            letterInterval = 0.03f;

            volume = voiceVolume;
            source.pitch = pitch;
            source.volume = volume;
            source.time = Random.Range(0f, Mathf.Max(0f, source.clip.length - 1f));
            source.Play();
        }

        public void FadeOut()
        {
            if (!speaking)
                return;

            speaking = false;
            fading = true;
        }

        public void StopNow()
        {
            speaking = false;
            fading = false;
            source.Stop();
            source.volume = volume;
        }

        private void Update()
        {
            // Game time stops while paused, so a paused line does not count as silence.
            if (speaking && Time.time - lastLetterTime > Mathf.Max(MinSilenceBeforeStop, letterInterval * 3f))
                FadeOut();

            if (!fading)
                return;

            source.volume -= volume * Time.deltaTime / FadeOutDuration;
            if (source.volume <= 0f)
                StopNow();
        }
    }
}
