using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

// Act 3, living room in the morning. Nanay is pale and coughing after her night shift.
// The player cleans the whole house (every kalat is +1 on the counter), and Nanay asks
// why they look like they are chasing something.
public class A3_Lroom_control : A3SceneBase
{
    public const string ChoresActionId = "act3_morning_chores";
    public const string WhyChasingChoiceId = "act3_why_chasing";

    [Header("Nanay")]
    [Tooltip("Nanay looks pale and tired in this scene.")]
    [SerializeField] private Color nanayPaleTint = new Color(0.92f, 0.9f, 1f, 1f);

    [Header("Chores")]
    [SerializeField] private string choresHint = "Clean up everything!";
    [SerializeField] private float cleaningPitchVariation = 0.1f;

    [Header("Choice: Why are you chasing something?")]
    [SerializeField] private string beGoodChoiceText = "I just want to be good, Nay.";
    [SerializeField] private string enoughChoiceText = "I have to do enough.";

    [Header("Scene Transition")]
    [SerializeField] private string nextSceneName = "A3_School";

    [Header("Dialogue")]
    public List<A3Line> introLines = new List<A3Line>();
    public List<A3Line> afterChoresLines = new List<A3Line>();
    public List<A3Line> beGoodLines = new List<A3Line>();
    public List<A3Line> enoughLines = new List<A3Line>();
    public List<A3Line> outroLines = new List<A3Line>();

    private readonly List<ClickableUIItem> kalatItems = new List<ClickableUIItem>();
    private readonly HashSet<ClickableUIItem> cleanedItems = new HashSet<ClickableUIItem>();
    private TMP_Text progressText;
    private Vector3 progressBaseScale = Vector3.one;
    private bool choresStarted;
    private bool choresDone;
    private float choresStartTime;
    private AudioSource cleaningSound;
    private AudioSource victorySound;
    private AudioSource stairsSound;
    private AudioSource doorSound;

    protected override void SetupScene()
    {
        A3Character nanay = AddCharacter("Nanay", "Nanay", trembles: true);
        if (nanay.image != null)
            nanay.baseColor = Multiply(nanay.baseColor, nanayPaleTint);

        cleaningSound = FindSound("clean");
        victorySound = FindSound("victory");
        stairsSound = FindSound("stair");
        doorSound = FindSound("door");

        // The kalat stay in the room but can't be picked up until the chores start.
        foreach (ClickableUIItem item in FindObjectsByType<ClickableUIItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!item.gameObject.scene.IsValid())
                continue;

            kalatItems.Add(item);
            item.enabled = false;
        }

        GameObject progressObject = canvasTransform != null ? FindDeepObject(canvasTransform, "Kalat_progress") : null;
        if (progressObject == null) progressObject = FindSceneObject("Kalat_progress");
        if (progressObject != null)
        {
            progressText = progressObject.GetComponent<TMP_Text>();
            progressBaseScale = progressObject.transform.localScale;
            progressObject.SetActive(false);
        }

        FillDefaultLines();
    }

    private void FillDefaultLines()
    {
        if (introLines.Count == 0)
        {
            introLines.Add(new A3Line(playerName, "Good morning, Nay!"));
            introLines.Add(new A3Line("Nanay", "Good morning, anak... *cough*", "enterslow:Nanay;sfx:cough"));
            introLines.Add(new A3Line(playerName, "Nay... you look so pale."));
            introLines.Add(new A3Line("Nanay", "Okay lang ako. I'm just tired from the night shift."));
            introLines.Add(new A3Line(playerName, "Then sit down po. I'll clean the whole house!"));
            introLines.Add(new A3Line("Nanay", "Ha? Anak, it's fine, you'll be late for—"));
            introLines.Add(new A3Line(playerName, "It's okay! Every bit counts!"));
        }

        if (afterChoresLines.Count == 0)
        {
            afterChoresLines.Add(new A3Line(playerName, "(That's {deeds} already... I can still do more.)"));
            afterChoresLines.Add(new A3Line("Nanay", "Anak..."));
            afterChoresLines.Add(new A3Line("Nanay", "Why do you look like you're chasing something?"));
            afterChoresLines.Add(new A3Line(playerName, "Huh? I'm not... I just..."));
        }

        if (beGoodLines.Count == 0)
        {
            beGoodLines.Add(new A3Line(playerName, "I just want to be good, Nay."));
            beGoodLines.Add(new A3Line("Nanay", "You already are, anak."));
        }

        if (enoughLines.Count == 0)
        {
            enoughLines.Add(new A3Line(playerName, "I have to do enough."));
            enoughLines.Add(new A3Line("Nanay", "Enough... for who, anak?"));
        }

        if (outroLines.Count == 0)
        {
            outroLines.Add(new A3Line("Nanay", "You're my child. You don't have to earn anything from me."));
            outroLines.Add(new A3Line(playerName, "(...)"));
            outroLines.Add(new A3Line(playerName, "Nay, let me stay home today. I'll take care of you."));
            outroLines.Add(new A3Line("Nanay", "No. Pumasok ka. I'll be fine, I promise. *cough* *cough*", "sfx:cough"));
            outroLines.Add(new A3Line(playerName, "...Opo."));
            outroLines.Add(new A3Line(playerName, "(If I do enough good today... she'll be okay. Right?)"));
        }
    }

    protected override IEnumerator RunScene()
    {
        // Coming down the stairs in the dark, then the living room appears.
        yield return new WaitForSeconds(0.4f);
        PlayEffect(stairsSound);
        yield return new WaitForSeconds(EffectLength(stairsSound, 0.5f, 1.5f));
        StartLoop(ambientAudioSource, 1f, 1.5f);
        yield return FadeFromBlack(1.5f);
        yield return new WaitForSeconds(0.3f);

        yield return PlayLines(introLines);
        yield return DoChores();
        yield return PlayLines(afterChoresLines);

        int picked = -1;
        yield return WaitForChoice(beGoodChoiceText, enoughChoiceText, choice => picked = choice);
        const string question = "Nanay asked why you look like you're chasing something.";
        PlayerDatabase.RecordChoice(WhyChasingChoiceId, question, picked == 0 ? beGoodChoiceText : enoughChoiceText, 0,
            picked == 0 ? "be_good" : "enough");

        yield return PlayLines(picked == 0 ? beGoodLines : enoughLines);
        yield return PlayLines(outroLines);
        yield return FadeOutAndLoad(nextSceneName, "A3_School", 1.2f, doorSound);
    }

    // ------------------------------------------------------------------ chores

    private IEnumerator DoChores()
    {
        HideDialogueBox();
        if (nextButton != null) nextButton.SetActive(false);

        if (kalatItems.Count == 0)
            yield break;

        choresStarted = true;
        choresStartTime = Time.time;
        foreach (ClickableUIItem item in kalatItems)
        {
            if (item.gameObject.activeInHierarchy)
                item.enabled = true;
            else
                cleanedItems.Add(item);
        }

        if (progressText != null)
        {
            progressText.gameObject.SetActive(true);
            UpdateProgress();
        }

        if (cleanedItems.Count >= kalatItems.Count)
            choresDone = true;
        else
            StartCoroutine(ShowHint());

        while (!choresDone)
            yield return null;

        float seconds = Time.time - choresStartTime;
        PlayerDatabase.RecordAction(ChoresActionId, $"Cleaned the whole house before school ({kalatItems.Count} items in {seconds:0} seconds)", seconds, 1);

        yield return new WaitForSeconds(0.3f);
        PlayEffect(victorySound);
        yield return new WaitForSeconds(1f);
        if (progressText != null) progressText.gameObject.SetActive(false);
    }

    // Called by ClickableUIItem when the player clicks a kalat.
    public void RegisterKalatCleaned(ClickableUIItem item)
    {
        if (!choresStarted || choresDone || item == null || !cleanedItems.Add(item))
            return;

        item.enabled = false;
        PlayCleaningSound();
        StartCoroutine(PopAway(item.transform));
        AddDeed();
        UpdateProgress();
        StartCoroutine(PunchProgress());

        if (cleanedItems.Count >= kalatItems.Count)
            choresDone = true;
    }

    private void PlayCleaningSound()
    {
        if (cleaningSound == null || cleaningSound.clip == null)
            return;

        cleaningSound.pitch = 1f + Random.Range(-cleaningPitchVariation, cleaningPitchVariation);
        cleaningSound.PlayOneShot(cleaningSound.clip);
    }

    private void UpdateProgress()
    {
        if (progressText != null)
            progressText.text = $"Kalat: {cleanedItems.Count} / {kalatItems.Count}";
    }

    private IEnumerator ShowHint()
    {
        TMP_Text hint = CreateText("ChoresHint", choresHint, 28f, new Vector2(0.1f, 0.75f), new Vector2(0.9f, 0.88f));
        if (hint == null)
            yield break;

        hint.outlineWidth = 0.2f;
        hint.outlineColor = Color.black;
        yield return FadeTextAlpha(hint, 0f, 1f, 0.5f);
        yield return new WaitForSeconds(2f);
        yield return FadeTextAlpha(hint, 1f, 0f, 0.5f);
        Destroy(hint.gameObject);
    }

    private IEnumerator PunchProgress()
    {
        if (progressText == null)
            yield break;

        Transform textTransform = progressText.transform;
        float elapsed = 0f;
        const float duration = 0.2f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float bump = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
            textTransform.localScale = progressBaseScale * (1f + 0.2f * bump);
            yield return null;
        }

        textTransform.localScale = progressBaseScale;
    }

    // The picked-up kalat pops a little bigger, then shrinks away.
    private static IEnumerator PopAway(Transform itemTransform)
    {
        Vector3 baseScale = itemTransform.localScale;
        Vector3 bigScale = baseScale * 1.25f;

        float elapsed = 0f;
        const float growDuration = 0.08f;
        while (elapsed < growDuration)
        {
            elapsed += Time.deltaTime;
            itemTransform.localScale = Vector3.Lerp(baseScale, bigScale, elapsed / growDuration);
            yield return null;
        }

        elapsed = 0f;
        const float shrinkDuration = 0.18f;
        while (elapsed < shrinkDuration)
        {
            elapsed += Time.deltaTime;
            itemTransform.localScale = Vector3.Lerp(bigScale, Vector3.zero, Mathf.SmoothStep(0f, 1f, elapsed / shrinkDuration));
            yield return null;
        }

        itemTransform.gameObject.SetActive(false);
        itemTransform.localScale = baseScale;
    }
}
