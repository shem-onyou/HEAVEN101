using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public enum A2SchoolAction
{
    None,
    ElijahEnters,
    ClassmateEnters,
    GoToClassroom
}

[System.Serializable]
public class A2SchoolDialogueLine
{
    public A2SchoolAction actionBefore;
    public string speakerName;
    [TextArea(2, 5)] public string message;

    public A2SchoolDialogueLine(string name, string msg, A2SchoolAction action = A2SchoolAction.None)
    {
        speakerName = name;
        message = msg;
        actionBefore = action;
    }
}

// Act 2 school gate: the three friends meet and are reminded of today's quiz. When asked if they reviewed,
// the player (who wants to look perfect today) can lie and say yes, which costs good points, or admit the truth.
public class A2_School_control : MonoBehaviour
{
    public const string ReviewChoiceId = "act2_did_you_review";
    public const string LiedAnswer = "lied";
    public const string HonestAnswer = "honest";

    // True if the player lied about reviewing; later Act 2 scenes can react to it.
    public static bool LiedAboutReviewing => PlayerDatabase.GetChoice(ReviewChoiceId) == LiedAnswer;

    [Header("Cutscene Objects (auto-found by name if empty)")]
    public GameObject fadeScreenIn;
    public GameObject fadeOutScreen;
    public GameObject dialogueBox;
    public GameObject nextButton;

    [Header("Choice System Objects (auto-found by name if empty)")]
    public GameObject choiceContainer;
    public Button choiceButton1;
    public TMP_Text choiceButton1Text;
    public Button choiceButton2;
    public TMP_Text choiceButton2Text;
    [SerializeField] private string lieChoiceText = "Yeah, of course I did!";
    [SerializeField] private string honestChoiceText = "...Honestly, no. I forgot.";
    [Tooltip("Points for lying about reviewing.")]
    [SerializeField] private int liePoints = -1;
    [Tooltip("Points for telling the truth.")]
    [SerializeField] private int honestPoints = 1;

    [Header("Characters (auto-found by name if empty)")]
    public Image classmate1Image;
    public Image classmate2Image;
    [SerializeField] private string classmate1Name = "Elijah";
    [SerializeField] private string classmate2Name = "Classmate";
    [SerializeField] private float walkInDuration = 1f;
    [Tooltip("Used only when both classmates were placed on the same spot: spreads them apart (fraction of the screen width).")]
    [Range(0f, 1f)] [SerializeField] private float classmateSpacing = 0.3f;

    [Header("Speaking Effects")]
    [SerializeField] private Color darkenedTint = new Color(0.45f, 0.45f, 0.45f, 1f);
    [SerializeField] private float tintDuration = 0.15f;
    [SerializeField] private float jitterDuration = 0.3f;
    [SerializeField] private float jitterStrength = 6f;

    [Header("Text UI References (auto-found by name if empty)")]
    public TMP_Text charNameText;
    public TMP_Text messageInputText;

    [Header("Typewriter Speed Settings")]
    [SerializeField] private float typingSpeed = 0.03f;

    [Header("Sounds (auto-found by object or clip name if empty)")]
    [Tooltip("School ambience, looped. Found by \"ambient\". Stays silent until it has a clip.")]
    [SerializeField] private AudioSource ambientAudioSource;
    [Tooltip("Optional: an AudioSource with the next_click clip.")]
    [SerializeField] private AudioSource clickAudioSource;

    [Header("Scene Transition")]
    [SerializeField] private string nextSceneName = "A2_Croom";

    [Header("Dialogue Sequence")]
    [Tooltip("Meeting the friends, up to the \"did you review?\" question.")]
    public List<A2SchoolDialogueLine> introLines = new List<A2SchoolDialogueLine>();
    [Tooltip("Played if the player lies and says they reviewed.")]
    public List<A2SchoolDialogueLine> lieLines = new List<A2SchoolDialogueLine>();
    [Tooltip("Played if the player admits they didn't review.")]
    public List<A2SchoolDialogueLine> honestLines = new List<A2SchoolDialogueLine>();
    [Tooltip("Played after either answer, before walking to the classroom.")]
    public List<A2SchoolDialogueLine> outroLines = new List<A2SchoolDialogueLine>();

    private class SchoolCharacter
    {
        public Image image;
        public string speakerName;
        public Vector2 spotPosition;
        public Color baseColor = Color.white;
        public Coroutine tintCoroutine;
    }

    private SchoolCharacter elijah;
    private SchoolCharacter classmate;
    private SchoolCharacter[] characters;

    private List<A2SchoolDialogueLine> currentLines;
    private int currentLineIndex = 0;
    private Coroutine typingCoroutine;
    private bool isTyping;
    private bool isPlayingAction;
    private bool waitingForChoice;
    private bool choiceMade;
    private bool outroPlaying;
    private bool isEnding;
    private float ambientVolume = 1f;

    private Coroutine jitterCoroutine;
    private RectTransform jitterTarget;
    private Vector2 jitterRestPosition;

    void Start()
    {
        if (introLines.Count == 0)
        {
            introLines.Add(new A2SchoolDialogueLine(classmate1Name, "Uy! You're early today?! Is the world ending?", A2SchoolAction.ElijahEnters));
            introLines.Add(new A2SchoolDialogueLine(classmate2Name, "Hoy, good morning! Ang aga mo, ah.", A2SchoolAction.ClassmateEnters));
            introLines.Add(new A2SchoolDialogueLine("You", "Of course! I'm turning over a new leaf today."));
            introLines.Add(new A2SchoolDialogueLine(classmate1Name, "Hehe, nice. Oh right, don't forget, we have the quiz later!"));
            introLines.Add(new A2SchoolDialogueLine("You", "(The quiz... I didn't review at all yesterday...)"));
            introLines.Add(new A2SchoolDialogueLine(classmate2Name, "So? Did you review last night?"));
        }

        if (lieLines.Count == 0)
        {
            lieLines.Add(new A2SchoolDialogueLine(classmate2Name, "Wow, nice! Ikaw na talaga."));
            lieLines.Add(new A2SchoolDialogueLine(classmate1Name, "Pakopya ha! Hehe."));
            lieLines.Add(new A2SchoolDialogueLine("You", "(...Why did I say that? I didn't even open my notes.)"));
        }

        if (honestLines.Count == 0)
        {
            honestLines.Add(new A2SchoolDialogueLine(classmate1Name, "Same! HAHAHA, apir!"));
            honestLines.Add(new A2SchoolDialogueLine(classmate2Name, "Hay nako, kayong dalawa talaga. Here, borrow my notes until the bell rings."));
            honestLines.Add(new A2SchoolDialogueLine("You", "(At least I told the truth... I'll review what I can.)"));
        }

        if (outroLines.Count == 0)
        {
            outroLines.Add(new A2SchoolDialogueLine(classmate2Name, "Tara na, let's go to the classroom."));
            outroLines.Add(new A2SchoolDialogueLine("You", "Wait for me!", A2SchoolAction.GoToClassroom));
        }

        FindSceneReferences();

        elijah = CreateCharacter(classmate1Image, classmate1Name);
        classmate = CreateCharacter(classmate2Image, classmate2Name);
        characters = new[] { elijah, classmate };

        SpreadIfStacked();
        KeepOnScreen(elijah, classmate);

        if (choiceContainer != null) choiceContainer.SetActive(false);
        if (fadeOutScreen != null) fadeOutScreen.SetActive(false);
        if (dialogueBox != null) dialogueBox.SetActive(false);
        HoldBlackScreen();

        if (nextButton != null)
        {
            Button nextDialogueButton = nextButton.GetComponent<Button>();
            if (nextDialogueButton != null)
            {
                nextDialogueButton.onClick.RemoveAllListeners();
                nextDialogueButton.onClick.AddListener(NextButton);
            }
        }

        currentLines = introLines;
        StartCoroutine(StartCutscene());
    }

    private SchoolCharacter CreateCharacter(Image characterImage, string speakerName)
    {
        SchoolCharacter character = new SchoolCharacter { image = characterImage, speakerName = speakerName };
        if (characterImage != null)
        {
            character.spotPosition = characterImage.rectTransform.anchoredPosition;
            character.baseColor = characterImage.color;
            characterImage.gameObject.SetActive(false);
        }

        return character;
    }

    // If classmate2 was made by duplicating classmate1 and left on the same spot, push them apart.
    private void SpreadIfStacked()
    {
        if (elijah.image == null || classmate.image == null)
            return;

        if (Vector2.Distance(elijah.spotPosition, classmate.spotPosition) > 1f)
            return;

        RectTransform parentTransform = elijah.image.rectTransform.parent as RectTransform;
        if (parentTransform == null)
            return;

        float halfGap = parentTransform.rect.width * classmateSpacing * 0.5f;
        Vector2 middle = elijah.spotPosition;
        elijah.spotPosition = middle - new Vector2(halfGap, 0f);
        classmate.spotPosition = middle + new Vector2(halfGap, 0f);
    }

    // Slides the characters together (keeping their gap) until none sticks out past a screen edge.
    private static void KeepOnScreen(params SchoolCharacter[] group)
    {
        float overRight = 0f;
        float overLeft = 0f;

        foreach (SchoolCharacter character in group)
        {
            if (character.image == null)
                continue;

            RectTransform imageTransform = character.image.rectTransform;
            RectTransform parentTransform = imageTransform.parent as RectTransform;
            if (parentTransform == null)
                continue;

            float halfImage = imageTransform.rect.width * Mathf.Abs(imageTransform.localScale.x) * 0.5f;
            float halfScreen = parentTransform.rect.width * 0.5f;
            overRight = Mathf.Max(overRight, character.spotPosition.x + halfImage - halfScreen);
            overLeft = Mathf.Max(overLeft, -halfScreen - (character.spotPosition.x - halfImage));
        }

        float shift = overRight > 0f ? -overRight : overLeft;
        if (Mathf.Approximately(shift, 0f))
            return;

        foreach (SchoolCharacter character in group)
            character.spotPosition += new Vector2(shift, 0f);
    }

    // Characters on the left half of the screen come in through the left edge, and vice versa.
    private static float SideOf(SchoolCharacter character)
    {
        return character.spotPosition.x < 0f ? -1f : 1f;
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
        if (choiceContainer == null) choiceContainer = FindDeepObject(canvasTransform, "choice_Container");
        if (classmate1Image == null) classmate1Image = FindImage(canvasTransform, "classmate1");
        if (classmate2Image == null) classmate2Image = FindImage(canvasTransform, "classmate2");

        // This scene is a copy of the Act 1 school; the pulubi is not part of Act 2 here.
        GameObject pulubiObject = FindDeepObject(canvasTransform, "pulubi");
        if (pulubiObject != null)
            pulubiObject.SetActive(false);

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

    private static Image FindImage(Transform parent, string objectName)
    {
        GameObject found = FindDeepObject(parent, objectName);
        return found != null ? found.GetComponent<Image>() : null;
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
            if (ambientAudioSource == null && SoundMatches(audioSource, "ambien"))
                ambientAudioSource = audioSource;
            else if (clickAudioSource == null && SoundMatches(audioSource, "click"))
                clickAudioSource = audioSource;
        }

        if (clickAudioSource != null)
        {
            clickAudioSource.playOnAwake = false;
            clickAudioSource.loop = false;
            clickAudioSource.Stop();
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

    private void PlayClick()
    {
        if (clickAudioSource != null && clickAudioSource.clip != null)
            clickAudioSource.PlayOneShot(clickAudioSource.clip);
    }

    // Slides the ambient's volume; starts it if it needs to be heard and stops it at silence.
    private IEnumerator FadeAmbient(float toVolume, float fadeDuration)
    {
        if (ambientAudioSource == null || ambientAudioSource.clip == null)
            yield break;

        float fromVolume = ambientAudioSource.isPlaying ? ambientAudioSource.volume : 0f;
        ambientAudioSource.volume = fromVolume;
        if (!ambientAudioSource.isPlaying && toVolume > 0f)
            ambientAudioSource.Play();

        float duration = Mathf.Max(fadeDuration, 0.01f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            ambientAudioSource.volume = Mathf.Lerp(fromVolume, toVolume, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        ambientAudioSource.volume = toVolume;
        if (toVolume <= 0f)
            ambientAudioSource.Stop();
    }

    IEnumerator StartCutscene()
    {
        yield return new WaitForSeconds(0.5f);
        yield return FadeFromBlack(1.5f);
        StartCoroutine(FadeAmbient(ambientVolume, 1.5f));
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

    public void NextButton()
    {
        if (isPlayingAction || waitingForChoice || isEnding)
            return;

        PlayClick();

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

        // End of the current list: the intro leads to the choice, a choice path leads to the outro,
        // and the outro ends the scene.
        if (!choiceMade)
            ShowReviewChoice();
        else if (!outroPlaying)
            StartLines(outroLines, true);
        else
            StartCoroutine(FadeOutScene());
    }

    private void StartLines(List<A2SchoolDialogueLine> lines, bool isOutro)
    {
        outroPlaying = isOutro;
        currentLines = lines;
        currentLineIndex = 0;

        if (currentLines.Count > 0)
            StartCoroutine(PlayLine(currentLineIndex));
        else
            NextButton();
    }

    private void ShowReviewChoice()
    {
        waitingForChoice = true;
        if (nextButton != null) nextButton.SetActive(false);
        if (choiceContainer != null) choiceContainer.SetActive(true);

        string question = introLines.Count > 0 ? introLines[introLines.Count - 1].message : "Did you review last night?";

        SetupChoiceButton(choiceButton1, choiceButton1Text, lieChoiceText, () =>
        {
            PlayerDatabase.RecordChoice(ReviewChoiceId, question, lieChoiceText, liePoints, LiedAnswer);
            MakeChoice(lieLines);
        });

        SetupChoiceButton(choiceButton2, choiceButton2Text, honestChoiceText, () =>
        {
            PlayerDatabase.RecordChoice(ReviewChoiceId, question, honestChoiceText, honestPoints, HonestAnswer);
            MakeChoice(honestLines);
        });
    }

    private void SetupChoiceButton(Button button, TMP_Text buttonText, string label, UnityEngine.Events.UnityAction onChosen)
    {
        if (button == null)
            return;

        button.gameObject.SetActive(true);
        if (buttonText != null) buttonText.text = label;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            PlayClick();
            onChosen();
        });
    }

    private void MakeChoice(List<A2SchoolDialogueLine> lines)
    {
        if (!waitingForChoice)
            return;

        waitingForChoice = false;
        choiceMade = true;
        if (choiceContainer != null) choiceContainer.SetActive(false);
        StartLines(lines, false);
    }

    private IEnumerator PlayLine(int index)
    {
        A2SchoolDialogueLine line = currentLines[index];

        if (line.actionBefore != A2SchoolAction.None)
        {
            isPlayingAction = true;
            if (nextButton != null) nextButton.SetActive(false);
            StopJitter();
            yield return PerformAction(line.actionBefore);
            isPlayingAction = false;
        }

        DisplayLine(line);
    }

    private IEnumerator PerformAction(A2SchoolAction action)
    {
        switch (action)
        {
            case A2SchoolAction.ElijahEnters:
                yield return WalkIn(elijah, SideOf(elijah), walkInDuration);
                break;

            case A2SchoolAction.ClassmateEnters:
                yield return WalkIn(classmate, SideOf(classmate), walkInDuration);
                break;

            case A2SchoolAction.GoToClassroom:
                yield return GoToClassroom();
                break;
        }
    }

    // side: -1 enters from the left edge, 1 enters from the right edge
    private IEnumerator WalkIn(SchoolCharacter character, float side, float duration)
    {
        if (character.image == null)
            yield break;

        RectTransform imageTransform = character.image.rectTransform;
        RectTransform parentTransform = imageTransform.parent as RectTransform;
        if (parentTransform == null)
            yield break;

        Vector2 startPosition = character.spotPosition + new Vector2(side * parentTransform.rect.width, 0f);
        imageTransform.anchoredPosition = startPosition;
        character.image.gameObject.SetActive(true);

        yield return MoveImage(imageTransform, startPosition, character.spotPosition, duration);
    }

    // The classmate walks off toward the classroom first, Elijah follows.
    private IEnumerator GoToClassroom()
    {
        if (dialogueBox != null) dialogueBox.SetActive(false);

        Coroutine classmateRun = StartCoroutine(RunOffScreen(classmate, walkInDuration));
        yield return new WaitForSeconds(0.3f);
        Coroutine elijahRun = StartCoroutine(RunOffScreen(elijah, walkInDuration));

        yield return classmateRun;
        yield return elijahRun;
        yield return new WaitForSeconds(0.3f);
    }

    private IEnumerator RunOffScreen(SchoolCharacter character, float duration)
    {
        if (character.image == null || !character.image.gameObject.activeSelf)
            yield break;

        RectTransform imageTransform = character.image.rectTransform;
        RectTransform parentTransform = imageTransform.parent as RectTransform;
        if (parentTransform == null)
            yield break;

        Vector2 startPosition = imageTransform.anchoredPosition;
        Vector2 exitPosition = startPosition + new Vector2(parentTransform.rect.width, 0f);

        yield return MoveImage(imageTransform, startPosition, exitPosition, duration);
        character.image.gameObject.SetActive(false);
    }

    private static IEnumerator MoveImage(RectTransform imageTransform, Vector2 startPosition, Vector2 targetPosition, float moveDuration)
    {
        float duration = Mathf.Max(moveDuration, 0.01f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            imageTransform.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, progress);
            yield return null;
        }

        imageTransform.anchoredPosition = targetPosition;
    }

    private void DisplayLine(A2SchoolDialogueLine line)
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

    // The speaker stays bright, comes to the front and jitters; everyone else darkens.
    private void UpdateSpeakerEffects(string speakerName)
    {
        StopJitter();

        foreach (SchoolCharacter character in characters)
        {
            if (character.image == null)
                continue;

            bool isSpeaking = speakerName == character.speakerName;
            Color targetColor = isSpeaking
                ? character.baseColor
                : new Color(character.baseColor.r * darkenedTint.r, character.baseColor.g * darkenedTint.g, character.baseColor.b * darkenedTint.b, character.baseColor.a);

            if (character.tintCoroutine != null)
                StopCoroutine(character.tintCoroutine);

            if (character.image.gameObject.activeInHierarchy)
                character.tintCoroutine = StartCoroutine(TintImage(character.image, targetColor));
            else
            {
                character.image.color = targetColor;
                character.tintCoroutine = null;
            }

            if (isSpeaking && character.image.gameObject.activeInHierarchy)
            {
                BringToFront(character.image);
                StartJitter(character.image);
            }
        }
    }

    // Moves the speaker in front of the other character while keeping both behind the dialogue box.
    private void BringToFront(Image characterImage)
    {
        Transform speakerTransform = characterImage.transform;
        int frontIndex = speakerTransform.GetSiblingIndex();

        foreach (SchoolCharacter other in characters)
        {
            if (other.image != null && other.image.transform.parent == speakerTransform.parent)
                frontIndex = Mathf.Max(frontIndex, other.image.transform.GetSiblingIndex());
        }

        speakerTransform.SetSiblingIndex(frontIndex);
    }

    private IEnumerator TintImage(Image characterImage, Color targetColor)
    {
        Color startColor = characterImage.color;
        float duration = Mathf.Max(tintDuration, 0.01f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            characterImage.color = Color.Lerp(startColor, targetColor, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        characterImage.color = targetColor;
    }

    private void StartJitter(Image characterImage)
    {
        jitterTarget = characterImage.rectTransform;
        jitterRestPosition = jitterTarget.anchoredPosition;
        jitterCoroutine = StartCoroutine(Jitter());
    }

    private void StopJitter()
    {
        if (jitterCoroutine != null)
        {
            StopCoroutine(jitterCoroutine);
            jitterCoroutine = null;
        }

        if (jitterTarget != null)
        {
            jitterTarget.anchoredPosition = jitterRestPosition;
            jitterTarget = null;
        }
    }

    // Small hop with a side-to-side wiggle that fades out, so the speaker looks alive.
    private IEnumerator Jitter()
    {
        float duration = Mathf.Max(jitterDuration, 0.01f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float wiggleX = Mathf.Sin(elapsed * 60f) * jitterStrength * (1f - progress);
            float hopY = Mathf.Sin(progress * Mathf.PI) * jitterStrength;
            jitterTarget.anchoredPosition = jitterRestPosition + new Vector2(wiggleX, hopY);
            yield return null;
        }

        jitterTarget.anchoredPosition = jitterRestPosition;
        jitterTarget = null;
        jitterCoroutine = null;
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

        isTyping = false;
    }

    private void CompleteTypingInstantly()
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        messageInputText.maxVisibleCharacters = messageInputText.textInfo.characterCount;
        isTyping = false;
    }

    // The school fades to black (and its sounds die down) before the classroom loads.
    private IEnumerator FadeOutScene()
    {
        isEnding = true;
        StopJitter();
        if (nextButton != null) nextButton.SetActive(false);
        if (choiceContainer != null) choiceContainer.SetActive(false);
        if (dialogueBox != null) dialogueBox.SetActive(false);
        StartCoroutine(FadeAmbient(0f, 1.2f));

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
                yield return FadeImageAlpha(fadeImage, 0f, 1f, 1.2f);
            }
        }

        yield return new WaitForSeconds(0.5f);

        if (string.IsNullOrEmpty(nextSceneName))
            nextSceneName = "A2_Croom";

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
}
