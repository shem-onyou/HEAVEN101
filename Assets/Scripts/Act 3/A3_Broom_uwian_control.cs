using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// The last scene of the game: A3_Heaven, at the gates of Heaven.
//  1. The player begs God to save Nanay, and defends themself: "I did everything right!"
//  2. God gently asks: "Did you, child?" and remembers the player's real choices from the save
//     (the lie, the copied answers, the man they walked past, the broken promise...). Only He speaks.
//     Even a "perfect" player is asked why they did good, and if it was ever enough.
//  3. Crushed and confused, the player can only say: "Lord... I need You.
//     I can't do everything without You."
//  4. God: "That is all I ever wanted to hear... Your Nanay is in My hands."
//  5. "Wake up, child. And this time... rest." Morning in the dark.
//  6. Ephesians 2:8-9, the credits, then every good deed of the game appears and is crossed out:
//     "You are not saved by your good works, but by grace, through faith."
// No points anywhere: the ending is the same for every player.
public class A3_Broom_uwian_control : A3SceneBase
{
    public const string PleaChoiceId = "act3_heaven_plea";
    public const string ConfessionChoiceId = "act3_heaven_confession";

    [Header("God (auto-found by name \"Lord\")")]
    [SerializeField] private string lordName = "God";
    [SerializeField] private float lordAppearDuration = 2.5f;
    [SerializeField] private float lordRiseDistance = 30f;
    [SerializeField] private float lordTypingSpeed = 0.05f;
    [Tooltip("God's tint while the player is speaking. Kept light so God never looks too dark.")]
    [SerializeField] private Color lordListeningTint = new Color(0.75f, 0.75f, 0.75f, 1f);

    [Header("Choice: the plea")]
    [SerializeField] private string pleaSaveChoiceText = "\"Lord, please save my Nanay!\"";
    [SerializeField] private string pleaBestChoiceText = "\"Lord, I did my best! Isn't that enough?\"";

    [Header("The confession (the player presses each line)")]
    [SerializeField] private string[] confessionLines = { "Lord... I need You.", "I can't do everything without You." };

    [Header("Remembering")]
    [Tooltip("How many of the player's past choices God brings up.")]
    [SerializeField] private int memoriesToShow = 3;
    [Tooltip("Each memory dims the light a little more.")]
    [SerializeField] private Color memoryDimTint = new Color(0.82f, 0.82f, 0.88f, 1f);

    [Header("Ending")]
    [SerializeField] private string[] verseLines =
    {
        "\"For by grace you have been saved through faith,",
        "and that not of yourselves;",
        "it is the gift of God,",
        "not of works, that no one would boast.\""
    };
    [SerializeField] private string verseReference = "Ephesians 2:8-9";

    [Header("Credits")]
    [SerializeField] private string theEndText = "THE END";
    [SerializeField] private string createdByText = "Created by";
    [SerializeField] private string creatorName = "Shem De Leon";

    [Header("Final Summary (every good deed, crossed out)")]
    [SerializeField] private string summaryTitle = "Your Good Deeds";
    // Renamed from summaryFinalLine: the scene had the old "Saved by grace, through faith." saved there,
    // so a new field drops that value and uses this clearer message instead.
    [Tooltip("Shown in gold under the crossed-out deeds. Press Enter in the box for a new line.")]
    [TextArea(2, 4)]
    [SerializeField] private string closingMessage = "You are not saved by your good works,\nbut by grace, through faith.";
    [SerializeField] private float summaryLineDelay = 0.25f;
    [SerializeField] private float strikeDelay = 0.35f;

    [SerializeField] private string mainMenuSceneName = "Main Menu";
    [SerializeField] private int actNumber = 3;

    [Header("Dialogue")]
    public List<A3Line> arrivalLines = new List<A3Line>();
    public List<A3Line> pleaSaveLines = new List<A3Line>();
    public List<A3Line> pleaBestLines = new List<A3Line>();
    [Tooltip("Shared end of the plea, then God's question.")]
    public List<A3Line> didYouLines = new List<A3Line>();
    [Tooltip("After the memories: why the player did good at all.")]
    public List<A3Line> motiveLines = new List<A3Line>();
    public List<A3Line> crushedLines = new List<A3Line>();
    [Tooltip("God's answer after \"I need You\".")]
    public List<A3Line> answerLines = new List<A3Line>();
    public List<A3Line> wakeLines = new List<A3Line>();
    [Tooltip("Played on a dark screen after waking up: Nanay's voice and Elijah's text.")]
    public List<A3Line> morningLines = new List<A3Line>();

    private GameObject lord;
    private SpriteRenderer lordSprite;
    private Graphic lordGraphic;
    private Vector3 lordRestPosition;
    private Vector3 lordRestScale = Vector3.one;
    private Color lordBaseColor = Color.white;
    private Coroutine lordEffectCoroutine;
    private Color currentDim = Color.white;

    private AudioSource godlySound;
    private AudioSource teleportSound;
    private AudioSource birdsSound;
    private AudioSource heartbeatSound;
    private AudioSource peaceMusic;

    protected override bool CounterVisibleAtStart => false;

    // If the scene was copied from Act One, its old SceneController may still be attached.
    // It is switched off before it starts, so only this script runs the scene.
    private void Awake()
    {
        foreach (SceneController oldController in FindObjectsByType<SceneController>(FindObjectsInactive.Include))
            oldController.enabled = false;
    }

    protected override void SetupScene()
    {
        // The counter broke last night. It is not coming back.
        showGoodDeedsCounter = false;

        lord = FindSceneObject("Lord");
        if (lord != null)
        {
            lordSprite = lord.GetComponent<SpriteRenderer>();
            lordGraphic = lord.GetComponent<Graphic>();
            lordRestPosition = lord.transform.localPosition;
            lordRestScale = lord.transform.localScale;
            lordBaseColor = GetLordColor();
            lord.SetActive(false);
        }

        godlySound = FindSound("godly", "choir", "heaven");
        // A sound named "GodlyAmbient" is also found as the ambient; keep only one of them.
        if (ambientAudioSource == godlySound)
            ambientAudioSource = null;
        teleportSound = FindSound("teleport");
        birdsSound = FindSound("bird");
        heartbeatSound = FindOrLoadSound("hearbeat", "beat");
        peaceMusic = FindSound("piano", "peace");

        FillDefaultLines();
    }

    private void FillDefaultLines()
    {
        if (arrivalLines.Count == 0)
        {
            arrivalLines.Add(new A3Line(playerName, "(...This light.)"));
            arrivalLines.Add(new A3Line(playerName, "(The gates... I'm here again. Just like the very first dream.)"));
            arrivalLines.Add(new A3Line(lordName, "Child.", "lord"));
            arrivalLines.Add(new A3Line(playerName, "Lord...!", "heartbeat"));
        }

        if (pleaSaveLines.Count == 0)
        {
            pleaSaveLines.Add(new A3Line(playerName, "Lord, please save my Nanay! She's burning up, she can barely breathe..."));
        }

        if (pleaBestLines.Count == 0)
        {
            pleaBestLines.Add(new A3Line(playerName, "Lord, I did my best! Isn't that enough? My Nanay is so sick..."));
        }

        if (didYouLines.Count == 0)
        {
            didYouLines.Add(new A3Line(playerName, "I helped her. I helped my friends. I helped everyone I could!"));
            didYouLines.Add(new A3Line(playerName, "I did everything right!"));
            didYouLines.Add(new A3Line(lordName, "...", "pause:1"));
            didYouLines.Add(new A3Line(lordName, "Did you, child?"));
            didYouLines.Add(new A3Line(playerName, "...What?"));
        }

        if (motiveLines.Count == 0)
        {
            motiveLines.Add(new A3Line(lordName, "And the good things you did..."));
            motiveLines.Add(new A3Line(lordName, "You counted every one of them. Didn't you?"));
            motiveLines.Add(new A3Line(lordName, "Tell Me, child. Was it ever enough?"));
        }

        if (crushedLines.Count == 0)
        {
            crushedLines.Add(new A3Line(playerName, "...", "stopheart"));
            crushedLines.Add(new A3Line(playerName, "I... I thought if I did enough..."));
            crushedLines.Add(new A3Line(playerName, "If I just tried harder... if I didn't make any mistakes..."));
            crushedLines.Add(new A3Line(playerName, "Then... what am I supposed to do?"));
            crushedLines.Add(new A3Line(playerName, "(I can't... I can't do it. I never could.)", "pause:1.5"));
        }

        if (answerLines.Count == 0)
        {
            answerLines.Add(new A3Line(lordName, "...", "calm"));
            answerLines.Add(new A3Line(lordName, "That is all I ever wanted to hear."));
            answerLines.Add(new A3Line(playerName, "...Lord?"));
            answerLines.Add(new A3Line(lordName, "I heard you, child. Last night, and tonight."));
            answerLines.Add(new A3Line(lordName, "Your Nanay is in My hands. She always was."));
            answerLines.Add(new A3Line(lordName, "And so are you."));
            answerLines.Add(new A3Line(lordName, "You were never meant to carry everything alone."));
        }

        if (wakeLines.Count == 0)
        {
            wakeLines.Add(new A3Line(lordName, "Wake up, child."));
            wakeLines.Add(new A3Line(lordName, "And this time..."));
            wakeLines.Add(new A3Line(lordName, "...rest."));
        }

        if (morningLines.Count == 0)
        {
            morningLines.Add(new A3Line(playerName, "(...)"));
            morningLines.Add(new A3Line(playerName, "(...Birds. It's morning.)"));
            morningLines.Add(new A3Line("Nanay", "Good morning, anak."));
            morningLines.Add(new A3Line(playerName, "Nay...? Your fever—"));
            morningLines.Add(new A3Line("Nanay", "Bumaba na. I'm still weak, but I'm okay."));
            morningLines.Add(new A3Line("Nanay", "You stayed up with me the whole night, didn't you?"));
            morningLines.Add(new A3Line("Nanay", "Salamat, anak. But next time, rest too, ha? You don't have to carry everything alone."));
            morningLines.Add(new A3Line(playerName, "...Opo, Nay."));
            morningLines.Add(new A3Line("Elijah (text)", "pre kumusta si tita? we prayed for her kagabi", "buzz"));
            morningLines.Add(new A3Line(playerName, "(...)"));
            morningLines.Add(new A3Line(playerName, "(For the first time in a long time...)"));
            morningLines.Add(new A3Line(playerName, "(I'm not counting.)"));
        }
    }

    // ------------------------------------------------------------------ God remembers

    private class Memory
    {
        public string time;
        public bool wasWrong;
        public List<A3Line> lines;
    }

    // God brings up the player's real past choices. Wrong choices come first; if there are not
    // enough, the moments where they walked past someone in need are used.
    private List<A3Line> BuildMemoryLines()
    {
        List<Memory> memories = new List<Memory>();
        foreach (PlayerChoiceRecord choice in PlayerDatabase.Data.choices)
        {
            List<A3Line> lines = MemoryFor(choice.choiceId, choice.answerId);
            if (lines != null)
                memories.Add(new Memory { time = choice.recordedAt, wasWrong = choice.points < 0, lines = lines });
        }

        // Wrong choices first, then the oldest first, so it feels like going back through time.
        memories.Sort((a, b) =>
        {
            if (a.wasWrong != b.wasWrong) return a.wasWrong ? -1 : 1;
            return string.CompareOrdinal(a.time, b.time);
        });

        List<A3Line> result = new List<A3Line>();
        int shown = 0;
        foreach (Memory memory in memories)
        {
            if (shown >= memoriesToShow)
                break;

            // Each memory dims the light a little more.
            memory.lines[0].actions = string.IsNullOrEmpty(memory.lines[0].actions) ? "dim" : memory.lines[0].actions + ";dim";
            result.AddRange(memory.lines);
            shown++;
        }

        if (shown > 0)
            result.Insert(0, new A3Line(lordName, "Do you remember...?"));

        return result;
    }

    // What God says about one past choice. Null = nothing to bring up.
    private List<A3Line> MemoryFor(string choiceId, string answer)
    {
        switch (choiceId)
        {
            case "act1_cut_class" when answer == "cut":
                return Recall("The morning your friend asked you to skip class... you went with him.");
            case "act1_pulubi" when answer == "refuse":
                return Recall("The hungry man outside your school asked you for food. You walked past him.");
            case "act2_did_you_review" when answer == "lied":
                return Recall("When your friends asked if you had reviewed... you told them you did.");
            case "act2_let_copy" when answer == "let_copy":
                return Recall("During the quiz, you let your friend copy your answers.");
            case "act2_answer_sure" when answer == "insisted":
                return Recall("And when he asked if you were sure... you said yes. You weren't.");
            case "act2_lend_money" when answer == "kept":
                return Recall("Your friend was hungry that day. You kept your lunch money.");
            case "act2_pulubi_medicine" when answer == "kept_fare":
                return Recall("That man's child was sick. You kept your fare.");
            case "act2_pulubi_medicine" when answer == "hesitated":
                return Recall("That man's child was sick. You didn't even answer him.");
            case "act2_honest_nanay" when answer == "lied":
                return Recall("When your Nanay asked you what happened... you lied to her.");
            case "act2_promise_nanay" when answer == "went":
                return new List<A3Line>
                {
                    new A3Line(lordName, "You promised your Nanay you would stay home. Then you left."),
                    new A3Line(lordName, "Your friend needed you too. Both were good, child... and you could not keep both.")
                };
            case "act2_promise_nanay" when answer == "stayed":
                return new List<A3Line>
                {
                    new A3Line(lordName, "That night, your friend was alone outside his house. You stayed home."),
                    new A3Line(lordName, "You kept your word. Both were good, child... and you could not keep both.")
                };
        }

        return null;
    }

    // Only God speaks while He remembers; the player has no answer.
    private List<A3Line> Recall(string lordLine)
    {
        return new List<A3Line> { new A3Line(lordName, lordLine) };
    }

    // If the player once said they did good to enter Heaven, God reminds them.
    private List<A3Line> BuildMotiveLines()
    {
        List<A3Line> lines = new List<A3Line>(motiveLines);
        if (PlayerDatabase.GetChoice(A2_Broom_uwian.WhyGoodChoiceId) == A2_Broom_uwian.ForHeavenAnswer)
        {
            lines.Insert(Mathf.Min(2, lines.Count), new A3Line(lordName, "You told Me once you did them so you could enter here."));
        }

        return lines;
    }

    // ------------------------------------------------------------------ the scene

    protected override IEnumerator RunScene()
    {
        // Silence in the dark, then the light of the gates.
        yield return new WaitForSeconds(1.5f);
        StartLoop(godlySound, 1f, 3f);
        StartLoop(ambientAudioSource, 1f, 3f);
        yield return FadeFromBlack(3f);
        yield return new WaitForSeconds(0.5f);

        yield return PlayLines(arrivalLines);

        // 1. The plea: "I did everything right!"
        int plea = -1;
        yield return WaitForChoice(pleaSaveChoiceText, pleaBestChoiceText, choice => plea = choice);
        PlayerDatabase.RecordChoice(PleaChoiceId, "At the gates, you begged God for help.",
            plea == 0 ? pleaSaveChoiceText : pleaBestChoiceText, 0, plea == 0 ? "save_nanay" : "did_my_best");
        yield return PlayLines(plea == 0 ? pleaSaveLines : pleaBestLines);
        yield return PlayLines(didYouLines);

        // 2. God remembers.
        yield return PlayLines(BuildMemoryLines());
        yield return PlayLines(BuildMotiveLines());

        // 3. Crushed.
        yield return PlayLines(crushedLines);

        // 4. The player says it, one line at a time.
        foreach (string confession in confessionLines)
        {
            yield return WaitForChoice(confession, null, null);
            yield return PlayLines(new List<A3Line> { new A3Line(playerName, confession) });
        }

        PlayerDatabase.RecordChoice(ConfessionChoiceId, "At the gates, with nothing left to show, you told God:",
            string.Join(" ", confessionLines), 0, "need_you");

        yield return PlayLines(answerLines);

        // 5. Back home.
        yield return WhiteLight();
        yield return PlayLines(wakeLines);
        yield return WakeUpInTheDark();
        yield return PlayLines(morningLines);
        yield return Ending();
    }

    protected override IEnumerator PerformCustomAction(string action, string argument)
    {
        switch (action)
        {
            case "lord":
                yield return LordAppears();
                break;

            case "heartbeat":
                // The player's panic: the heart pounds and the light softens.
                StartLoop(heartbeatSound, 0.8f, 0.5f);
                if (godlySound != null) StartCoroutine(FadeAudio(godlySound, OriginalVolume(godlySound) * 0.4f, 0.8f));
                break;

            case "dim":
                // Each memory makes the light a little dimmer.
                currentDim = Multiply(currentDim, memoryDimTint);
                yield return TintBackground(currentDim, 0.8f);
                break;

            case "stopheart":
                // Silence. Nothing left to say.
                if (heartbeatSound != null) StartCoroutine(FadeAudio(heartbeatSound, 0f, 1.5f));
                if (godlySound != null) StartCoroutine(FadeAudio(godlySound, 0f, 1.5f));
                yield return new WaitForSeconds(1.5f);
                break;

            case "calm":
                // God answers, and the light comes back.
                currentDim = Color.white;
                if (godlySound != null) StartCoroutine(FadeAudio(godlySound, OriginalVolume(godlySound), 3f));
                StartLoop(peaceMusic, 1f, 4f);
                yield return TintBackground(Color.white, 2.5f);
                break;
        }
    }

    // ------------------------------------------------------------------ God

    private IEnumerator LordAppears()
    {
        HideDialogueBox();
        if (lord == null || lord.activeSelf)
            yield break;

        Transform lordTransform = lord.transform;
        Vector3 startPosition = lordRestPosition - new Vector3(0f, lordRiseDistance, 0f);
        Color startColor = lordBaseColor;
        startColor.a = 0f;

        lordTransform.localPosition = startPosition;
        SetLordColor(startColor);
        lord.SetActive(true);

        float duration = Mathf.Max(lordAppearDuration, 0.01f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            lordTransform.localPosition = Vector3.Lerp(startPosition, lordRestPosition, progress);
            SetLordColor(Color.Lerp(startColor, lordBaseColor, progress));
            yield return null;
        }

        lordTransform.localPosition = lordRestPosition;
        SetLordColor(lordBaseColor);
        yield return new WaitForSeconds(0.5f);
    }

    // Lord can be a SpriteRenderer (like in Act One) or a UI Image.
    private Color GetLordColor()
    {
        if (lordSprite != null) return lordSprite.color;
        if (lordGraphic != null) return lordGraphic.color;
        return Color.white;
    }

    private void SetLordColor(Color color)
    {
        if (lordSprite != null) lordSprite.color = color;
        if (lordGraphic != null) lordGraphic.color = color;
    }

    // God glows and pulses a little when He speaks, and dims softly while the player speaks.
    protected override void OnLineShown(A3Line line)
    {
        if (lord == null || !lord.activeInHierarchy)
            return;

        if (lordEffectCoroutine != null)
            StopCoroutine(lordEffectCoroutine);

        lordEffectCoroutine = StartCoroutine(LordSpeakEffect(line.speakerName == lordName));
    }

    private IEnumerator LordSpeakEffect(bool speaking)
    {
        Color startColor = GetLordColor();
        Color targetColor = speaking ? lordBaseColor : Multiply(lordBaseColor, lordListeningTint);
        Transform lordTransform = lord.transform;

        float elapsed = 0f;
        const float duration = 0.4f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            SetLordColor(Color.Lerp(startColor, targetColor, progress));
            if (speaking)
                lordTransform.localScale = lordRestScale * Mathf.Lerp(1f, 1.04f, Mathf.Sin(progress * Mathf.PI));
            yield return null;
        }

        SetLordColor(targetColor);
        lordTransform.localScale = lordRestScale;
        lordEffectCoroutine = null;
    }

    protected override float TypingSpeedFor(A3Line line)
    {
        return line.speakerName == lordName ? lordTypingSpeed : base.TypingSpeedFor(line);
    }

    // ------------------------------------------------------------------ light, then waking up

    // The gates fill with white light (with the teleport sound if there is one).
    // God's last words are heard over the white.
    private IEnumerator WhiteLight()
    {
        HideDialogueBox();
        if (nextButton != null) nextButton.SetActive(false);

        PlayEffect(teleportSound);
        if (godlySound != null) StartCoroutine(FadeAudio(godlySound, OriginalVolume(godlySound) * 1.2f, 2f));

        yield return FadeToColor(Color.white, 2.5f);
        if (lord != null) lord.SetActive(false);

        // The dialogue box stays readable on top of the white.
        if (dialogueBox != null) dialogueBox.transform.SetAsLastSibling();
        yield return new WaitForSeconds(1f);
    }

    // The white slowly sinks into darkness: eyes closed, back home. Birds outside, if there are any.
    private IEnumerator WakeUpInTheDark()
    {
        HideDialogueBox();
        if (nextButton != null) nextButton.SetActive(false);
        if (godlySound != null) StartCoroutine(FadeAudio(godlySound, 0f, 3f));
        if (ambientAudioSource != null) StartCoroutine(FadeAudio(ambientAudioSource, 0f, 3f));

        Image cover = fadeOutScreen != null ? fadeOutScreen.GetComponent<Image>() : null;
        if (cover != null)
        {
            Color startColor = cover.color;
            float elapsed = 0f;
            const float duration = 3f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                Color color = Color.Lerp(startColor, Color.black, Mathf.SmoothStep(0f, 1f, elapsed / duration));
                color.a = 1f;
                cover.color = color;
                yield return null;
            }

            cover.color = Color.black;
        }

        yield return new WaitForSeconds(1.5f);
        StartLoop(birdsSound, 0.6f, 2f);
        yield return new WaitForSeconds(1f);
    }

    // ------------------------------------------------------------------ the ending

    private IEnumerator Ending()
    {
        isEnding = true;
        HideDialogueBox();
        if (nextButton != null) nextButton.SetActive(false);
        StopAllSounds(2.5f);
        yield return FadeToBlack(1f);
        yield return new WaitForSeconds(1.5f);

        PlayerDatabase.MarkActCompleted(actNumber);

        // Ephesians 2:8-9, one line at a time.
        List<TMP_Text> verseTexts = new List<TMP_Text>();
        float top = 0.78f;
        foreach (string verseLine in verseLines)
        {
            TMP_Text text = CreateText("VerseLine", verseLine, 32f, new Vector2(0.05f, top - 0.09f), new Vector2(0.95f, top));
            verseTexts.Add(text);
            yield return FadeTextAlpha(text, 0f, 1f, 1.5f);
            yield return new WaitForSeconds(0.8f);
            top -= 0.09f;
        }

        TMP_Text reference = CreateText("VerseReference", verseReference, 26f, new Vector2(0.05f, top - 0.12f), new Vector2(0.95f, top - 0.03f));
        reference.color = new Color(1f, 0.9f, 0.6f, 0f);
        verseTexts.Add(reference);
        yield return FadeTextAlpha(reference, 0f, 1f, 1.5f);
        yield return new WaitForSeconds(5f);

        foreach (TMP_Text text in verseTexts)
            StartCoroutine(FadeTextAlpha(text, 1f, 0f, 2f));
        yield return new WaitForSeconds(3f);

        yield return Credits();
        yield return CrossedOutSummary();
        ShowMainMenuButton();
    }

    // Minimal credits: THE END / Created by / Shem De Leon.
    private IEnumerator Credits()
    {
        TMP_Text theEnd = CreateText("TheEnd", theEndText, 64f, new Vector2(0.1f, 0.55f), new Vector2(0.9f, 0.75f));
        TMP_Text createdBy = CreateText("CreatedBy", createdByText, 24f, new Vector2(0.1f, 0.42f), new Vector2(0.9f, 0.5f));
        TMP_Text creator = CreateText("CreatorName", creatorName, 36f, new Vector2(0.1f, 0.32f), new Vector2(0.9f, 0.43f));
        createdBy.color = new Color(0.75f, 0.75f, 0.75f, 0f);

        yield return FadeTextAlpha(theEnd, 0f, 1f, 2f);
        yield return new WaitForSeconds(0.8f);
        StartCoroutine(FadeTextAlpha(createdBy, 0f, 1f, 1.5f));
        yield return FadeTextAlpha(creator, 0f, 1f, 1.5f);
        yield return new WaitForSeconds(3.5f);

        StartCoroutine(FadeTextAlpha(theEnd, 1f, 0f, 2f));
        StartCoroutine(FadeTextAlpha(createdBy, 1f, 0f, 2f));
        yield return FadeTextAlpha(creator, 1f, 0f, 2f);
        Destroy(theEnd.gameObject);
        Destroy(createdBy.gameObject);
        Destroy(creator.gameObject);
        yield return new WaitForSeconds(1f);
    }

    // Every good deed the player did in the whole game appears... and then each one is crossed out.
    // They were good, but they were never the way through the gates.
    private IEnumerator CrossedOutSummary()
    {
        List<string> entries = PlayerDatabase.GetGoodDeedLabels();
        entries.Add($"Good Points: {PlayerDatabase.GoodPoints}");
        bool[] shown = new bool[entries.Count];
        bool[] struck = new bool[entries.Count];

        TMP_Text title = CreateText("SummaryTitle", summaryTitle, 36f, new Vector2(0.1f, 0.86f), new Vector2(0.9f, 0.96f));
        TMP_Text list = CreateText("SummaryList", "", 28f, new Vector2(0.12f, 0.36f), new Vector2(0.88f, 0.85f), TextAlignmentOptions.Top);
        list.enableAutoSizing = true;
        list.fontSizeMin = 14f;
        list.fontSizeMax = 28f;
        list.lineSpacing = 6f;
        list.alpha = 1f;

        yield return FadeTextAlpha(title, 0f, 1f, 1f);

        // The deeds appear one by one.
        for (int i = 0; i < entries.Count; i++)
        {
            shown[i] = true;
            list.text = BuildSummaryText(entries, shown, struck);
            yield return new WaitForSeconds(summaryLineDelay);
        }

        yield return new WaitForSeconds(2f);

        // ...and are crossed out one by one.
        for (int i = 0; i < entries.Count; i++)
        {
            struck[i] = true;
            list.text = BuildSummaryText(entries, shown, struck);
            PlayEffect(clickAudioSource);
            yield return new WaitForSeconds(strikeDelay);
        }

        yield return new WaitForSeconds(1.5f);

        // Two lines between the list and the Main Menu button; shrinks a little if it needs to.
        TMP_Text finalLine = CreateText("SummaryFinalLine", closingMessage, 30f, new Vector2(0.06f, 0.2f), new Vector2(0.94f, 0.355f));
        finalLine.enableAutoSizing = true;
        finalLine.fontSizeMin = 18f;
        finalLine.fontSizeMax = 30f;
        finalLine.lineSpacing = 4f;
        finalLine.color = new Color(1f, 0.88f, 0.55f, 0f);
        yield return FadeTextAlpha(finalLine, 0f, 1f, 2.5f);
        yield return new WaitForSeconds(2f);
    }

    private static string BuildSummaryText(List<string> entries, bool[] shown, bool[] struck)
    {
        System.Text.StringBuilder text = new System.Text.StringBuilder();
        for (int i = 0; i < entries.Count; i++)
        {
            if (!shown[i])
                break;

            text.AppendLine(struck[i] ? $"<color=#8A8A8A><s>{entries[i]}</s></color>" : entries[i]);
        }

        return text.ToString();
    }

    private void ShowMainMenuButton()
    {
        string menuScene = string.IsNullOrEmpty(mainMenuSceneName) ? "Main Menu" : mainMenuSceneName;
        if (choiceContainer == null || choiceButton1 == null)
        {
            SceneManager.LoadScene(menuScene);
            return;
        }

        RectTransform containerTransform = choiceContainer.transform as RectTransform;
        RectTransform canvasRect = canvasTransform as RectTransform;
        if (containerTransform != null && canvasRect != null)
            containerTransform.anchoredPosition = new Vector2(0f, -canvasRect.rect.height * 0.36f);

        choiceContainer.transform.SetAsLastSibling();
        isEnding = false;
        ShowChoice("Main Menu", () => SceneManager.LoadScene(menuScene));
    }
}
