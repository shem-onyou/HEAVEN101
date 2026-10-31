using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public enum A1ActOneAction
{
    None,
    LordAppears,
    WakeUpShake
}

[System.Serializable]
public class ChoiceOption
{
    public string choiceText;
    public int nextDialogueIndex;
    public int points;
}

[System.Serializable]
public class DialogueLine
{
    public A1ActOneAction actionBefore;
    public string speakerName;
    [TextArea(2, 5)] public string message;

    [Header("Player Choice Options (Optional)")]
    public List<ChoiceOption> choices;

    [Tooltip("Ends the scene after this line (the last line of each choice path).")]
    public bool endsScene;

    public DialogueLine(string name, string msg, List<ChoiceOption> choiceOptions = null,
        A1ActOneAction action = A1ActOneAction.None, bool endsScene = false)
    {
        speakerName = name;
        message = msg;
        choices = choiceOptions;
        actionBefore = action;
        this.endsScene = endsScene;
    }
}

public class SceneController : MonoBehaviour
{
    [Header("Cutscene Objects (auto-found by name if empty)")]
    public GameObject fadeScreenIn;
    public GameObject fadeOutScreen;
    public GameObject charLord;
    public GameObject dialogueBox;
    public GameObject nextButton;
    [SerializeField] private AudioSource teleportationAudioSource;

    [Header("Sound Effects (auto-found by name if empty)")]
    [Tooltip("Found automatically if an AudioSource's object name contains \"Godly\".")]
    [SerializeField] private AudioSource ambientAudioSource;
    [Tooltip("The ambient stays silent during the intro and fades in over this many seconds once the screen is clear.")]
    [SerializeField] private float ambientFadeInDuration = 1.5f;
    [Tooltip("Found automatically if an AudioSource's object name contains \"click\".")]
    [SerializeField] private AudioSource clickAudioSource;
    [Tooltip("Found automatically if an AudioSource's object name contains \"shake\".")]
    [SerializeField] private AudioSource shakeAudioSource;

    [Header("Choice System Objects (auto-found by name if empty)")]
    public GameObject choiceContainer;
    public Button choiceButton1;
    public Button choiceButton2;
    public TMP_Text choiceButton1Text;
    public TMP_Text choiceButton2Text;

    [Header("Text UI References (auto-found by name if empty)")]
    public TMP_Text charNameText;
    public TMP_Text messageInputText;

    [Header("Typewriter Speed Settings")]
    [SerializeField] private float typingSpeed = 0.03f;

    [Header("Intro (text on the black screen before the scene)")]
    [TextArea(1, 3)]
    public List<string> introLines = new List<string>
    {
        "You are asleep...",
        "Somewhere between dreaming and waking, your spirit drifts away from your body.",
        "And when you open your eyes... you are somewhere you have never been."
    };
    [SerializeField] private float introTextFadeDuration = 1f;
    [SerializeField] private float introLineHoldDuration = 2.5f;
    [SerializeField] private float introFontSize = 32f;
    [Tooltip("How long the black screen takes to clear after the intro text.")]
    [SerializeField] private float fadeFromBlackDuration = 2.5f;

    [Header("Lord")]
    [SerializeField] private string lordName = "God";
    [SerializeField] private float lordAppearDuration = 1.5f;
    [SerializeField] private float lordRiseDistance = 30f;

    [Header("Speaking Effects")]
    [Tooltip("Lord's tint while You is speaking. Kept lighter than the classmates' so God never looks too dark.")]
    [SerializeField] private Color darkenedTint = new Color(0.6f, 0.6f, 0.6f, 1f);
    [SerializeField] private float tintDuration = 0.2f;
    [Tooltip("God glows a little bigger when speaking instead of jittering.")]
    [SerializeField] private float speakPulseScale = 1.04f;
    [SerializeField] private float speakPulseDuration = 0.4f;
    [SerializeField] private float wakeUpShakeStrength = 12f;
    [SerializeField] private float wakeUpShakeDuration = 0.6f;

    [Header("Scene Transition")]
    [SerializeField] private string nextSceneName = "A1_Bedroom";
    [Tooltip("Shortest time the screen takes to turn white during the teleport sound.")]
    [SerializeField] private float whiteFadeDuration = 1.5f;

    [Header("Dialogue Sequence")]
    public List<DialogueLine> dialogueList = new List<DialogueLine>();

    private int currentLineIndex = 0;
    private Coroutine typingCoroutine;
    private bool isTyping;
    private bool isPlayingAction;
    private bool isEnding;
    private float ambientVolume = 1f;

    private SpriteRenderer lordSprite;
    private Graphic lordGraphic;
    private Vector3 lordRestPosition;
    private Vector3 lordRestScale;
    private Color lordBaseColor = Color.white;
    private Coroutine lordTintCoroutine;
    private Coroutine lordPulseCoroutine;

    void Start()
    {
        if (dialogueList.Count == 0)
        {
            dialogueList.Add(new DialogueLine("You", "My head... Where am I? The last thing I remember is...")); // 0
            dialogueList.Add(new DialogueLine("You", "You... Who are you? No, what are you?", action: A1ActOneAction.LordAppears)); // 1
            dialogueList.Add(new DialogueLine(lordName, "I am the One who spoke the stars into being and breathed life into every soul. You know me as God.")); // 2
            dialogueList.Add(new DialogueLine("You", "God? Then... is this Heaven?")); // 3
            dialogueList.Add(new DialogueLine("You", "Wait... I was somewhere else, wasn't I? Why can't I remember how I got here? What happened to me?")); // 4
            dialogueList.Add(new DialogueLine(lordName, "Take a breath, child. Your body still lives, but you have been brought here for a reason.")); // 5
            dialogueList.Add(new DialogueLine(lordName, "Your body sleeps. Your spirit has crossed beyond it, and now you stand outside the gates.")); // 6
            dialogueList.Add(new DialogueLine(lordName, "This is a vision of the Day of Judgment. One day, every soul will answer for the life it lived.")); // 7
            dialogueList.Add(new DialogueLine("You", "Every soul...? Including me?")); // 8
            dialogueList.Add(new DialogueLine(lordName, "Especially you.")); // 9

            // Choice Question (Index 10)
            List<ChoiceOption> questionChoices = new List<ChoiceOption>()
            {
                new ChoiceOption { choiceText = "I don't know...", nextDialogueIndex = 11 },
                new ChoiceOption { choiceText = "Because I've been good.", nextDialogueIndex = 17 }
            };
            dialogueList.Add(new DialogueLine(lordName, "Tell me, child. Why should I open the gates to you?", questionChoices)); // 10

            // Path A: If Player chooses "I don't know..."
            dialogueList.Add(new DialogueLine("You", "I... I don't know.")); // 11
            dialogueList.Add(new DialogueLine(lordName, "You don't know? Then tell me, child, how have you lived your life?")); // 12
            dialogueList.Add(new DialogueLine("You", "I try to be a good person. I help people, I go to school, I listen to Nanay... Isn't that enough?")); // 13
            dialogueList.Add(new DialogueLine(lordName, "Is it? Many have stood where you stand now, holding that very same answer.")); // 14
            dialogueList.Add(new DialogueLine(lordName, "Judgment is near. Search your heart before that day comes. When it does, no more time will be given.")); // 15
            dialogueList.Add(new DialogueLine(lordName, "WAKE UP! And live your remaining days wisely.", action: A1ActOneAction.WakeUpShake, endsScene: true)); // 16

            // Path B: If Player chooses "Because I've been good."
            dialogueList.Add(new DialogueLine("You", "Because I've been good! I obey my parents, I help my friends, I never hurt anyone. My good deeds should be enough to save me.")); // 17
            dialogueList.Add(new DialogueLine(lordName, "Good deeds... Is that what you believe opens these gates?")); // 18
            dialogueList.Add(new DialogueLine("You", "Isn't it? I've done more good than bad. That has to count for something.")); // 19
            dialogueList.Add(new DialogueLine(lordName, "Then I will give you time, child. Look closely at the life you live, and you will find your answer.")); // 20
            dialogueList.Add(new DialogueLine(lordName, "What you call a dream is a warning. The Day of Judgment is coming, and every choice you make will matter.")); // 21
            dialogueList.Add(new DialogueLine(lordName, "WAKE UP! Seek the answer.", action: A1ActOneAction.WakeUpShake, endsScene: true)); // 22
        }

        FindSceneReferences();

        if (choiceContainer != null) choiceContainer.SetActive(false);
        if (fadeOutScreen != null) fadeOutScreen.SetActive(false);
        if (dialogueBox != null) dialogueBox.SetActive(false);
        HoldBlackScreen();

        // The ambient plays on awake in the scene; keep it silent until the intro is over.
        if (ambientAudioSource != null)
        {
            ambientVolume = ambientAudioSource.volume;
            ambientAudioSource.Stop();
        }

        if (charLord != null)
        {
            lordSprite = charLord.GetComponent<SpriteRenderer>();
            lordGraphic = charLord.GetComponent<Graphic>();
            lordRestPosition = charLord.transform.localPosition;
            lordRestScale = charLord.transform.localScale;
            lordBaseColor = GetLordColor();
            charLord.SetActive(false);
        }

        // The Next button already calls NextButton() through its OnClick in the Inspector,
        // so no listener is added here (it would run twice per click).

        StartCoroutine(StartCutscene());
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

        if (teleportationAudioSource == null)
        {
            GameObject teleportationObject = GameObject.Find("Teleportation");
            if (teleportationObject != null)
                teleportationAudioSource = teleportationObject.GetComponent<AudioSource>();
        }

        FindSoundEffects();

        if (canvasTransform == null)
            return;

        if (dialogueBox == null) dialogueBox = FindDeepObject(canvasTransform, "DialogueBox");
        if (fadeScreenIn == null) fadeScreenIn = FindDeepObject(canvasTransform, "FadeIn");
        if (fadeOutScreen == null) fadeOutScreen = FindDeepObject(canvasTransform, "FadeOut");
        if (charLord == null) charLord = FindDeepObject(canvasTransform, "Lord");
        if (choiceContainer == null) choiceContainer = FindDeepObject(canvasTransform, "choice_Container");

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

    // Matches AudioSources by a word in their object name OR their clip name,
    // e.g. an object called "Audio Source" holding the "shake-sound" clip still counts as the shake.
    private void FindSoundEffects()
    {
        AudioSource[] audioSources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include);

        foreach (AudioSource audioSource in audioSources)
        {
            if (audioSource == teleportationAudioSource)
                continue;

            if (ambientAudioSource == null && SoundMatches(audioSource, "godly"))
                ambientAudioSource = audioSource;
            else if (clickAudioSource == null && SoundMatches(audioSource, "click"))
                clickAudioSource = audioSource;
            else if (shakeAudioSource == null && SoundMatches(audioSource, "shake"))
                shakeAudioSource = audioSource;
        }

        // One-shot effects must not play by themselves when the scene starts.
        foreach (AudioSource effect in new[] { clickAudioSource, shakeAudioSource })
        {
            if (effect == null)
                continue;

            effect.playOnAwake = false;
            effect.loop = false;
            effect.Stop();
        }
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

    IEnumerator StartCutscene()
    {
        yield return PlayIntro();
        yield return FadeFromBlack();

        if (ambientAudioSource != null)
            StartCoroutine(FadeInAudio(ambientAudioSource, ambientVolume, ambientFadeInDuration));

        yield return new WaitForSeconds(0.5f);

        if (dialogueList.Count > 0)
            StartCoroutine(PlayLine(currentLineIndex));
    }

    // The FadeIn Animator would clear the screen on its own after 3 seconds,
    // so it is turned off and the screen stays fully black until the intro text is done.
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

    // Shows each intro line in the middle of the black screen, one after another.
    private IEnumerator PlayIntro()
    {
        if (introLines == null || introLines.Count == 0)
            yield break;

        yield return new WaitForSeconds(1f);

        TMP_Text introText = CreateIntroText();
        if (introText == null)
            yield break;

        foreach (string line in introLines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            introText.text = line;
            yield return FadeTextAlpha(introText, 0f, 1f, introTextFadeDuration);
            yield return new WaitForSeconds(introLineHoldDuration);
            yield return FadeTextAlpha(introText, 1f, 0f, introTextFadeDuration);
            yield return new WaitForSeconds(0.3f);
        }

        Destroy(introText.gameObject);
        yield return new WaitForSeconds(0.5f);
    }

    private TMP_Text CreateIntroText()
    {
        Transform canvasTransform = fadeScreenIn != null ? fadeScreenIn.transform.parent : null;
        if (canvasTransform == null && dialogueBox != null)
            canvasTransform = dialogueBox.transform.parent;
        if (canvasTransform == null)
            return null;

        GameObject textObject = new GameObject("IntroText", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textTransform = textObject.GetComponent<RectTransform>();
        textTransform.SetParent(canvasTransform, false);
        textTransform.anchorMin = new Vector2(0.1f, 0f);
        textTransform.anchorMax = new Vector2(0.9f, 1f);
        textTransform.offsetMin = Vector2.zero;
        textTransform.offsetMax = Vector2.zero;
        textTransform.SetAsLastSibling();

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (messageInputText != null)
            text.font = messageInputText.font;
        text.fontSize = introFontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.alpha = 0f;
        text.raycastTarget = false;
        return text;
    }

    private static IEnumerator FadeTextAlpha(TMP_Text text, float fromAlpha, float toAlpha, float fadeDuration)
    {
        float duration = Mathf.Max(fadeDuration, 0.01f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            text.alpha = Mathf.Lerp(fromAlpha, toAlpha, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        text.alpha = toAlpha;
    }

    private IEnumerator FadeFromBlack()
    {
        if (fadeScreenIn == null)
            yield break;

        Image fadeImage = fadeScreenIn.GetComponent<Image>();
        if (fadeImage != null)
        {
            float duration = Mathf.Max(fadeFromBlackDuration, 0.01f);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                fadeImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(1f, 0f, Mathf.SmoothStep(0f, 1f, elapsed / duration)));
                yield return null;
            }
        }

        fadeScreenIn.SetActive(false);
    }

    public void NextButton()
    {
        if (isPlayingAction || isEnding)
            return;

        DialogueLine currentLine = dialogueList[currentLineIndex];

        // Waiting for the player to pick a choice.
        if (!isTyping && HasChoices(currentLine))
            return;

        PlayEffect(clickAudioSource);

        if (isTyping)
        {
            CompleteTypingInstantly();
            return;
        }

        if (currentLine.endsScene || currentLineIndex + 1 >= dialogueList.Count)
        {
            StartCoroutine(EndScene());
            return;
        }

        currentLineIndex++;
        StartCoroutine(PlayLine(currentLineIndex));
    }

    private static bool HasChoices(DialogueLine line)
    {
        return line.choices != null && line.choices.Count > 0;
    }

    private IEnumerator PlayLine(int index)
    {
        DialogueLine line = dialogueList[index];

        if (line.actionBefore != A1ActOneAction.None)
        {
            isPlayingAction = true;
            if (nextButton != null) nextButton.SetActive(false);
            yield return PerformAction(line.actionBefore);
            isPlayingAction = false;
        }

        DisplayLine(line);
    }

    private IEnumerator PerformAction(A1ActOneAction action)
    {
        switch (action)
        {
            case A1ActOneAction.LordAppears:
                yield return LordAppears();
                break;

            case A1ActOneAction.WakeUpShake:
                // The shake runs while the "WAKE UP!" line types out, so the action itself does not wait.
                PlayEffect(shakeAudioSource);
                StartCoroutine(WakeUpShake());
                break;
        }
    }

    // God fades in from light and rises gently into place.
    private IEnumerator LordAppears()
    {
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
    }

    // Shakes God and the dialogue box hard, then settles, for the "WAKE UP!" lines.
    private IEnumerator WakeUpShake()
    {
        Transform boxTransform = dialogueBox != null ? dialogueBox.transform : null;
        Vector3 boxRestPosition = boxTransform != null ? boxTransform.localPosition : Vector3.zero;
        bool lordVisible = charLord != null && charLord.activeInHierarchy;

        float duration = Mathf.Max(wakeUpShakeDuration, 0.01f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float strength = wakeUpShakeStrength * (1f - Mathf.Clamp01(elapsed / duration));
            Vector3 offset = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * strength;

            if (boxTransform != null) boxTransform.localPosition = boxRestPosition + offset;
            if (lordVisible) charLord.transform.localPosition = lordRestPosition + offset * 0.5f;
            yield return null;
        }

        if (boxTransform != null) boxTransform.localPosition = boxRestPosition;
        if (lordVisible) charLord.transform.localPosition = lordRestPosition;
    }

    private void DisplayLine(DialogueLine line)
    {
        if (dialogueBox != null) dialogueBox.SetActive(true);
        if (nextButton != null) nextButton.SetActive(true);
        if (choiceContainer != null) choiceContainer.SetActive(false);

        if (charNameText != null)
            charNameText.text = line.speakerName;

        UpdateSpeakerEffects(line.speakerName);

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        typingCoroutine = StartCoroutine(TypeText(line.message));
    }

    // God stays bright and glows bigger when speaking; dims a little while You is speaking.
    private void UpdateSpeakerEffects(string speakerName)
    {
        if (charLord == null || !charLord.activeInHierarchy)
            return;

        bool lordSpeaking = speakerName == lordName;
        Color targetColor = lordSpeaking ? lordBaseColor : Darken(lordBaseColor);

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

    private Color Darken(Color baseColor)
    {
        return new Color(baseColor.r * darkenedTint.r, baseColor.g * darkenedTint.g, baseColor.b * darkenedTint.b, baseColor.a);
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

    private IEnumerator TypeText(string targetMessage)
    {
        isTyping = true;
        messageInputText.text = targetMessage;
        messageInputText.maxVisibleCharacters = 0;
        messageInputText.ForceMeshUpdate();

        int totalCharacters = messageInputText.textInfo.characterCount;

        for (int i = 0; i <= totalCharacters; i++)
        {
            messageInputText.maxVisibleCharacters = i;
            yield return new WaitForSeconds(typingSpeed);
        }

        OnTypingFinished();
    }

    private void CompleteTypingInstantly()
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        messageInputText.maxVisibleCharacters = messageInputText.textInfo.characterCount;
        OnTypingFinished();
    }

    private void OnTypingFinished()
    {
        isTyping = false;

        DialogueLine line = dialogueList[currentLineIndex];
        if (HasChoices(line))
            ShowChoices(line.choices);
    }

    private void ShowChoices(List<ChoiceOption> choices)
    {
        if (nextButton != null) nextButton.SetActive(false);
        if (choiceContainer != null) choiceContainer.SetActive(true);

        DialogueLine questionLine = dialogueList[currentLineIndex];
        string choiceId = $"{SceneManager.GetActiveScene().name}_line{currentLineIndex}";

        if (choices.Count > 0 && choiceButton1 != null)
        {
            choiceButton1.gameObject.SetActive(true);
            if (choiceButton1Text != null) choiceButton1Text.text = choices[0].choiceText;
            choiceButton1.onClick.RemoveAllListeners();
            ChoiceOption option0 = choices[0];
            choiceButton1.onClick.AddListener(() =>
            {
                PlayEffect(clickAudioSource);
                PlayerDatabase.RecordChoice(choiceId, questionLine.message, option0.choiceText, option0.points);
                MakeChoice(option0.nextDialogueIndex);
            });
        }

        if (choices.Count > 1 && choiceButton2 != null)
        {
            choiceButton2.gameObject.SetActive(true);
            if (choiceButton2Text != null) choiceButton2Text.text = choices[1].choiceText;
            choiceButton2.onClick.RemoveAllListeners();
            ChoiceOption option1 = choices[1];
            choiceButton2.onClick.AddListener(() =>
            {
                PlayEffect(clickAudioSource);
                PlayerDatabase.RecordChoice(choiceId, questionLine.message, option1.choiceText, option1.points);
                MakeChoice(option1.nextDialogueIndex);
            });
        }
    }

    public void MakeChoice(int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= dialogueList.Count)
            return;

        if (choiceContainer != null) choiceContainer.SetActive(false);
        currentLineIndex = targetIndex;
        StartCoroutine(PlayLine(currentLineIndex));
    }

    // The screen turns white while the teleport sound plays, then goes black and loads the bedroom.
    private IEnumerator EndScene()
    {
        isEnding = true;
        if (nextButton != null) nextButton.SetActive(false);
        if (choiceContainer != null) choiceContainer.SetActive(false);
        if (dialogueBox != null) dialogueBox.SetActive(false);

        float soundLength = 0f;
        if (teleportationAudioSource != null)
        {
            teleportationAudioSource.Play();
            if (teleportationAudioSource.clip != null)
                soundLength = teleportationAudioSource.clip.length;
        }

        float whiteDuration = Mathf.Max(whiteFadeDuration, soundLength * 0.6f);
        if (ambientAudioSource != null)
            StartCoroutine(FadeOutAudio(ambientAudioSource, whiteDuration));

        if (fadeOutScreen != null)
        {
            Color clearWhite = new Color(1f, 1f, 1f, 0f);
            yield return FadeOverlay(clearWhite, Color.white, whiteDuration);
        }

        while (teleportationAudioSource != null && teleportationAudioSource.isPlaying)
            yield return null;

        if (fadeOutScreen != null)
            yield return FadeOverlay(Color.white, Color.black, 0.5f);

        yield return new WaitForSeconds(0.3f);

        if (string.IsNullOrEmpty(nextSceneName))
            nextSceneName = "A1_Bedroom";

        SceneManager.LoadScene(nextSceneName);
    }

    private static IEnumerator FadeInAudio(AudioSource audioSource, float targetVolume, float fadeDuration)
    {
        audioSource.volume = 0f;
        audioSource.Play();

        float duration = Mathf.Max(fadeDuration, 0.01f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(0f, targetVolume, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        audioSource.volume = targetVolume;
    }

    // The heavenly ambient quietly fades away as the player is pulled back to their body.
    private static IEnumerator FadeOutAudio(AudioSource audioSource, float fadeDuration)
    {
        float startVolume = audioSource.volume;
        float duration = Mathf.Max(fadeDuration, 0.01f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        audioSource.Stop();
        audioSource.volume = startVolume;
    }

    // Fades the FadeOut overlay between two colors. Its Animator (copied from FadeIn) is turned off
    // so it does not fight the script over the color.
    private IEnumerator FadeOverlay(Color fromColor, Color toColor, float fadeDuration)
    {
        fadeOutScreen.SetActive(true);

        Animator fadeAnimator = fadeOutScreen.GetComponent<Animator>();
        if (fadeAnimator != null)
            fadeAnimator.enabled = false;

        Image fadeImage = fadeOutScreen.GetComponent<Image>();
        CanvasGroup canvasGroup = fadeOutScreen.GetComponent<CanvasGroup>();
        if (fadeImage == null && canvasGroup == null)
            canvasGroup = fadeOutScreen.AddComponent<CanvasGroup>();

        // With an Image the color itself fades; a CanvasGroup only fades the alpha.
        if (fadeImage != null && canvasGroup != null)
            canvasGroup.alpha = 1f;

        float duration = Mathf.Max(fadeDuration, 0.01f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            if (fadeImage != null)
                fadeImage.color = Color.Lerp(fromColor, toColor, progress);
            else
                canvasGroup.alpha = Mathf.Lerp(fromColor.a, toColor.a, progress);
            yield return null;
        }

        if (fadeImage != null)
            fadeImage.color = toColor;
        else
            canvasGroup.alpha = toColor.a;
    }
}
