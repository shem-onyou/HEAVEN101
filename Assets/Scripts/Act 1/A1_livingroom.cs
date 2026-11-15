using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using TMPro;

public enum A1LivingRoomAction
{
    None,
    NanayEnters
}

[System.Serializable]
public class A1LivingRoomChoiceOption
{
    public string choiceText;
    public int nextDialogueIndex;
    public int points;
}

[System.Serializable]
public class A1LivingRoomDialogueLine
{
    public A1LivingRoomAction actionBefore;
    public string speakerName;
    [TextArea(2, 5)] public string message;
    public List<A1LivingRoomChoiceOption> choices = new List<A1LivingRoomChoiceOption>();

    public A1LivingRoomDialogueLine(string name, string msg, List<A1LivingRoomChoiceOption> choiceOptions = null,
        A1LivingRoomAction action = A1LivingRoomAction.None)
    {
        speakerName = name;
        message = msg;
        choices = choiceOptions ?? new List<A1LivingRoomChoiceOption>();
        actionBefore = action;
    }
}

public class A1_livingroom : MonoBehaviour
{
    [Header("Cutscene Objects (auto-found by name if empty)")]
    [FormerlySerializedAs("livingRoomFadeScreenIn")]
    public GameObject fadeScreenIn;
    [FormerlySerializedAs("livingRoomFadeOutScreen")]
    public GameObject fadeOutScreen;
    public GameObject nanayCharacter;
    [FormerlySerializedAs("livingRoomDialogueBox")]
    public GameObject dialogueBox;
    [FormerlySerializedAs("livingRoomNextButton")]
    public GameObject nextButton;

    [Header("Choice Objects (auto-found by name if empty)")]
    [FormerlySerializedAs("livingRoomChoiceContainer")]
    public GameObject choiceContainer;
    [FormerlySerializedAs("livingRoomChoiceButton")]
    public Button choiceButton;
    [FormerlySerializedAs("livingRoomChoiceButtonText")]
    public TMP_Text choiceButtonText;
    public Button choiceButton2;
    public TMP_Text choiceButton2Text;

    [Header("Text UI References (auto-found by name if empty)")]
    [FormerlySerializedAs("livingRoomCharacterNameText")]
    public TMP_Text charNameText;
    [FormerlySerializedAs("livingRoomMessageText")]
    public TMP_Text messageInputText;

    [Header("Typewriter Speed Settings")]
    [FormerlySerializedAs("livingRoomTypingSpeed")]
    [SerializeField] private float typingSpeed = 0.03f;

    [Header("Dialogue Sequence")]
    [FormerlySerializedAs("livingRoomDialogueList")]
    public List<A1LivingRoomDialogueLine> dialogueList = new List<A1LivingRoomDialogueLine>();
    [Tooltip("Played after every kalat is cleaned.")]
    public List<A1LivingRoomDialogueLine> completionDialogue = new List<A1LivingRoomDialogueLine>();
    [FormerlySerializedAs("livingRoomNextSceneName")]
    [SerializeField] private string nextSceneName;
    public TMP_Text kalatProgressText;

    [Header("Nanay")]
    [SerializeField] private string nanayName = "Nanay";
    [SerializeField] private float nanayWalkDuration = 1.2f;

    [Header("Speaking Effects")]
    [SerializeField] private Color darkenedTint = new Color(0.45f, 0.45f, 0.45f, 1f);
    [SerializeField] private float tintDuration = 0.15f;
    [SerializeField] private float jitterDuration = 0.3f;
    [SerializeField] private float jitterStrength = 6f;

    [Header("Chores")]
    [TextArea(1, 2)]
    [SerializeField] private string choresHint = "Click the kalat to clean the living room!";
    [SerializeField] private float choresHintDuration = 3f;

    [Header("Sounds (auto-found by object or clip name if empty)")]
    [Tooltip("Optional: an AudioSource with the next_click clip.")]
    [SerializeField] private AudioSource clickAudioSource;
    [Tooltip("Morning house sounds, looped. Found by \"ambient\". Stays silent until it has a clip.")]
    [SerializeField] private AudioSource ambientAudioSource;
    [Tooltip("The player running down the stairs at the start. Found by \"stair\".")]
    [SerializeField] private AudioSource stairsAudioSource;
    [Tooltip("Plays for every kalat picked up. Found by \"clean\".")]
    [SerializeField] private AudioSource cleaningAudioSource;
    [Tooltip("Plays when every kalat is cleaned. Found by \"victory\" or \"bell\".")]
    [SerializeField] private AudioSource victoryAudioSource;
    [Tooltip("Plays when leaving for school. Found by \"door\".")]
    [SerializeField] private AudioSource doorAudioSource;
    [Tooltip("Random pitch change per pickup so repeated cleaning sounds don't feel identical.")]
    [SerializeField] private float cleaningPitchVariation = 0.1f;

    private int currentLineIndex = 0;
    private List<A1LivingRoomDialogueLine> currentLines;
    private Coroutine typingCoroutine;
    private bool isTyping;
    private bool isPlayingAction;
    private bool waitingForChoice;
    private bool choresStarted;
    private float choresStartTime;
    private bool completionDialogueActive;
    private bool isSceneTransitionQueued;
    private ClickableUIItem[] clickableKalatItems;
    private readonly HashSet<ClickableUIItem> cleanedKalatItems = new HashSet<ClickableUIItem>();

    private Graphic nanayGraphic;
    private Vector2 nanaySpotPosition;
    private Color nanayBaseColor = Color.white;
    private Coroutine nanayTintCoroutine;
    private Coroutine jitterCoroutine;
    private Vector2 jitterRestPosition;
    private bool isJittering;
    private Vector3 progressTextBaseScale = Vector3.one;
    private float ambientVolume = 1f;
    private Coroutine progressPunchCoroutine;

    private void Awake()
    {
        clickableKalatItems = FindObjectsByType<ClickableUIItem>(FindObjectsInactive.Include);
        SetKalatItemsClickable(false);
        EnsureKalatProgressText();

        if (kalatProgressText != null)
        {
            kalatProgressText.gameObject.SetActive(false);
            UpdateKalatProgress();
        }
    }

    private void EnsureKalatProgressText()
    {
        if (kalatProgressText != null)
            return;

        GameObject progressObject = GameObject.Find("Kalat_progress");
        if (progressObject != null)
        {
            kalatProgressText = progressObject.GetComponent<TMP_Text>();
            return;
        }

        TMP_Text[] allTexts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        foreach (TMP_Text text in allTexts)
        {
            if (text != null && text.name == "Kalat_progress")
            {
                kalatProgressText = text;
                return;
            }
        }
    }

    void Start()
    {
        if (string.IsNullOrEmpty(nextSceneName))
            nextSceneName = "A1_School";

        // The player comes down from the bedroom already dressed and in a hurry.
        if (dialogueList.Count == 0)
        {
            dialogueList.Add(new A1LivingRoomDialogueLine("You", "Nanay! I'm late, I have to go!")); // 0
            dialogueList.Add(new A1LivingRoomDialogueLine(nanayName, "Anak, wait! Late again? Hay nako, you slept like a rock.", action: A1LivingRoomAction.NanayEnters)); // 1
            dialogueList.Add(new A1LivingRoomDialogueLine("You", "Sorry, Nanay! I'll go now--")); // 2
            dialogueList.Add(new A1LivingRoomDialogueLine(nanayName, "Ah-ah! Before you leave, help me first with the chores.")); // 3
            dialogueList.Add(new A1LivingRoomDialogueLine("You", "What chores, Nanay?")); // 4
            dialogueList.Add(new A1LivingRoomDialogueLine(nanayName, "Look at this living room! Please pick up all the kalat.", new List<A1LivingRoomChoiceOption>
            {
                new A1LivingRoomChoiceOption { choiceText = "Do it", nextDialogueIndex = 8, points = 1 },
                new A1LivingRoomChoiceOption { choiceText = "But I'm late", nextDialogueIndex = 6 }
            })); // 5
            dialogueList.Add(new A1LivingRoomDialogueLine("You", "But I'm already late for school!")); // 6
            dialogueList.Add(new A1LivingRoomDialogueLine(nanayName, "I know, anak. Just do it fast, then you can go.")); // 7
            dialogueList.Add(new A1LivingRoomDialogueLine("You", "Okay, Nanay. I'll help you first.")); // 8
            dialogueList.Add(new A1LivingRoomDialogueLine(nanayName, "Thank you, anak. Hurry up, okay?")); // 9
        }

        if (completionDialogue.Count == 0)
        {
            completionDialogue.Add(new A1LivingRoomDialogueLine("You", "Done! I'm finished, Nanay. I'm going to school now!", action: A1LivingRoomAction.NanayEnters));
            completionDialogue.Add(new A1LivingRoomDialogueLine(nanayName, "Wow, ang linis! Thank you, anak."));
            completionDialogue.Add(new A1LivingRoomDialogueLine(nanayName, "Be careful on your way to school, ha!"));
        }

        FindSceneReferences();

        if (choiceContainer != null) choiceContainer.SetActive(false);
        if (dialogueBox != null) dialogueBox.SetActive(false);
        if (nextButton != null) nextButton.SetActive(false);
        if (fadeOutScreen != null) fadeOutScreen.SetActive(false);
        HoldBlackScreen();

        if (nanayCharacter != null)
        {
            nanayGraphic = nanayCharacter.GetComponent<Graphic>();
            RectTransform nanayTransform = nanayCharacter.transform as RectTransform;
            if (nanayTransform != null)
                nanaySpotPosition = nanayTransform.anchoredPosition;
            if (nanayGraphic != null)
                nanayBaseColor = nanayGraphic.color;
            nanayCharacter.SetActive(false);
        }

        if (nextButton != null)
        {
            Button nextDialogueButton = nextButton.GetComponent<Button>();
            if (nextDialogueButton != null)
            {
                nextDialogueButton.onClick.RemoveAllListeners();
                nextDialogueButton.onClick.AddListener(NextLivingRoomButton);
            }
        }

        currentLines = dialogueList;
        StartCoroutine(StartLivingRoomCutscene());
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

        if (canvasTransform == null)
            return;

        if (dialogueBox == null) dialogueBox = FindDeepObject(canvasTransform, "DialogueBox");
        if (fadeScreenIn == null) fadeScreenIn = FindDeepObject(canvasTransform, "FadeIn");
        if (fadeOutScreen == null) fadeOutScreen = FindDeepObject(canvasTransform, "FadeOut");
        if (nanayCharacter == null) nanayCharacter = FindDeepObject(canvasTransform, "Nanay");
        if (choiceContainer == null) choiceContainer = FindDeepObject(canvasTransform, "choice_Container");

        if (choiceContainer != null)
        {
            if (choiceButton == null)
            {
                GameObject buttonObject = FindDeepObject(choiceContainer.transform, "choice_Button1");
                if (buttonObject != null) choiceButton = buttonObject.GetComponent<Button>();
            }

            if (choiceButton2 == null)
            {
                GameObject buttonObject = FindDeepObject(choiceContainer.transform, "choice_Button2");
                if (buttonObject != null) choiceButton2 = buttonObject.GetComponent<Button>();
            }

            if (choiceButtonText == null && choiceButton != null)
                choiceButtonText = choiceButton.GetComponentInChildren<TMP_Text>(true);

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
    private void FindSounds()
    {
        foreach (AudioSource audioSource in FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
        {
            if (clickAudioSource == null && SoundMatches(audioSource, "click"))
                clickAudioSource = audioSource;
            else if (ambientAudioSource == null && SoundMatches(audioSource, "ambient"))
                ambientAudioSource = audioSource;
            else if (stairsAudioSource == null && SoundMatches(audioSource, "stair"))
                stairsAudioSource = audioSource;
            else if (cleaningAudioSource == null && SoundMatches(audioSource, "clean"))
                cleaningAudioSource = audioSource;
            else if (victoryAudioSource == null && (SoundMatches(audioSource, "victory") || SoundMatches(audioSource, "bell")))
                victoryAudioSource = audioSource;
            else if (doorAudioSource == null && SoundMatches(audioSource, "door"))
                doorAudioSource = audioSource;
        }

        // Every sound waits for its moment; nothing plays by itself when the scene starts.
        foreach (AudioSource effect in new[] { clickAudioSource, stairsAudioSource, cleaningAudioSource, victoryAudioSource, doorAudioSource })
        {
            if (effect == null)
                continue;

            effect.playOnAwake = false;
            effect.loop = false;
            effect.Stop();
        }

        if (ambientAudioSource != null)
        {
            ambientVolume = ambientAudioSource.volume;
            ambientAudioSource.Stop();
            ambientAudioSource.loop = true;
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

    private void PlayClick()
    {
        PlayEffect(clickAudioSource);
    }

    // Slides a sound's volume; starts it if it needs to be heard and stops it at silence.
    private static IEnumerator FadeAudio(AudioSource audioSource, float fromVolume, float toVolume, float fadeDuration)
    {
        if (audioSource.clip == null)
            yield break;

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

    // The player is heard running down the stairs in the dark, then the living room fades in.
    private IEnumerator StartLivingRoomCutscene()
    {
        yield return new WaitForSeconds(0.5f);
        PlayEffect(stairsAudioSource);
        yield return new WaitForSeconds(0.5f);
        yield return FadeFromBlack(1.5f);

        if (ambientAudioSource != null)
            StartCoroutine(FadeAudio(ambientAudioSource, 0f, ambientVolume, 1.5f));

        yield return new WaitForSeconds(0.5f);

        if (currentLines.Count > 0)
            StartCoroutine(PlayLine(currentLineIndex));
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

    private IEnumerator FadeFromBlack(float duration)
    {
        if (fadeScreenIn == null)
            yield break;

        Image fadeImage = fadeScreenIn.GetComponent<Image>();
        if (fadeImage != null)
            yield return FadeImageAlpha(fadeImage, 1f, 0f, duration);

        fadeScreenIn.SetActive(false);
    }

    public void NextLivingRoomButton()
    {
        if (isPlayingAction || waitingForChoice || isSceneTransitionQueued)
            return;

        PlayClick();

        if (isTyping)
        {
            CompleteLivingRoomTypingInstantly();
            return;
        }

        // The last line of the morning talk starts the chores; the last completion line leaves for school.
        if (currentLineIndex + 1 >= currentLines.Count)
        {
            EndLivingRoomDialogue();
            return;
        }

        currentLineIndex++;
        StartCoroutine(PlayLine(currentLineIndex));
    }

    private IEnumerator PlayLine(int index)
    {
        A1LivingRoomDialogueLine line = currentLines[index];

        if (line.actionBefore == A1LivingRoomAction.NanayEnters)
        {
            isPlayingAction = true;
            if (nextButton != null) nextButton.SetActive(false);
            yield return NanayWalks(true);
            isPlayingAction = false;
        }

        DisplayLivingRoomLine(line);
    }

    // Nanay walks in from the right edge to her spot, or back out the same way.
    private IEnumerator NanayWalks(bool walkingIn)
    {
        if (nanayCharacter == null)
            yield break;

        RectTransform nanayTransform = nanayCharacter.transform as RectTransform;
        RectTransform parentTransform = nanayTransform != null ? nanayTransform.parent as RectTransform : null;
        if (parentTransform == null)
        {
            nanayCharacter.SetActive(walkingIn);
            yield break;
        }

        StopJitter();
        Vector2 offscreenPosition = nanaySpotPosition + new Vector2(parentTransform.rect.width, 0f);
        Vector2 startPosition = walkingIn ? offscreenPosition : nanayTransform.anchoredPosition;
        Vector2 targetPosition = walkingIn ? nanaySpotPosition : offscreenPosition;

        nanayTransform.anchoredPosition = startPosition;
        nanayCharacter.SetActive(true);

        float duration = Mathf.Max(nanayWalkDuration, 0.01f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            nanayTransform.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, progress);
            yield return null;
        }

        nanayTransform.anchoredPosition = targetPosition;
        if (!walkingIn)
            nanayCharacter.SetActive(false);
    }

    private void DisplayLivingRoomLine(A1LivingRoomDialogueLine line)
    {
        if (dialogueBox != null) dialogueBox.SetActive(true);
        if (nextButton != null) nextButton.SetActive(true);
        if (choiceContainer != null) choiceContainer.SetActive(false);

        if (charNameText != null)
            charNameText.text = line.speakerName;

        UpdateSpeakerEffects(line.speakerName);

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        typingCoroutine = StartCoroutine(TypeLivingRoomText(line.message));
    }

    // Nanay stays bright and jitters while talking; she darkens while You is speaking.
    private void UpdateSpeakerEffects(string speakerName)
    {
        StopJitter();

        if (nanayCharacter == null || nanayGraphic == null || !nanayCharacter.activeInHierarchy)
            return;

        bool nanaySpeaking = speakerName == nanayName;
        Color targetColor = nanaySpeaking
            ? nanayBaseColor
            : new Color(nanayBaseColor.r * darkenedTint.r, nanayBaseColor.g * darkenedTint.g, nanayBaseColor.b * darkenedTint.b, nanayBaseColor.a);

        if (nanayTintCoroutine != null)
            StopCoroutine(nanayTintCoroutine);
        nanayTintCoroutine = StartCoroutine(TintNanay(targetColor));

        if (nanaySpeaking)
            jitterCoroutine = StartCoroutine(Jitter());
    }

    private IEnumerator TintNanay(Color targetColor)
    {
        Color startColor = nanayGraphic.color;
        float duration = Mathf.Max(tintDuration, 0.01f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            nanayGraphic.color = Color.Lerp(startColor, targetColor, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        nanayGraphic.color = targetColor;
    }

    private void StopJitter()
    {
        if (jitterCoroutine != null)
        {
            StopCoroutine(jitterCoroutine);
            jitterCoroutine = null;
        }

        RectTransform nanayTransform = nanayCharacter != null ? nanayCharacter.transform as RectTransform : null;
        if (isJittering && nanayTransform != null)
            nanayTransform.anchoredPosition = jitterRestPosition;
        isJittering = false;
    }

    // Small hop with a side-to-side wiggle that fades out, so Nanay looks alive.
    private IEnumerator Jitter()
    {
        RectTransform nanayTransform = nanayCharacter.transform as RectTransform;
        if (nanayTransform == null)
            yield break;

        jitterRestPosition = nanayTransform.anchoredPosition;
        isJittering = true;

        float duration = Mathf.Max(jitterDuration, 0.01f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float wiggleX = Mathf.Sin(elapsed * 60f) * jitterStrength * (1f - progress);
            float hopY = Mathf.Sin(progress * Mathf.PI) * jitterStrength;
            nanayTransform.anchoredPosition = jitterRestPosition + new Vector2(wiggleX, hopY);
            yield return null;
        }

        nanayTransform.anchoredPosition = jitterRestPosition;
        isJittering = false;
        jitterCoroutine = null;
    }

    private IEnumerator TypeLivingRoomText(string targetMessage)
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
            yield return new WaitForSeconds(typingSpeed);
        }

        OnTypingFinished();
    }

    private void CompleteLivingRoomTypingInstantly()
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        messageInputText.maxVisibleCharacters = messageInputText.textInfo.characterCount;
        CharacterVoice.Stop();
        OnTypingFinished();
    }

    // The scene only moves on when the player presses Next, so the last line can be read.
    private void OnTypingFinished()
    {
        isTyping = false;

        if (HasChoices(currentLines[currentLineIndex]))
            ShowLivingRoomChoice();
    }

    private static bool HasChoices(A1LivingRoomDialogueLine line)
    {
        return line.choices != null && line.choices.Count > 0;
    }

    private void ShowLivingRoomChoice()
    {
        waitingForChoice = true;
        if (nextButton != null) nextButton.SetActive(false);
        if (choiceContainer != null) choiceContainer.SetActive(true);

        List<A1LivingRoomChoiceOption> choices = currentLines[currentLineIndex].choices;
        string question = currentLines[currentLineIndex].message;
        string choiceId = $"{SceneManager.GetActiveScene().name}_line{currentLineIndex}";

        SetupChoiceButton(choiceButton, choiceButtonText, choices.Count > 0 ? choices[0] : null, choiceId, question);
        SetupChoiceButton(choiceButton2, choiceButton2Text, choices.Count > 1 ? choices[1] : null, choiceId, question);
    }

    private void SetupChoiceButton(Button button, TMP_Text buttonText, A1LivingRoomChoiceOption option, string choiceId, string question)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.gameObject.SetActive(option != null);
        if (option == null)
            return;

        if (buttonText != null)
            buttonText.text = option.choiceText;

        button.onClick.AddListener(() =>
        {
            PlayClick();
            PlayerDatabase.RecordChoice(choiceId, question, option.choiceText, option.points);
            MakeLivingRoomChoice(option.nextDialogueIndex);
        });
    }

    private void MakeLivingRoomChoice(int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= currentLines.Count)
            return;

        waitingForChoice = false;
        if (choiceContainer != null) choiceContainer.SetActive(false);
        currentLineIndex = targetIndex;
        StartCoroutine(PlayLine(currentLineIndex));
    }

    private void EndLivingRoomDialogue()
    {
        if (nextButton != null) nextButton.SetActive(false);
        if (choiceContainer != null) choiceContainer.SetActive(false);
        if (dialogueBox != null) dialogueBox.SetActive(false);

        if (!choresStarted)
        {
            StartCoroutine(StartChores());
            return;
        }

        if (completionDialogueActive && !isSceneTransitionQueued)
        {
            isSceneTransitionQueued = true;
            StartCoroutine(LeaveForSchool());
        }
    }

    // Nanay walks off, a hint shows how to play, and the kalat become clickable.
    private IEnumerator StartChores()
    {
        isPlayingAction = true;
        yield return NanayWalks(false);
        isPlayingAction = false;

        choresStarted = true;
        choresStartTime = Time.time;

        EnsureKalatProgressText();
        UpdateKalatProgress();
        if (kalatProgressText != null)
        {
            progressTextBaseScale = kalatProgressText.transform.localScale;
            kalatProgressText.gameObject.SetActive(true);
            kalatProgressText.enabled = true;
        }

        SetKalatItemsClickable(true);
        StartCoroutine(ShowChoresHint());
    }

    private IEnumerator ShowChoresHint()
    {
        Transform canvasTransform = dialogueBox != null ? dialogueBox.transform.parent : null;
        if (canvasTransform == null || string.IsNullOrEmpty(choresHint))
            yield break;

        GameObject hintObject = new GameObject("ChoresHint", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform hintTransform = hintObject.GetComponent<RectTransform>();
        hintTransform.SetParent(canvasTransform, false);
        hintTransform.anchorMin = new Vector2(0.1f, 0.75f);
        hintTransform.anchorMax = new Vector2(0.9f, 0.95f);
        hintTransform.offsetMin = Vector2.zero;
        hintTransform.offsetMax = Vector2.zero;
        hintTransform.SetAsLastSibling();

        TextMeshProUGUI hint = hintObject.GetComponent<TextMeshProUGUI>();
        if (messageInputText != null)
            hint.font = messageInputText.font;
        hint.fontSize = 26f;
        hint.alignment = TextAlignmentOptions.Center;
        hint.color = Color.white;
        hint.outlineWidth = 0.2f;
        hint.outlineColor = Color.black;
        hint.raycastTarget = false;
        hint.text = choresHint;

        yield return FadeTextAlpha(hint, 0f, 1f, 0.5f);
        yield return new WaitForSeconds(choresHintDuration);
        yield return FadeTextAlpha(hint, 1f, 0f, 0.5f);
        Destroy(hintObject);
    }

    public void RegisterKalatCleaned(ClickableUIItem item)
    {
        if (!choresStarted || item == null || !cleanedKalatItems.Add(item))
            return;

        item.enabled = false;
        PlayCleaningSound();
        StartCoroutine(PopAway(item.transform));
        UpdateKalatProgress();
        if (progressPunchCoroutine != null)
            StopCoroutine(progressPunchCoroutine);
        progressPunchCoroutine = StartCoroutine(PunchProgressText());

        if (cleanedKalatItems.Count >= clickableKalatItems.Length)
        {
            float secondsTaken = Time.time - choresStartTime;
            PlayerDatabase.RecordAction("act1_morning_chores",
                $"Helped Nanay clean the living room ({clickableKalatItems.Length} items in {secondsTaken:0} seconds)",
                secondsTaken, 1);
            StartCoroutine(ShowCompletionDialogue());
        }
    }

    private void PlayCleaningSound()
    {
        if (cleaningAudioSource == null || cleaningAudioSource.clip == null)
            return;

        cleaningAudioSource.pitch = 1f + Random.Range(-cleaningPitchVariation, cleaningPitchVariation);
        cleaningAudioSource.PlayOneShot(cleaningAudioSource.clip);
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

    private IEnumerator PunchProgressText()
    {
        if (kalatProgressText == null)
            yield break;

        Transform textTransform = kalatProgressText.transform;
        Vector3 baseScale = progressTextBaseScale;
        float elapsed = 0f;
        const float duration = 0.2f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float bump = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
            textTransform.localScale = baseScale * (1f + 0.2f * bump);
            yield return null;
        }

        textTransform.localScale = baseScale;
    }

    private void UpdateKalatProgress()
    {
        if (kalatProgressText != null)
            kalatProgressText.text = $"Kalat: {cleanedKalatItems.Count} / {clickableKalatItems.Length}";
    }

    private IEnumerator ShowCompletionDialogue()
    {
        yield return new WaitForSeconds(0.3f);
        PlayEffect(victoryAudioSource);
        yield return new WaitForSeconds(1f);

        if (kalatProgressText != null) kalatProgressText.gameObject.SetActive(false);

        completionDialogueActive = true;
        currentLines = completionDialogue;
        currentLineIndex = 0;

        if (currentLines.Count > 0)
            StartCoroutine(PlayLine(currentLineIndex));
        else
            EndLivingRoomDialogue();
    }

    // The door closes behind the player and the room fades to black before the school scene loads.
    private IEnumerator LeaveForSchool()
    {
        StopJitter();

        if (doorAudioSource != null && doorAudioSource.clip != null)
            doorAudioSource.Play();

        if (ambientAudioSource != null && ambientAudioSource.isPlaying)
            StartCoroutine(FadeAudio(ambientAudioSource, ambientAudioSource.volume, 0f, 1f));

        if (fadeOutScreen != null)
        {
            fadeOutScreen.SetActive(true);

            Animator fadeAnimator = fadeOutScreen.GetComponent<Animator>();
            if (fadeAnimator != null)
                fadeAnimator.enabled = false;

            CanvasGroup canvasGroup = fadeOutScreen.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
                canvasGroup.alpha = 1f;

            Image fadeImage = fadeOutScreen.GetComponent<Image>();
            if (fadeImage != null)
            {
                fadeImage.color = new Color(0f, 0f, 0f, 0f);
                yield return FadeImageAlpha(fadeImage, 0f, 1f, 1f);
            }
        }

        while (doorAudioSource != null && doorAudioSource.isPlaying)
            yield return null;

        yield return new WaitForSeconds(0.5f);
        SceneManager.LoadScene(nextSceneName);
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

    private void SetKalatItemsClickable(bool clickable)
    {
        foreach (ClickableUIItem item in clickableKalatItems)
            item.enabled = clickable;
    }
}
