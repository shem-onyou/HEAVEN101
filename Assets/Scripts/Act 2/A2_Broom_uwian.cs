using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum A2BroomUwiAction
{
    None,
    LieDown,
    LordAppears,
    ShowTally,
    HideTally,
    Pause,
    FinalShake
}

[System.Serializable]
public class A2BroomUwiDialogueLine
{
    public A2BroomUwiAction actionBefore;
    public string speakerName;
    [TextArea(2, 5)] public string message;

    public A2BroomUwiDialogueLine(string name, string msg, A2BroomUwiAction action = A2BroomUwiAction.None)
    {
        speakerName = name;
        message = msg;
        actionBefore = action;
    }
}

// The last scene of Act 2: night in the bedroom. The player quietly remembers the day (based on
// their real choices), falls asleep, and God appears again. He shows the player their own tally of
// good points and asks why they did good today. It ends on a question, then the Act 2 results.
public class A2_Broom_uwian : MonoBehaviour
{
    public const string WhyGoodChoiceId = "act2_why_good";
    public const string ForHeavenAnswer = "heaven";
    public const string ForLoveAnswer = "love";

    [Header("Cutscene Objects (auto-found by name if empty)")]
    public GameObject fadeScreenIn;
    public GameObject fadeOutScreen;
    public GameObject dialogueBox;
    public GameObject nextButton;
    public GameObject charLord;

    [Header("Choice System Objects (auto-found by name if empty)")]
    public GameObject choiceContainer;
    public Button choiceButton1;
    public TMP_Text choiceButton1Text;
    [Tooltip("This scene only has choice_Button1; if this is empty, a copy of Button 1 is made.")]
    public Button choiceButton2;
    public TMP_Text choiceButton2Text;

    [Header("Choice: Why did you do good today?")]
    [SerializeField] private string forHeavenChoiceText = "So I can enter Heaven.";
    [SerializeField] private string forLoveChoiceText = "Because I love them.";

    [Header("Characters")]
    [SerializeField] private string playerName = "You";
    [SerializeField] private string lordName = "God";
    [SerializeField] private float lordAppearDuration = 2f;
    [SerializeField] private float lordRiseDistance = 30f;

    [Header("Room")]
    [Tooltip("Found by name \"BG_bedroom\".")]
    public SpriteRenderer backgroundRenderer;
    [SerializeField] private Color nightTint = new Color(0.42f, 0.42f, 0.6f, 1f);
    [SerializeField] private float sleepFadeDuration = 2.5f;

    [Header("Speaking Effects")]
    [Tooltip("God's tint while You is speaking. Kept light so God never looks too dark.")]
    [SerializeField] private Color lordDarkenedTint = new Color(0.6f, 0.6f, 0.6f, 1f);
    [SerializeField] private float tintDuration = 0.2f;
    [SerializeField] private float speakPulseScale = 1.04f;
    [SerializeField] private float speakPulseDuration = 0.4f;
    [SerializeField] private float finalShakeStrength = 14f;
    [SerializeField] private float finalShakeDuration = 0.8f;

    [Header("Text UI References (auto-found by name if empty)")]
    public TMP_Text charNameText;
    public TMP_Text messageInputText;

    [Header("Typewriter Speed Settings")]
    [SerializeField] private float typingSpeed = 0.03f;
    [SerializeField] private float slowTypingSpeed = 0.06f;
    [Tooltip("God speaks slowly in this scene.")]
    [SerializeField] private float lordTypingSpeed = 0.05f;

    [Header("Sounds (auto-found by object or clip name if empty)")]
    [Tooltip("The electric fan at night. Found by \"fan\".")]
    [SerializeField] private AudioSource fanAudioSource;
    [Tooltip("Optional: night ambient (crickets). Found by \"ambient\" or \"night\".")]
    [SerializeField] private AudioSource ambientAudioSource;
    [Tooltip("Found by \"beat\" (hearbeat).")]
    [SerializeField] private AudioSource heartbeatAudioSource;
    [Tooltip("Found by \"shake\".")]
    [SerializeField] private AudioSource shakeAudioSource;
    [Tooltip("Optional: God's presence, looped. Found by \"godly\".")]
    [SerializeField] private AudioSource godlyAudioSource;
    [SerializeField] private AudioSource clickAudioSource;

    [Header("Act Results")]
    [SerializeField] private int actNumber = 2;
    [SerializeField] private string actResultsTitle = "Act 2 Complete";
    [SerializeField] private string mainMenuSceneName = "Main Menu";
    [Tooltip("First scene of Act 3. Empty means \"A3_Broom\".")]
    [SerializeField] private string nextActSceneName = "A3_Broom";

    [Header("Dialogue (the reflection lines are built from the player's choices)")]
    public List<A2BroomUwiDialogueLine> openingLines = new List<A2BroomUwiDialogueLine>();
    public List<A2BroomUwiDialogueLine> sleepLines = new List<A2BroomUwiDialogueLine>();
    public List<A2BroomUwiDialogueLine> visionLines = new List<A2BroomUwiDialogueLine>();
    public List<A2BroomUwiDialogueLine> forHeavenLines = new List<A2BroomUwiDialogueLine>();
    public List<A2BroomUwiDialogueLine> forLoveLines = new List<A2BroomUwiDialogueLine>();
    public List<A2BroomUwiDialogueLine> finalLines = new List<A2BroomUwiDialogueLine>();

    private SpriteRenderer lordSprite;
    private Graphic lordGraphic;
    private Vector3 lordRestPosition;
    private Vector3 lordRestScale = Vector3.one;
    private Color lordBaseColor = Color.white;
    private Coroutine lordTintCoroutine;
    private Coroutine lordPulseCoroutine;
    private Color backgroundBaseColor = Color.white;
    private TMP_Text tallyText;

    private List<A2BroomUwiDialogueLine> currentLines;
    private int currentLineIndex;
    private System.Action onLinesFinished;
    private Coroutine typingCoroutine;
    private bool isTyping;
    private bool isPlayingAction;
    private bool waitingForChoice;
    private bool isEnding;
    private float fanVolume = 1f;
    private float ambientVolume = 1f;
    private float heartbeatVolume = 1f;
    private float godlyVolume = 1f;

    void Start()
    {
        FillDefaultLines();
        FindSceneReferences();

        if (choiceContainer != null) choiceContainer.SetActive(false);
        if (fadeOutScreen != null) fadeOutScreen.SetActive(false);
        if (dialogueBox != null) dialogueBox.SetActive(false);
        HoldBlackScreen();

        if (backgroundRenderer != null)
        {
            backgroundBaseColor = backgroundRenderer.color;
            backgroundRenderer.color = MultiplyColor(backgroundBaseColor, nightTint);
        }

        if (charLord != null)
        {
            lordSprite = charLord.GetComponent<SpriteRenderer>();
            lordGraphic = charLord.GetComponent<Graphic>();
            lordRestPosition = charLord.transform.localPosition;
            lordRestScale = charLord.transform.localScale;
            lordBaseColor = GetLordColor();

            // God shows up in front of the (darkened) bedroom.
            SpriteRenderer backgroundSprite = backgroundRenderer;
            if (lordSprite != null && backgroundSprite != null)
            {
                lordSprite.sortingLayerID = backgroundSprite.sortingLayerID;
                lordSprite.sortingOrder = backgroundSprite.sortingOrder + 1;
            }

            charLord.SetActive(false);
        }

        if (nextButton != null)
        {
            Button nextDialogueButton = nextButton.GetComponent<Button>();
            if (nextDialogueButton != null)
            {
                nextDialogueButton.onClick.RemoveAllListeners();
                nextDialogueButton.onClick.AddListener(NextButton);
            }
        }

        StartCoroutine(StartCutscene());
    }

    private void FillDefaultLines()
    {
        if (openingLines.Count == 0)
        {
            openingLines.Add(new A2BroomUwiDialogueLine(playerName, "(...)"));
            openingLines.Add(new A2BroomUwiDialogueLine(playerName, "(What a day.)"));
        }

        if (sleepLines.Count == 0)
        {
            sleepLines.Add(new A2BroomUwiDialogueLine(playerName, "(I'm so tired...)"));
            sleepLines.Add(new A2BroomUwiDialogueLine(playerName, "...", A2BroomUwiAction.LieDown));
        }

        if (visionLines.Count == 0)
        {
            visionLines.Add(new A2BroomUwiDialogueLine(lordName, "Child.", A2BroomUwiAction.LordAppears));
            visionLines.Add(new A2BroomUwiDialogueLine(playerName, "(This voice... It's Him again.)"));
            visionLines.Add(new A2BroomUwiDialogueLine(lordName, "You kept count today, didn't you?"));
            visionLines.Add(new A2BroomUwiDialogueLine(lordName, "Every good deed. Every mistake. Adding... and taking away."));
            visionLines.Add(new A2BroomUwiDialogueLine(lordName, "This is your number.", A2BroomUwiAction.ShowTally));
            visionLines.Add(new A2BroomUwiDialogueLine(lordName, "Tell me, child. Is it enough?"));
            visionLines.Add(new A2BroomUwiDialogueLine(playerName, "I... I don't know."));
            visionLines.Add(new A2BroomUwiDialogueLine(lordName, "Then answer Me this.", A2BroomUwiAction.HideTally));
            visionLines.Add(new A2BroomUwiDialogueLine(lordName, "Why did you do good today?"));
        }

        if (forHeavenLines.Count == 0)
        {
            forHeavenLines.Add(new A2BroomUwiDialogueLine(playerName, "So I can enter Heaven."));
            forHeavenLines.Add(new A2BroomUwiDialogueLine(lordName, "..."));
            forHeavenLines.Add(new A2BroomUwiDialogueLine(lordName, "Then you will keep counting, child. Every day. Until your very last."));
        }

        if (forLoveLines.Count == 0)
        {
            forLoveLines.Add(new A2BroomUwiDialogueLine(playerName, "Because I love them."));
            forLoveLines.Add(new A2BroomUwiDialogueLine(lordName, "..."));
            forLoveLines.Add(new A2BroomUwiDialogueLine(lordName, "And yet, even your love could not reach everyone today."));
        }

        if (finalLines.Count == 0)
        {
            finalLines.Add(new A2BroomUwiDialogueLine(lordName, "So tell me, child..."));
            finalLines.Add(new A2BroomUwiDialogueLine(lordName, "When you fall short... what then?", A2BroomUwiAction.Pause));
        }
    }

    // The player's quiet thoughts about what really happened today.
    private List<A2BroomUwiDialogueLine> BuildReflectionLines()
    {
        List<A2BroomUwiDialogueLine> lines = new List<A2BroomUwiDialogueLine>();

        switch (PlayerDatabase.GetChoice(A2_School_uwian_scr.PulubiChoiceId))
        {
            case A2_School_uwian_scr.GaveFareAnswer:
                lines.Add(new A2BroomUwiDialogueLine(playerName, "(I gave that man my pamasahe. I hope his child is okay now.)"));
                break;
            case A2_School_uwian_scr.HesitatedAnswer:
                lines.Add(new A2BroomUwiDialogueLine(playerName, "(That man... I didn't even answer him. I just let them pull me away.)"));
                break;
            case A2_School_uwian_scr.KeptFareAnswer:
                lines.Add(new A2BroomUwiDialogueLine(playerName, "(That man... I wonder if his child got the medicine.)"));
                break;
        }

        if (PlayerDatabase.GetChoice(A2_Lroom_uwian.HonestChoiceId) == "lied")
            lines.Add(new A2BroomUwiDialogueLine(playerName, "(And Nanay... she believed me.)"));

        switch (PlayerDatabase.GetChoice(A2_Lroom_uwian.PromiseChoiceId))
        {
            case A2_Lroom_uwian.StayedAnswer:
                lines.Add(new A2BroomUwiDialogueLine(playerName, "(I kept my promise to Nanay.)"));
                lines.Add(new A2BroomUwiDialogueLine(playerName, "(...Elijah still hasn't replied.)"));
                break;
            case A2_Lroom_uwian.WentAnswer:
                lines.Add(new A2BroomUwiDialogueLine(playerName, "(I was there for Elijah.)"));
                lines.Add(new A2BroomUwiDialogueLine(playerName, "(...But I broke my promise to Nanay.)"));
                break;
        }

        return lines;
    }

    private void FindSceneReferences()
    {
        Transform canvasTransform = dialogueBox != null ? dialogueBox.transform.parent : null;
        if (canvasTransform == null)
        {
            GameObject canvasObject = GameObject.Find("Canvas");
            if (canvasObject != null)
                canvasTransform = canvasObject.transform;
        }

        FindSounds();

        if (backgroundRenderer == null)
        {
            GameObject background = canvasTransform != null ? FindDeepObject(canvasTransform, "BG_bedroom") : null;
            if (background == null) background = GameObject.Find("BG_bedroom");
            if (background != null) backgroundRenderer = background.GetComponent<SpriteRenderer>();
        }

        if (canvasTransform == null)
            return;

        if (dialogueBox == null) dialogueBox = FindDeepObject(canvasTransform, "DialogueBox");
        if (fadeScreenIn == null) fadeScreenIn = FindDeepObject(canvasTransform, "FadeIn");
        if (fadeOutScreen == null) fadeOutScreen = FindDeepObject(canvasTransform, "FadeOut");
        if (choiceContainer == null) choiceContainer = FindDeepObject(canvasTransform, "choice_Container");
        if (charLord == null) charLord = FindDeepObject(canvasTransform, "Lord");
        if (charLord == null)
        {
            // Lord may sit outside the Canvas in this scene.
            foreach (Transform sceneTransform in FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (sceneTransform.name == "Lord")
                {
                    charLord = sceneTransform.gameObject;
                    break;
                }
            }
        }

        if (choiceContainer != null)
        {
            if (choiceButton1 == null)
            {
                GameObject buttonObject = FindDeepObject(choiceContainer.transform, "choice_Button1");
                if (buttonObject != null) choiceButton1 = buttonObject.GetComponent<Button>();
            }

            if (choiceButton2 == null)
            {
                GameObject buttonObject = FindDeepObject(choiceContainer.transform, "choice_Button2");
                if (buttonObject != null) choiceButton2 = buttonObject.GetComponent<Button>();
            }

            // The bedroom only had one choice button: make the second one from the first.
            if (choiceButton2 == null && choiceButton1 != null)
            {
                GameObject copy = Instantiate(choiceButton1.gameObject, choiceButton1.transform.parent);
                copy.name = "choice_Button2";
                copy.transform.SetSiblingIndex(choiceButton1.transform.GetSiblingIndex() + 1);
                choiceButton2 = copy.GetComponent<Button>();

                // Without a layout group the copy would sit on top of Button 1, so move it below.
                if (choiceButton1.transform.parent.GetComponent<LayoutGroup>() == null)
                {
                    RectTransform firstRect = choiceButton1.transform as RectTransform;
                    RectTransform copyRect = copy.transform as RectTransform;
                    if (firstRect != null && copyRect != null)
                        copyRect.anchoredPosition = firstRect.anchoredPosition - new Vector2(0f, firstRect.rect.height * Mathf.Abs(firstRect.localScale.y) * 1.3f);
                }
            }

            if (choiceButton1Text == null && choiceButton1 != null)
                choiceButton1Text = choiceButton1.GetComponentInChildren<TMP_Text>(true);

            if (choiceButton2Text == null && choiceButton2 != null)
                choiceButton2Text = choiceButton2.GetComponentInChildren<TMP_Text>(true);
        }

        if (dialogueBox == null)
            return;

        if (charNameText == null)
        {
            GameObject nameObject = FindDeepObject(dialogueBox.transform, "CharacterName");
            if (nameObject != null) charNameText = nameObject.GetComponent<TMP_Text>();
        }

        if (messageInputText == null)
        {
            GameObject messageObject = FindDeepObject(dialogueBox.transform, "MessageOutput");
            if (messageObject != null) messageInputText = messageObject.GetComponent<TMP_Text>();
        }

        if (nextButton == null)
        {
            Button foundButton = dialogueBox.GetComponentInChildren<Button>(true);
            if (foundButton != null) nextButton = foundButton.gameObject;
        }
    }

    private static GameObject FindDeepObject(Transform parent, string objectName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == objectName)
                return child.gameObject;

            GameObject found = FindDeepObject(child, objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    // Matches AudioSources by a word in their object name or their clip name.
    // Everything is stopped first: the rooster, birds and alarm were copied from the morning bedroom.
    private void FindSounds()
    {
        foreach (AudioSource audioSource in FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
        {
            if (fanAudioSource == null && SoundMatches(audioSource, "fan"))
                fanAudioSource = audioSource;
            else if (heartbeatAudioSource == null && SoundMatches(audioSource, "beat"))
                heartbeatAudioSource = audioSource;
            else if (shakeAudioSource == null && SoundMatches(audioSource, "shake"))
                shakeAudioSource = audioSource;
            else if (godlyAudioSource == null && SoundMatches(audioSource, "godly"))
                godlyAudioSource = audioSource;
            else if (clickAudioSource == null && SoundMatches(audioSource, "click"))
                clickAudioSource = audioSource;
            else if (ambientAudioSource == null && (SoundMatches(audioSource, "ambient") || SoundMatches(audioSource, "night")))
                ambientAudioSource = audioSource;

            audioSource.playOnAwake = false;
            audioSource.Stop();
        }

        if (fanAudioSource != null) { fanVolume = fanAudioSource.volume; fanAudioSource.loop = true; }
        if (ambientAudioSource != null) { ambientVolume = ambientAudioSource.volume; ambientAudioSource.loop = true; }
        if (heartbeatAudioSource != null) { heartbeatVolume = heartbeatAudioSource.volume; heartbeatAudioSource.loop = true; }
        if (godlyAudioSource != null) { godlyVolume = godlyAudioSource.volume; godlyAudioSource.loop = true; }
        if (shakeAudioSource != null) shakeAudioSource.loop = false;
        if (clickAudioSource != null) clickAudioSource.loop = false;
    }

    private static bool SoundMatches(AudioSource audioSource, string keyword)
    {
        if (audioSource.gameObject.name.ToLowerInvariant().Contains(keyword))
            return true;

        return audioSource.clip != null && audioSource.clip.name.ToLowerInvariant().Contains(keyword);
    }

    private static void PlayEffect(AudioSource effect)
    {
        if (effect != null && effect.clip != null)
            effect.PlayOneShot(effect.clip);
    }

    // Starts the sound if needed and slides its volume; stops it when it reaches silence.
    private IEnumerator FadeAudio(AudioSource audioSource, float toVolume, float fadeDuration)
    {
        if (audioSource == null || audioSource.clip == null)
            yield break;

        float fromVolume = audioSource.isPlaying ? audioSource.volume : 0f;
        audioSource.volume = fromVolume;
        if (!audioSource.isPlaying && toVolume > 0f)
            audioSource.Play();

        float duration = Mathf.Max(fadeDuration, 0.01f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(fromVolume, toVolume, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        audioSource.volume = toVolume;
        if (toVolume <= 0f)
            audioSource.Stop();
    }

    private static void StopAudio(AudioSource audioSource)
    {
        if (audioSource != null)
            audioSource.Stop();
    }

    IEnumerator StartCutscene()
    {
        // The fan hums in the dark before the room appears.
        StartCoroutine(FadeAudio(fanAudioSource, fanVolume, 1f));
        StartCoroutine(FadeAudio(ambientAudioSource, ambientVolume, 1.5f));
        yield return new WaitForSeconds(1f);
        yield return FadeFromBlack(2f);
        yield return new WaitForSeconds(0.5f);

        List<A2BroomUwiDialogueLine> reflectionLines = new List<A2BroomUwiDialogueLine>(openingLines);
        reflectionLines.AddRange(BuildReflectionLines());
        reflectionLines.AddRange(sleepLines);

        StartLines(reflectionLines, () => StartLines(visionLines, ShowWhyGoodChoice));
    }

    private void ShowWhyGoodChoice()
    {
        const string question = "God asked: \"Why did you do good today?\"";

        ShowChoice(
            forHeavenChoiceText, () =>
            {
                PlayerDatabase.RecordChoice(WhyGoodChoiceId, question, forHeavenChoiceText, 0, ForHeavenAnswer);
                StartLines(forHeavenLines, () => StartLines(finalLines, () => StartCoroutine(EndAct())));
            },
            forLoveChoiceText, () =>
            {
                PlayerDatabase.RecordChoice(WhyGoodChoiceId, question, forLoveChoiceText, 0, ForLoveAnswer);
                StartLines(forLoveLines, () => StartLines(finalLines, () => StartCoroutine(EndAct())));
            });
    }

    // The FadeIn Animator is turned off so the script decides when the screen clears.
    private void HoldBlackScreen()
    {
        if (fadeScreenIn == null)
            return;

        fadeScreenIn.SetActive(true);

        Animator fadeAnimator = fadeScreenIn.GetComponent<Animator>();
        if (fadeAnimator != null)
            fadeAnimator.enabled = false;

        Image fadeImage = fadeScreenIn.GetComponent<Image>();
        if (fadeImage != null)
            fadeImage.color = Color.black;
    }

    private IEnumerator FadeFromBlack(float fadeDuration)
    {
        if (fadeScreenIn == null)
            yield break;

        Image fadeImage = fadeScreenIn.GetComponent<Image>();
        if (fadeImage != null)
            yield return FadeImageAlpha(fadeImage, 1f, 0f, fadeDuration);

        fadeScreenIn.SetActive(false);
    }

    // Plays a list of lines; when the player presses Next on the last one, onFinished runs.
    private void StartLines(List<A2BroomUwiDialogueLine> lines, System.Action onFinished)
    {
        currentLines = lines;
        currentLineIndex = 0;
        onLinesFinished = onFinished;

        if (currentLines.Count > 0)
            StartCoroutine(PlayLine(currentLineIndex));
        else
            FinishLines();
    }

    private void FinishLines()
    {
        System.Action next = onLinesFinished;
        onLinesFinished = null;
        next?.Invoke();
    }

    public void NextButton()
    {
        if (isPlayingAction || waitingForChoice || isEnding)
            return;

        PlayEffect(clickAudioSource);

        if (isTyping)
        {
            CompleteTypingInstantly();
            return;
        }

        if (currentLineIndex + 1 < currentLines.Count)
        {
            currentLineIndex++;
            StartCoroutine(PlayLine(currentLineIndex));
            return;
        }

        FinishLines();
    }

    private void ShowChoice(string firstLabel, System.Action firstChosen, string secondLabel, System.Action secondChosen)
    {
        waitingForChoice = true;
        if (nextButton != null) nextButton.SetActive(false);
        if (choiceContainer != null) choiceContainer.SetActive(true);

        SetupChoiceButton(choiceButton1, choiceButton1Text, firstLabel, firstChosen);
        SetupChoiceButton(choiceButton2, choiceButton2Text, secondLabel, secondChosen);
    }

    private void SetupChoiceButton(Button button, TMP_Text buttonText, string label, System.Action onChosen)
    {
        if (button == null)
            return;

        button.gameObject.SetActive(true);
        if (buttonText != null) buttonText.text = label;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            if (!waitingForChoice)
                return;

            waitingForChoice = false;
            PlayEffect(clickAudioSource);
            if (choiceContainer != null) choiceContainer.SetActive(false);
            onChosen();
        });
    }

    private IEnumerator PlayLine(int index)
    {
        A2BroomUwiDialogueLine line = currentLines[index];

        if (line.actionBefore != A2BroomUwiAction.None)
        {
            isPlayingAction = true;
            if (nextButton != null) nextButton.SetActive(false);
            yield return PerformAction(line.actionBefore);
            isPlayingAction = false;
        }

        DisplayLine(line);
    }

    private IEnumerator PerformAction(A2BroomUwiAction action)
    {
        switch (action)
        {
            case A2BroomUwiAction.LieDown:
                yield return LieDown();
                break;

            case A2BroomUwiAction.LordAppears:
                yield return LordAppears();
                break;

            case A2BroomUwiAction.ShowTally:
                yield return ShowTally();
                break;

            case A2BroomUwiAction.HideTally:
                yield return HideTally();
                break;

            case A2BroomUwiAction.Pause:
                if (dialogueBox != null) dialogueBox.SetActive(false);
                yield return new WaitForSeconds(1.2f);
                break;

            case A2BroomUwiAction.FinalShake:
                yield return FinalShake();
                break;
        }
    }

    // The player lies down: the fan fades, the room sinks into darkness and the heart starts beating.
    private IEnumerator LieDown()
    {
        if (dialogueBox != null) dialogueBox.SetActive(false);

        StartCoroutine(FadeAudio(fanAudioSource, 0f, sleepFadeDuration));
        StartCoroutine(FadeAudio(ambientAudioSource, 0f, sleepFadeDuration));
        yield return TintBackground(Color.black, sleepFadeDuration);
        yield return new WaitForSeconds(1.5f);

        StartCoroutine(FadeAudio(heartbeatAudioSource, heartbeatVolume * 0.6f, 1.5f));
        yield return new WaitForSeconds(1.5f);
    }

    // God fades in from the darkness and rises gently into place.
    private IEnumerator LordAppears()
    {
        if (dialogueBox != null) dialogueBox.SetActive(false);
        StartCoroutine(FadeAudio(godlyAudioSource, godlyVolume, lordAppearDuration));

        if (charLord == null)
            yield break;

        Transform lordTransform = charLord.transform;
        Vector3 startPosition = lordRestPosition - new Vector3(0f, lordRiseDistance, 0f);
        Color startColor = lordBaseColor;
        startColor.a = 0f;

        lordTransform.localPosition = startPosition;
        SetLordColor(startColor);
        charLord.SetActive(true);

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

    // The player's own good points appear, big, at the top of the screen.
    private IEnumerator ShowTally()
    {
        if (dialogueBox != null) dialogueBox.SetActive(false);

        Transform canvasTransform = dialogueBox != null ? dialogueBox.transform.parent : null;
        if (canvasTransform == null)
            yield break;

        int points = PlayerDatabase.GoodPoints;
        tallyText = CreateTopText(canvasTransform, "GoodPointsTally", $"Good Points\n<size=160%>{points}</size>");
        yield return FadeTextAlpha(tallyText, 0f, 1f, 1.2f);
        yield return new WaitForSeconds(1.2f);
    }

    private IEnumerator HideTally()
    {
        if (tallyText == null)
            yield break;

        if (dialogueBox != null) dialogueBox.SetActive(false);
        yield return FadeTextAlpha(tallyText, tallyText.alpha, 0f, 1f);
        Destroy(tallyText.gameObject);
        tallyText = null;
        yield return new WaitForSeconds(0.3f);
    }

    // The last question shakes everything; then a sudden cut to black and silence.
    private IEnumerator FinalShake()
    {
        PlayEffect(shakeAudioSource);

        Transform boxTransform = dialogueBox != null ? dialogueBox.transform : null;
        Vector3 boxRestPosition = boxTransform != null ? boxTransform.localPosition : Vector3.zero;
        bool lordVisible = charLord != null && charLord.activeInHierarchy;

        float duration = Mathf.Max(finalShakeDuration, 0.01f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float strength = finalShakeStrength * (1f - Mathf.Clamp01(elapsed / duration));
            Vector3 offset = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * strength;

            if (boxTransform != null) boxTransform.localPosition = boxRestPosition + offset;
            if (lordVisible) charLord.transform.localPosition = lordRestPosition + offset * 0.5f;
            yield return null;
        }

        if (boxTransform != null) boxTransform.localPosition = boxRestPosition;
        if (lordVisible) charLord.transform.localPosition = lordRestPosition;
    }

    private IEnumerator TintBackground(Color tint, float duration)
    {
        if (backgroundRenderer == null)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        Color startColor = backgroundRenderer.color;
        Color targetColor = MultiplyColor(backgroundBaseColor, tint);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            backgroundRenderer.color = Color.Lerp(startColor, targetColor, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        backgroundRenderer.color = targetColor;
    }

    private static Color MultiplyColor(Color baseColor, Color tint)
    {
        return new Color(baseColor.r * tint.r, baseColor.g * tint.g, baseColor.b * tint.b, baseColor.a);
    }

    private void DisplayLine(A2BroomUwiDialogueLine line)
    {
        if (dialogueBox != null) dialogueBox.SetActive(true);
        if (nextButton != null) nextButton.SetActive(true);
        if (choiceContainer != null) choiceContainer.SetActive(false);

        if (charNameText != null)
            charNameText.text = line.speakerName;

        UpdateSpeakerEffects(line.speakerName);

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        float speed = typingSpeed;
        if (line.speakerName == lordName)
            speed = lordTypingSpeed;
        else if (line.speakerName == playerName && line.message.StartsWith("("))
            speed = slowTypingSpeed;

        typingCoroutine = StartCoroutine(TypeText(line.message, speed));
    }

    // God stays bright and glows bigger when speaking; dims a little while You is speaking.
    private void UpdateSpeakerEffects(string speakerName)
    {
        if (charLord == null || !charLord.activeInHierarchy)
            return;

        bool lordSpeaking = speakerName == lordName;
        Color targetColor = lordSpeaking ? lordBaseColor : MultiplyColor(lordBaseColor, lordDarkenedTint);

        if (lordTintCoroutine != null)
            StopCoroutine(lordTintCoroutine);
        lordTintCoroutine = StartCoroutine(TintLord(targetColor));

        if (lordPulseCoroutine != null)
        {
            StopCoroutine(lordPulseCoroutine);
            charLord.transform.localScale = lordRestScale;
        }

        if (lordSpeaking)
            lordPulseCoroutine = StartCoroutine(SpeakPulse());
    }

    private IEnumerator TintLord(Color targetColor)
    {
        Color startColor = GetLordColor();
        float duration = Mathf.Max(tintDuration, 0.01f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetLordColor(Color.Lerp(startColor, targetColor, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetLordColor(targetColor);
    }

    private IEnumerator SpeakPulse()
    {
        Transform lordTransform = charLord.transform;
        float duration = Mathf.Max(speakPulseDuration, 0.01f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float bump = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
            lordTransform.localScale = lordRestScale * Mathf.Lerp(1f, speakPulseScale, bump);
            yield return null;
        }

        lordTransform.localScale = lordRestScale;
        lordPulseCoroutine = null;
    }

    // Lord is a SpriteRenderer in this scene, but a UI Image works too.
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

    private IEnumerator TypeText(string targetMessage, float speed)
    {
        isTyping = true;
        messageInputText.text = targetMessage;
        messageInputText.maxVisibleCharacters = 0;
        messageInputText.ForceMeshUpdate();

        int totalCharacters = messageInputText.textInfo.characterCount;

        for (int i = 0; i <= totalCharacters; i++)
        {
            messageInputText.maxVisibleCharacters = i;
            CharacterVoice.Speak(charNameText != null ? charNameText.text : null, messageInputText, i);
            yield return new WaitForSeconds(DialoguePacing.Delay(messageInputText, i, speed));
        }

        isTyping = false;
    }

    private void CompleteTypingInstantly()
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        messageInputText.maxVisibleCharacters = messageInputText.textInfo.characterCount;
        CharacterVoice.Stop();
        isTyping = false;
    }

    // The question is left hanging: a hard shake, a sudden cut to black, silence, then the results.
    private IEnumerator EndAct()
    {
        isEnding = true;
        if (nextButton != null) nextButton.SetActive(false);

        yield return FinalShake();

        Image blackScreen = PrepareFadeOutImage();
        if (blackScreen != null)
            blackScreen.color = Color.black;

        if (dialogueBox != null) dialogueBox.SetActive(false);
        if (charLord != null) charLord.SetActive(false);
        StopAudio(heartbeatAudioSource);
        StopAudio(godlyAudioSource);
        StopAudio(fanAudioSource);
        StopAudio(ambientAudioSource);

        yield return new WaitForSeconds(2.5f);
        ShowActResults();
    }

    private void ShowActResults()
    {
        PlayerDatabase.MarkActCompleted(actNumber);

        Transform canvasTransform = fadeOutScreen != null ? fadeOutScreen.transform.parent : null;
        if (canvasTransform == null && dialogueBox != null)
            canvasTransform = dialogueBox.transform.parent;
        if (canvasTransform == null)
            return;

        // The scene was saved before Act 3 existed, so an empty value means the default.
        if (string.IsNullOrEmpty(nextActSceneName))
            nextActSceneName = "A3_Broom";

        TMP_FontAsset font = messageInputText != null ? messageInputText.font : null;
        ActResultsScreen.Show(canvasTransform, choiceContainer, font, actResultsTitle, mainMenuSceneName, nextActSceneName);
    }

    // FadeOut may carry an Animator (copied from FadeIn) that would fight the script, so it is turned off.
    private Image PrepareFadeOutImage()
    {
        if (fadeOutScreen == null)
            return null;

        fadeOutScreen.SetActive(true);

        Animator fadeAnimator = fadeOutScreen.GetComponent<Animator>();
        if (fadeAnimator != null)
            fadeAnimator.enabled = false;

        CanvasGroup canvasGroup = fadeOutScreen.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        Image fadeImage = fadeOutScreen.GetComponent<Image>();
        if (fadeImage != null)
            fadeImage.color = new Color(0f, 0f, 0f, 0f);

        return fadeImage;
    }

    private TMP_Text CreateTopText(Transform canvasTransform, string objectName, string message)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textTransform = textObject.GetComponent<RectTransform>();
        textTransform.SetParent(canvasTransform, false);
        textTransform.anchorMin = new Vector2(0.2f, 0.6f);
        textTransform.anchorMax = new Vector2(0.8f, 0.95f);
        textTransform.offsetMin = Vector2.zero;
        textTransform.offsetMax = Vector2.zero;
        textTransform.SetAsLastSibling();

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (messageInputText != null)
            text.font = messageInputText.font;
        text.fontSize = 40f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.alpha = 0f;
        text.raycastTarget = false;
        text.outlineWidth = 0.2f;
        text.outlineColor = Color.black;
        text.text = message;
        return text;
    }

    private static IEnumerator FadeTextAlpha(TMP_Text text, float fromAlpha, float toAlpha, float fadeDuration)
    {
        float duration = Mathf.Max(fadeDuration, 0.01f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            text.alpha = Mathf.Lerp(fromAlpha, toAlpha, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }

        text.alpha = toAlpha;
    }

    private static IEnumerator FadeImageAlpha(Image image, float fromAlpha, float toAlpha, float fadeDuration)
    {
        Color color = image.color;
        float duration = Mathf.Max(fadeDuration, 0.01f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(fromAlpha, toAlpha, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            image.color = color;
            yield return null;
        }

        color.a = toAlpha;
        image.color = color;
    }
}
