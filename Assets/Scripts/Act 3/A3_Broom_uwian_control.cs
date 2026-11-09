using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// The last scene of the game. After the prayer, the player dreams of the gates again.
// God answers: "I heard you, child." He asks the first question one last time, the player answers
// with trust, and He sends them back gently: "Wake up, child. And this time... rest."
// Morning: Nanay's fever is down and the counter is gone. Then Ephesians 2:8-9, and THE END.
public class A3_Broom_uwian_control : A3SceneBase
{
    public const string WhyOpenChoiceId = "act3_why_open";

    [Header("Heaven")]
    [Tooltip("Drag a Heaven picture here (Assets/Backgrounds/Heaven/SCENE). If empty, the bedroom glows instead.")]
    [SerializeField] private Sprite heavenSprite;
    [SerializeField] private Color noHeavenGlowTint = new Color(1f, 0.95f, 0.8f, 1f);

    [Header("God")]
    [SerializeField] private string lordName = "God";
    [SerializeField] private float lordAppearDuration = 2.5f;
    [SerializeField] private float lordRiseDistance = 30f;
    [SerializeField] private float lordTypingSpeed = 0.05f;
    [SerializeField] private Color lordListeningTint = new Color(0.75f, 0.75f, 0.75f, 1f);

    [Header("Choice: Why should I open the gates to you?")]
    [SerializeField] private string undeservingChoiceText = "I don't deserve it...";
    [SerializeField] private string nothingIDidChoiceText = "Not because of anything I did...";
    [SerializeField] private string trustLine = "...but I trust You.";

    [Header("Ending")]
    [SerializeField] private string[] verseLines =
    {
        "\"For by grace you have been saved through faith,",
        "and that not of yourselves;",
        "it is the gift of God,",
        "not of works, that no one would boast.\""
    };
    [SerializeField] private string verseReference = "Ephesians 2:8-9";
    [SerializeField] private string theEndText = "THE END";
    [SerializeField] private string mainMenuSceneName = "Main Menu";
    [SerializeField] private int actNumber = 3;

    [Header("Dialogue")]
    public List<A3Line> visionLines = new List<A3Line>();
    public List<A3Line> wakeLines = new List<A3Line>();
    public List<A3Line> morningLines = new List<A3Line>();

    private GameObject lord;
    private SpriteRenderer lordSprite;
    private Vector3 lordRestPosition;
    private Vector3 lordRestScale = Vector3.one;
    private Color lordBaseColor = Color.white;
    private Coroutine lordEffectCoroutine;
    private Sprite bedroomSprite;

    private AudioSource godlySound;
    private AudioSource fanSound;
    private AudioSource birdsSound;

    protected override bool CounterVisibleAtStart => false;

    protected override void SetupScene()
    {
        // The counter broke last night. It is not coming back.
        showGoodDeedsCounter = false;

        lord = FindSceneObject("Lord");
        if (lord != null)
        {
            lordSprite = lord.GetComponent<SpriteRenderer>();
            lordRestPosition = lord.transform.localPosition;
            lordRestScale = lord.transform.localScale;
            if (lordSprite != null)
            {
                lordBaseColor = lordSprite.color;
                if (backgroundRenderer != null)
                {
                    lordSprite.sortingLayerID = backgroundRenderer.sortingLayerID;
                    lordSprite.sortingOrder = backgroundRenderer.sortingOrder + 1;
                }
            }

            lord.SetActive(false);
        }

        if (backgroundRenderer != null)
            bedroomSprite = backgroundRenderer.sprite;

        AddCharacter("Nanay", "Nanay");

        godlySound = FindSound("godly", "choir", "heaven");
        fanSound = FindSound("fan");
        birdsSound = FindSound("bird");

        FillDefaultLines();
    }

    private void FillDefaultLines()
    {
        if (visionLines.Count == 0)
        {
            visionLines.Add(new A3Line(lordName, "Child."));
            visionLines.Add(new A3Line(playerName, "...Where am I?"));
            visionLines.Add(new A3Line(playerName, "(This light... the gates. Just like the very first dream.)"));
            visionLines.Add(new A3Line(lordName, "I heard you, child."));
            visionLines.Add(new A3Line(playerName, "You... heard me?"));
            visionLines.Add(new A3Line(lordName, "Every word."));
        }

        if (wakeLines.Count == 0)
        {
            wakeLines.Add(new A3Line(lordName, "Wake up, child."));
            wakeLines.Add(new A3Line(lordName, "And this time..."));
            wakeLines.Add(new A3Line(lordName, "...rest."));
        }

        if (morningLines.Count == 0)
        {
            morningLines.Add(new A3Line(playerName, "(...Morning.)"));
            morningLines.Add(new A3Line(playerName, "(I'm in my room...?)"));
            morningLines.Add(new A3Line("Nanay", "Good morning, anak.", "enterslow:Nanay"));
            morningLines.Add(new A3Line(playerName, "Nay! Your fever—"));
            morningLines.Add(new A3Line("Nanay", "Bumaba na. I'm still weak, but I'm okay."));
            morningLines.Add(new A3Line("Nanay", "Aling Rosa found you asleep beside me, so she helped me bring you up here."));
            morningLines.Add(new A3Line("Nanay", "Salamat, anak. But next time, rest too, ha? You don't have to carry everything alone."));
            morningLines.Add(new A3Line(playerName, "...Opo, Nay."));
            morningLines.Add(new A3Line("Elijah (text)", "pre kumusta si tita? we prayed for her kagabi", "sfx:phone"));
            morningLines.Add(new A3Line(playerName, "(...)"));
            morningLines.Add(new A3Line(playerName, "(For the first time in a long time...)"));
            morningLines.Add(new A3Line(playerName, "(I'm not counting.)"));
        }
    }

    // God remembers what the player answered at the end of Act 2.
    private List<A3Line> BuildMemoryLines()
    {
        List<A3Line> lines = new List<A3Line>();
        string whyGood = PlayerDatabase.GetChoice(A2_Broom_uwian.WhyGoodChoiceId);

        if (whyGood == A2_Broom_uwian.ForHeavenAnswer)
        {
            lines.Add(new A3Line(lordName, "Once, you told Me you did good so you could enter here."));
            lines.Add(new A3Line(playerName, "...I did."));
        }
        else if (whyGood == A2_Broom_uwian.ForLoveAnswer)
        {
            lines.Add(new A3Line(lordName, "Once, you told Me you did good because you loved them."));
            lines.Add(new A3Line(playerName, "...I did. But even my love wasn't enough."));
        }

        lines.Add(new A3Line(lordName, "Then let Me ask you once more."));
        lines.Add(new A3Line(lordName, "Why should I open the gates to you?"));
        return lines;
    }

    protected override IEnumerator RunScene()
    {
        // Silence in the dark, then the light of the gates.
        yield return new WaitForSeconds(1.5f);
        ShowHeaven();
        StartLoop(godlySound, 1f, 3f);
        yield return FadeFromBlack(3f);
        yield return LordAppears();

        yield return PlayLines(visionLines);
        yield return PlayLines(BuildMemoryLines());

        int picked = -1;
        yield return WaitForChoice(undeservingChoiceText, nothingIDidChoiceText, choice => picked = choice);
        string answer = picked == 0 ? undeservingChoiceText : nothingIDidChoiceText;
        PlayerDatabase.RecordChoice(WhyOpenChoiceId, "God asked one last time: \"Why should I open the gates to you?\"",
            $"{answer} {trustLine}", 0, picked == 0 ? "undeserving" : "nothing_i_did");

        yield return PlayLines(new List<A3Line>
        {
            new A3Line(playerName, answer),
            new A3Line(playerName, trustLine),
            new A3Line(lordName, "..."),
            new A3Line(lordName, "That is all I ever wanted to see.")
        });

        yield return WhiteLight();
        yield return PlayLines(wakeLines);
        yield return Morning();
        yield return PlayLines(morningLines);
        yield return Ending();
    }

    // ------------------------------------------------------------------ the vision

    private void ShowHeaven()
    {
        if (backgroundRenderer == null)
            return;

        if (heavenSprite != null)
        {
            backgroundRenderer.sprite = heavenSprite;
            backgroundRenderer.color = Color.white;
        }
        else
        {
            SetBackgroundTint(noHeavenGlowTint);
        }
    }

    private IEnumerator LordAppears()
    {
        if (lord == null)
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

    private void SetLordColor(Color color)
    {
        if (lordSprite != null)
            lordSprite.color = color;
    }

    // God glows and pulses a little when He speaks, and dims softly while the player speaks.
    protected override void OnLineShown(A3Line line)
    {
        if (lord == null || !lord.activeInHierarchy)
            return;

        if (lordEffectCoroutine != null)
            StopCoroutine(lordEffectCoroutine);

        bool lordSpeaking = line.speakerName == lordName;
        lordEffectCoroutine = StartCoroutine(LordSpeakEffect(lordSpeaking));
    }

    private IEnumerator LordSpeakEffect(bool speaking)
    {
        Color startColor = lordSprite != null ? lordSprite.color : Color.white;
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

    // Everything fills with white light; God's last words are heard over it.
    private IEnumerator WhiteLight()
    {
        HideDialogueBox();
        if (nextButton != null) nextButton.SetActive(false);
        if (godlySound != null) StartCoroutine(FadeAudio(godlySound, OriginalVolume(godlySound) * 1.2f, 2f));

        yield return FadeToColor(Color.white, 2.5f);
        if (lord != null) lord.SetActive(false);

        // The dialogue box stays readable on top of the white.
        if (dialogueBox != null) dialogueBox.transform.SetAsLastSibling();
        yield return new WaitForSeconds(1f);
    }

    // The light fades into a quiet morning in the bedroom.
    private IEnumerator Morning()
    {
        HideDialogueBox();
        if (nextButton != null) nextButton.SetActive(false);
        if (godlySound != null) StartCoroutine(FadeAudio(godlySound, 0f, 3f));

        if (backgroundRenderer != null)
        {
            if (bedroomSprite != null) backgroundRenderer.sprite = bedroomSprite;
            backgroundRenderer.color = backgroundBaseColor;
        }

        yield return new WaitForSeconds(1.5f);
        StartLoop(fanSound, 0.8f, 2f);
        StartLoop(birdsSound, 0.8f, 2f);
        yield return FadeOutCover(3f);
        yield return new WaitForSeconds(0.5f);
    }

    // ------------------------------------------------------------------ the ending

    private IEnumerator Ending()
    {
        isEnding = true;
        HideDialogueBox();
        if (nextButton != null) nextButton.SetActive(false);
        StopAllSounds(2.5f);
        yield return FadeToBlack(2.5f);
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

        // THE END. The good points show for a moment... then they fade away.
        TMP_Text theEnd = CreateText("TheEnd", theEndText, 64f, new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.72f));
        yield return FadeTextAlpha(theEnd, 0f, 1f, 2f);
        yield return new WaitForSeconds(1f);

        TMP_Text points = CreateText("FinalGoodPoints", $"Good Points: {PlayerDatabase.GoodPoints}", 26f, new Vector2(0.1f, 0.4f), new Vector2(0.9f, 0.5f));
        points.color = new Color(0.8f, 0.8f, 0.8f, 0f);
        yield return FadeTextAlpha(points, 0f, 1f, 1.5f);
        yield return new WaitForSeconds(2f);
        yield return FadeTextAlpha(points, 1f, 0f, 3f);
        yield return new WaitForSeconds(1f);

        ShowMainMenuButton();
    }

    private void ShowMainMenuButton()
    {
        if (choiceContainer == null || choiceButton1 == null)
        {
            SceneManager.LoadScene(mainMenuSceneName);
            return;
        }

        RectTransform containerTransform = choiceContainer.transform as RectTransform;
        RectTransform canvasRect = canvasTransform as RectTransform;
        if (containerTransform != null && canvasRect != null)
            containerTransform.anchoredPosition = new Vector2(0f, -canvasRect.rect.height * 0.3f);

        choiceContainer.transform.SetAsLastSibling();
        isEnding = false;
        ShowChoice("Main Menu", () => SceneManager.LoadScene(string.IsNullOrEmpty(mainMenuSceneName) ? "Main Menu" : mainMenuSceneName));
    }
}
