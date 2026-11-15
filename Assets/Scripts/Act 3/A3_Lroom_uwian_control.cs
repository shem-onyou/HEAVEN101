using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

// Act 3, the night at home: the heart of the game.
// Nanay is sick with a high fever. The player does everything (the counter keeps going up), but she
// doesn't get better. Alone at night, the player prays, and builds the prayer one line at a time:
//   1. offering their own good deeds (taken from the save), which fade away;
//   2. bargaining ("I'll do a hundred more..."), which fades away too;
//   3. an outburst, and the Good Deeds counter breaks;
//   4. nothing left: a whispered "...please." The room sinks into darkness and the player falls
//      asleep from exhaustion. The real surrender ("Lord... I need You.") happens in A3_Heaven.
public class A3_Lroom_uwian_control : A3SceneBase
{
    public const string CareActionId = "act3_care_nanay";
    public const string BargainChoiceId = "act3_prayer_bargain";
    public const string OutburstChoiceId = "act3_prayer_outburst";
    public const string PrayerChoiceId = "act3_prayer";

    [Header("Characters")]
    [SerializeField] private string nanayName = "Nanay";
    [SerializeField] private string neighborName = "Aling Rosa";
    [SerializeField] private Color nanayPaleTint = new Color(0.88f, 0.86f, 1f, 1f);

    [Header("Room Lighting")]
    [SerializeField] private Color nightTint = new Color(0.55f, 0.55f, 0.72f, 1f);
    [SerializeField] private Color prayerColdTint = new Color(0.3f, 0.32f, 0.5f, 1f);
    [Tooltip("The room sinks almost to black as the player falls asleep.")]
    [SerializeField] private Color exhaustedTint = new Color(0.12f, 0.12f, 0.2f, 1f);

    [Header("The Prayer")]
    [Tooltip("Used when the player has fewer than two good deeds saved.")]
    [SerializeField] private string[] fallbackDeedPrayers = { "Lord... I tried my best.", "Lord... I helped everyone I could." };
    [SerializeField] private string bargainPrayer1 = "Lord, I'll do a hundred more good deeds. Just heal her.";
    [SerializeField] private string bargainButton1 = "\"I'll do a hundred more.\"";
    [SerializeField] private string bargainPrayer2 = "Lord, I'll never make a mistake again. Just please heal her.";
    [SerializeField] private string bargainButton2 = "\"I'll never fail again.\"";
    [SerializeField] private string outburstPrayer1 = "Isn't it ENOUGH?!";
    [SerializeField] private string outburstPrayer2 = "What more do You WANT from me?!";
    [Tooltip("The last whisper before falling asleep.")]
    [SerializeField] private string whisperPrayer = "...please.";
    [SerializeField] private float prayerFontSize = 34f;
    [SerializeField] private float prayerTypingSpeed = 0.07f;

    [Header("Night Sounds (Assets/Resources/Sounds, unless one is placed in the scene)")]
    [Tooltip("tik-tak_sound: the wall clock, from \"Hours later...\" until the player falls asleep.")]
    [SerializeField] private float clockVolume = 0.35f;
    [Tooltip("hearbeat: during the prayer.")]
    [SerializeField] private float heartbeatVolume = 0.5f;
    [Tooltip("boom_sound: the hit when the player cries out.")]
    [SerializeField] private float boomVolume = 0.9f;
    [Tooltip("The heartbeat slows down to this pitch as the player falls asleep.")]
    [SerializeField] private float sleepyHeartbeatPitch = 0.75f;

    // Renamed from nextSceneName: the scene had "A3_Broom_uwi" saved there, which is now A3_Heaven.
    [Header("Scene Transition")]
    [SerializeField] private string heavenSceneName = "A3_Heaven";

    [Header("Dialogue")]
    public List<A3Line> arriveLines = new List<A3Line>();
    public List<A3Line> nightLines = new List<A3Line>();

    private A3Character nanay;
    private AudioSource doorSound;
    private AudioSource heartbeatSound;
    private AudioSource clockSound;
    private AudioSource boomSound;

    protected override void SetupScene()
    {
        nanay = AddCharacter("Nanay", nanayName, trembles: true);
        if (nanay.image != null)
            nanay.baseColor = Multiply(nanay.baseColor, nanayPaleTint);

        // Copied from the morning living room: no kalat tonight.
        foreach (ClickableUIItem kalat in FindObjectsByType<ClickableUIItem>(FindObjectsInactive.Include))
            kalat.gameObject.SetActive(false);
        GameObject progress = FindSceneObject("Kalat_progress");
        if (progress != null) progress.SetActive(false);

        doorSound = FindSound("door");
        heartbeatSound = FindOrLoadSound("hearbeat", "beat");
        clockSound = FindOrLoadSound("tik-tak_sound", "tik");
        boomSound = FindOrLoadSound("boom_sound", "boom");
        if (boomSound != null) boomSound.volume = boomVolume;

        FillDefaultLines();
    }

    private void FillDefaultLines()
    {
        if (arriveLines.Count == 0)
        {
            arriveLines.Add(new A3Line(playerName, "NANAY!"));
            arriveLines.Add(new A3Line(nanayName, "A-anak... I'm okay... *cough* *cough*", "cough"));
            arriveLines.Add(new A3Line(neighborName, "Ay, salamat at nandito ka na. I found her on the floor."));
            arriveLines.Add(new A3Line(neighborName, "Her fever is very high. Give her medicine and let her rest. I'll come back later, ha?"));
            arriveLines.Add(new A3Line(playerName, "Nay... I'm here. I'll take care of everything.", "sfx:door"));
            arriveLines.Add(new A3Line(playerName, "(Medicine.)", "deed"));
            arriveLines.Add(new A3Line(playerName, "(A wet towel for her forehead.)", "deed"));
            arriveLines.Add(new A3Line(playerName, "(Lugaw... I'll cook her lugaw.)", "deed"));
            arriveLines.Add(new A3Line(playerName, "(Clean the house... fix her blanket... anything...)", "deed;deed"));
        }

        if (nightLines.Count == 0)
        {
            nightLines.Add(new A3Line(nanayName, "Anak... you've done so much... magpahinga ka na."));
            nightLines.Add(new A3Line(playerName, "Not yet, Nay. Not until you're better."));
            nightLines.Add(new A3Line(nanayName, "*cough* *cough*...", "cough"));
            nightLines.Add(new A3Line(playerName, "(Her fever isn't going down...)"));
            nightLines.Add(new A3Line(playerName, "(I did everything. Everything right. So why isn't it working...?)"));
            nightLines.Add(new A3Line(playerName, "(She's asleep... but she's still burning up.)", "pause:1.5"));
            nightLines.Add(new A3Line(playerName, "(...)"));
            nightLines.Add(new A3Line(playerName, "(Maybe... if I pray...)"));
        }
    }

    protected override IEnumerator RunScene()
    {
        // The front door bangs open in the dark: the player rushes in.
        ShowCharacter(nanay);
        yield return new WaitForSeconds(0.3f);
        PlayEffect(doorSound);
        StartLoop(ambientAudioSource, 0.8f, 1f);
        yield return FadeFromBlack(0.8f);

        yield return PlayLines(arriveLines);
        yield return HoursLater();
        yield return PlayLines(nightLines);
        yield return Prayer();

        yield return FadeOutAndLoad(heavenSceneName, "A3_Heaven", 3f);
    }

    // Fade to black, "Hours later...", and the room comes back at night.
    private IEnumerator HoursLater()
    {
        HideDialogueBox();
        if (nextButton != null) nextButton.SetActive(false);

        PlayerDatabase.RecordAction(CareActionId, "Took care of Nanay all night", 1f, 1);

        yield return FadeToBlack(1f);

        // Time passes: the wall clock ticks in the dark, and the house gets quieter for the night.
        StartLoop(clockSound, clockVolume, 1f);
        if (ambientAudioSource != null) StartCoroutine(FadeAudio(ambientAudioSource, OriginalVolume(ambientAudioSource) * 0.5f, 1.5f));
        yield return ShowCenterText("Hours later...", 36f, 1.5f);
        SetBackgroundTint(nightTint);
        yield return FadeOutCover(1.5f);
        yield return new WaitForSeconds(0.3f);
    }

    // ------------------------------------------------------------------ the prayer

    private IEnumerator Prayer()
    {
        HideDialogueBox();
        if (nextButton != null) nextButton.SetActive(false);

        // The room goes cold and dark; only the clock and the heartbeat are left.
        TintAllCharacters(new Color(0.35f, 0.35f, 0.45f, 1f));
        StartCoroutine(TintBackground(prayerColdTint, 2.5f));
        if (ambientAudioSource != null) StartCoroutine(FadeAudio(ambientAudioSource, OriginalVolume(ambientAudioSource) * 0.3f, 2f));
        StartLoop(heartbeatSound, heartbeatVolume, 2f);
        yield return new WaitForSeconds(2f);

        // 1. Offering their own good deeds.
        List<string> deeds = PlayerDatabase.GetGoodDeedPrayers();
        string deed1 = deeds.Count > 0 ? deeds[0] : fallbackDeedPrayers[0];
        string deed2 = deeds.Count > 1 ? deeds[1] : fallbackDeedPrayers[Mathf.Min(1, fallbackDeedPrayers.Length - 1)];

        string offered = null;
        yield return BargainRound(ButtonLabelFor(deed1), deed1, ButtonLabelFor(deed2), deed2, chosen => offered = chosen);
        PlayerDatabase.RecordChoice(BargainChoiceId, "Alone at night, you offered God your good deeds.", offered, 0);

        // 2. Bargaining.
        yield return BargainRound(bargainButton1, bargainPrayer1, bargainButton2, bargainPrayer2, null);

        // 3. The outburst. The counter breaks.
        int picked = -1;
        yield return WaitForChoice(outburstPrayer1, outburstPrayer2, choice => picked = choice);
        string outburst = picked == 0 ? outburstPrayer1 : outburstPrayer2;
        PlayerDatabase.RecordChoice(OutburstChoiceId, "When nothing worked, you cried out to God.", outburst, 0, picked == 0 ? "enough" : "what_more");
        yield return Outburst(outburst);

        // 4. Nothing left but a whisper.
        yield return WaitForChoice(whisperPrayer, null, null);
        PlayerDatabase.RecordChoice(PrayerChoiceId, "Your prayer that night ended with:", whisperPrayer, 0, "whisper");
        yield return Exhausted();
    }

    private static string ButtonLabelFor(string prayer)
    {
        const string prefix = "Lord... ";
        return prayer.StartsWith(prefix) ? $"\"{prayer.Substring(prefix.Length)}\"" : $"\"{prayer}\"";
    }

    // The chosen line appears in the dark... nothing answers, Nanay coughs, and the line fades away.
    private IEnumerator BargainRound(string button1, string prayer1, string button2, string prayer2, System.Action<string> onChosen)
    {
        int picked = -1;
        yield return WaitForChoice(button1, button2, choice => picked = choice);
        string prayer = picked == 0 ? prayer1 : prayer2;
        onChosen?.Invoke(prayer);

        TMP_Text line = CreatePrayerText(prayer, 1);
        yield return TypeOnScreen(line, prayerTypingSpeed);
        yield return new WaitForSeconds(1.2f);

        PlayEffect(FindOrLoadSound("cough", "cough"));
        StartCoroutine(CoughShake(nanay, 2, 4f));
        StartCoroutine(FlickerCounter());
        yield return new WaitForSeconds(0.8f);

        yield return Dissolve(line, 1.5f);
        yield return new WaitForSeconds(0.8f);
    }

    private IEnumerator Outburst(string outburst)
    {
        TMP_Text line = CreatePrayerText(outburst, 1);
        line.fontSize = prayerFontSize * 1.3f;
        line.fontStyle = FontStyles.Bold;
        line.alpha = 1f;
        line.maxVisibleCharacters = 99999;

        // The cry hits like a blow: a deep boom with the shake.
        PlayEffect(boomSound);
        StartCoroutine(ShakeScreen(16f, 0.7f));
        yield return BreakCounter();
        yield return Dissolve(line, 1.2f);

        // Silence.
        if (heartbeatSound != null) StartCoroutine(FadeAudio(heartbeatSound, OriginalVolume(heartbeatSound) * 0.25f, 2f));
        if (ambientAudioSource != null) StartCoroutine(FadeAudio(ambientAudioSource, 0f, 2f));
        yield return new WaitForSeconds(3f);
    }

    // The whisper types out slowly and small. The heartbeat fades, the room sinks into darkness,
    // and the player falls asleep beside Nanay with nothing left to give.
    private IEnumerator Exhausted()
    {
        TMP_Text whisper = CreatePrayerText(whisperPrayer, 1);
        whisper.fontSize = prayerFontSize * 0.8f;
        whisper.color = new Color(0.85f, 0.85f, 0.9f, 1f);
        yield return TypeOnScreen(whisper, prayerTypingSpeed * 2f);
        yield return new WaitForSeconds(2f);

        // Falling asleep: the heartbeat slows and fades, and the clock fades away with it.
        if (heartbeatSound != null)
        {
            StartCoroutine(SlidePitch(heartbeatSound, sleepyHeartbeatPitch, 4f));
            StartCoroutine(FadeAudio(heartbeatSound, 0f, 4f));
        }
        if (clockSound != null) StartCoroutine(FadeAudio(clockSound, 0f, 5f));
        TintAllCharacters(new Color(0.15f, 0.15f, 0.22f, 1f));
        StartCoroutine(FadeTextAlpha(whisper, whisper.alpha, 0f, 3f));
        yield return TintBackground(exhaustedTint, 4f);
        yield return new WaitForSeconds(1.5f);
    }

    // Slowly changes how fast (and how low) a sound plays, e.g. a heartbeat slowing down.
    private static IEnumerator SlidePitch(AudioSource audioSource, float toPitch, float duration)
    {
        float fromPitch = audioSource.pitch;
        float elapsed = 0f;
        while (elapsed < duration && audioSource != null)
        {
            elapsed += Time.deltaTime;
            audioSource.pitch = Mathf.Lerp(fromPitch, toPitch, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
    }

    // Rows go from top (0) to bottom (2) in the upper half of the screen.
    private TMP_Text CreatePrayerText(string prayer, int row)
    {
        float top = 0.9f - row * 0.12f;
        TMP_Text text = CreateText("PrayerLine", prayer, prayerFontSize, new Vector2(0.06f, top - 0.12f), new Vector2(0.94f, top));
        text.fontStyle = FontStyles.Italic;
        text.outlineWidth = 0.2f;
        text.outlineColor = Color.black;
        text.alpha = 1f;
        text.maxVisibleCharacters = 0;
        return text;
    }

    private static IEnumerator TypeOnScreen(TMP_Text text, float speed)
    {
        text.ForceMeshUpdate();
        int total = text.textInfo.characterCount;
        for (int i = 0; i <= total; i++)
        {
            text.maxVisibleCharacters = i;
            yield return new WaitForSeconds(speed);
        }
    }

    // The letters drift apart and fade, like the words didn't reach anyone.
    private static IEnumerator Dissolve(TMP_Text text, float duration)
    {
        float startAlpha = text.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            text.alpha = Mathf.Lerp(startAlpha, 0f, progress);
            text.characterSpacing = progress * 30f;
            yield return null;
        }

        Destroy(text.gameObject);
    }
}
