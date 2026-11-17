using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public enum A1SchoolAction
{
    None,
    ElijahEnters,
    ElijahRunsOff,
    PulubiAppears
}

[System.Serializable]
public class A1SchoolDialogueLine
{
    public A1SchoolAction actionBefore;
    public string speakerName;
    [TextArea(2, 5)] public string message;

    public A1SchoolDialogueLine(string name, string msg, A1SchoolAction action = A1SchoolAction.None)
    {
        speakerName = name;
        message = msg;
        actionBefore = action;
    }
}

public class A1_school : MonoBehaviour
{
    [Header("Cutscene Objects (auto-found by name if empty)")]
    public GameObject fadeScreenIn;
    public GameObject fadeOutScreen;
    public GameObject dialogueBox;
    public GameObject nextButton;

    [Header("Characters (auto-found by name if empty)")]
    public Image classmate1Image;
    public Image pulubiImage;
    [Tooltip("Only used if the scene has no \"pulubi\" object.")]
    public Sprite pulubiSprite;
    [SerializeField] private string classmate1Name = "Elijah";
    [SerializeField] private string pulubiName = "???";
    [SerializeField] private float pulubiMoveDuration = 2f;
    [SerializeField] private float classmateMoveDuration = 1f;

    [Header("Speaking Effects")]
    [SerializeField] private Color darkenedTint = new Color(0.45f, 0.45f, 0.45f, 1f);
    [SerializeField] private float tintDuration = 0.15f;
    [SerializeField] private float jitterDuration = 0.3f;
    [SerializeField] private float jitterStrength = 6f;
    [Tooltip("The pulubi trembles weakly instead of hopping.")]
    [SerializeField] private float pulubiTrembleStrength = 2f;
    [SerializeField] private float pulubiTrembleDuration = 0.8f;

    [Header("Choice System Objects (auto-found by name if empty)")]
    public GameObject choiceContainer;
    public Button choiceButton1;
    public TMP_Text choiceButton1Text;
    public Button choiceButton2;
    public TMP_Text choiceButton2Text;
    [SerializeField] private string cutClassChoiceText = "Cut the first subject";
    [SerializeField] private string goToClassChoiceText = "No, let's go to class";

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
    [SerializeField] private string nextSceneName = "A1_Classroom";

    [Header("Dialogue Sequence")]
    [Tooltip("Arriving late and meeting Elijah, up to the cut-class question.")]
    public List<A1SchoolDialogueLine> dialogueList = new List<A1SchoolDialogueLine>();
    public List<A1SchoolDialogueLine> cutClassLines = new List<A1SchoolDialogueLine>();
    public List<A1SchoolDialogueLine> goToClassLines = new List<A1SchoolDialogueLine>();
    [Tooltip("Played after either choice: Elijah runs ahead and the pulubi appears.")]
    public List<A1SchoolDialogueLine> pulubiLines = new List<A1SchoolDialogueLine>();

    private class SchoolCharacter
    {
        public Image image;
        public string speakerName;
        public Vector2 spotPosition;
        public Color baseColor = Color.white;
        public Coroutine tintCoroutine;
    }

    private SchoolCharacter elijah;
    private SchoolCharacter pulubi;

    private List<A1SchoolDialogueLine> currentLines;
    private int currentLineIndex = 0;
    private Coroutine typingCoroutine;
    private bool isTyping;
    private bool isPlayingAction;
    private bool waitingForChoice;
    private bool choiceMade;
    private bool pulubiPartPlaying;
    private bool isEnding;
    private float ambientVolume = 1f;

    private Coroutine jitterCoroutine;
    private RectTransform jitterTarget;
    private Vector2 jitterRestPosition;

    void Start()
    {
        if (dialogueList.Count == 0)
        {
            dialogueList.Add(new A1SchoolDialogueLine("You", "I'm so late! Ma'am will be mad!"));
            dialogueList.Add(new A1SchoolDialogueLine(classmate1Name, "...", A1SchoolAction.ElijahEnters));
            dialogueList.Add(new A1SchoolDialogueLine("You", "<link=\"shake\">HUY!</link> You're late too?!"));
            dialogueList.Add(new A1SchoolDialogueLine(classmate1Name, "Yeah, but it's fine. We don't have to rush."));
            dialogueList.Add(new A1SchoolDialogueLine("You", "But Ma'am will be mad if we're late!"));
            dialogueList.Add(new A1SchoolDialogueLine(classmate1Name, "Hmm... Let's just cut the first subject!"));
        }

        if (cutClassLines.Count == 0)
        {
            cutClassLines.Add(new A1SchoolDialogueLine("You", "Let's go!"));
            cutClassLines.Add(new A1SchoolDialogueLine(classmate1Name, "Wait... you really want to, huh?"));
            cutClassLines.Add(new A1SchoolDialogueLine(classmate1Name, "I'm just joking! HAHAHA, tara na!"));
        }

        if (goToClassLines.Count == 0)
        {
            goToClassLines.Add(new A1SchoolDialogueLine("You", "No way! I don't want to get in trouble."));
            goToClassLines.Add(new A1SchoolDialogueLine(classmate1Name, "I'm just joking! HAHAHA, tara na!"));
        }

        if (pulubiLines.Count == 0)
        {
            pulubiLines.Add(new A1SchoolDialogueLine("You", "Hoy, wait up!", A1SchoolAction.ElijahRunsOff));
            pulubiLines.Add(new A1SchoolDialogueLine(pulubiName, "So... hungry...", A1SchoolAction.PulubiAppears));
            pulubiLines.Add(new A1SchoolDialogueLine("You", "(Huh? Who's that...?)"));
            pulubiLines.Add(new A1SchoolDialogueLine("You", "(I'm already late... I have to go.)"));
        }

        FindSceneReferences();

        elijah = CreateCharacter(classmate1Image, classmate1Name);
        pulubi = CreateCharacter(pulubiImage, pulubiName);

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

        currentLines = dialogueList;
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
        if (pulubiImage == null) pulubiImage = FindImage(canvasTransform, "pulubi");
        if (pulubiImage == null) pulubiImage = CreatePulubiImage(canvasTransform);

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

    // Fallback for a scene without a "pulubi" object: builds one from pulubiSprite at Elijah's size.
    private Image CreatePulubiImage(Transform canvasTransform)
    {
        if (pulubiSprite == null)
            return null;

        GameObject pulubiObject = new GameObject("pulubi", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform pulubiTransform = pulubiObject.GetComponent<RectTransform>();
        pulubiTransform.SetParent(canvasTransform, false);

        Image image = pulubiObject.GetComponent<Image>();
        image.sprite = pulubiSprite;
        image.preserveAspect = true;

        if (classmate1Image != null)
        {
            RectTransform classmateTransform = classmate1Image.rectTransform;
            pulubiTransform.anchorMin = classmateTransform.anchorMin;
            pulubiTransform.anchorMax = classmateTransform.anchorMax;
            pulubiTransform.pivot = classmateTransform.pivot;
            pulubiTransform.sizeDelta = classmateTransform.sizeDelta;
            pulubiTransform.localScale = classmateTransform.localScale;
            pulubiTransform.anchoredPosition = new Vector2(-classmateTransform.anchoredPosition.x, classmateTransform.anchoredPosition.y);
        }
        else
        {
            image.SetNativeSize();
        }

        if (dialogueBox != null)
            pulubiTransform.SetSiblingIndex(dialogueBox.transform.GetSiblingIndex());

        return image;
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
            if (ambientAudioSource == null && SoundMatches(audioSource, "ambient"))
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

    IEnumerator StartCutscene()
    {
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

        // End of the current list: the intro leads to the choice, a choice path leads to
        // the pulubi, and the pulubi part ends the scene.
        if (!choiceMade)
            ShowSchoolChoice();
        else if (!pulubiPartPlaying)
            StartLines(pulubiLines, true);
        else
            StartCoroutine(FadeOutScene());
    }

    private void StartLines(List<A1SchoolDialogueLine> lines, bool isPulubiPart)
    {
        pulubiPartPlaying = isPulubiPart;
        currentLines = lines;
        currentLineIndex = 0;

        if (currentLines.Count > 0)
            StartCoroutine(PlayLine(currentLineIndex));
        else
            NextButton();
    }

    private IEnumerator PlayLine(int index)
    {
        A1SchoolDialogueLine line = currentLines[index];

        if (line.actionBefore != A1SchoolAction.None)
        {
            isPlayingAction = true;
            if (nextButton != null) nextButton.SetActive(false);
            StopJitter();
            yield return PerformAction(line.actionBefore);
            isPlayingAction = false;
        }

        DisplayLine(line);
    }

    private IEnumerator PerformAction(A1SchoolAction action)
    {
        switch (action)
        {
            case A1SchoolAction.ElijahEnters:
                yield return WalkIn(elijah, -1f, classmateMoveDuration);
                break;

            case A1SchoolAction.ElijahRunsOff:
                if (dialogueBox != null) dialogueBox.SetActive(false);
                yield return RunOffScreen(elijah, classmateMoveDuration);
                break;

            case A1SchoolAction.PulubiAppears:
                if (dialogueBox != null) dialogueBox.SetActive(false);
                yield return new WaitForSeconds(0.5f);
                yield return WalkIn(pulubi, -1f, pulubiMoveDuration);
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

        // Turns to face the right edge first, then runs off.
        Quaternion restRotation = imageTransform.localRotation;
        yield return CharacterTurn.FaceExit(imageTransform, 1f);
        yield return MoveImage(imageTransform, startPosition, exitPosition, duration);
        character.image.gameObject.SetActive(false);
        imageTransform.localRotation = restRotation;
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

    private void DisplayLine(A1SchoolDialogueLine line)
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

    // The speaker stays bright (Elijah hops, the pulubi trembles weakly); everyone else darkens.
    private void UpdateSpeakerEffects(string speakerName)
    {
        StopJitter();

        foreach (SchoolCharacter character in new[] { elijah, pulubi })
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
                bool trembling = character == pulubi;
                StartJitter(character.image,
                    trembling ? pulubiTrembleStrength : jitterStrength,
                    trembling ? pulubiTrembleDuration : jitterDuration,
                    !trembling);
            }
        }
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

    private void StartJitter(Image characterImage, float strength, float duration, bool hop)
    {
        jitterTarget = characterImage.rectTransform;
        jitterRestPosition = jitterTarget.anchoredPosition;
        jitterCoroutine = StartCoroutine(Jitter(strength, duration, hop));
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

    // A small hop with a side wiggle (lively), or just a faint side tremble (weak and hungry).
    private IEnumerator Jitter(float strength, float jitterTime, bool hop)
    {
        float duration = Mathf.Max(jitterTime, 0.01f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float wiggleX = Mathf.Sin(elapsed * 60f) * strength * (1f - progress);
            float hopY = hop ? Mathf.Sin(progress * Mathf.PI) * strength : 0f;
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
            CharacterVoice.Speak(charNameText != null ? charNameText.text : null, messageInputText, i);
            yield return new WaitForSeconds(DialoguePacing.Delay(messageInputText, i, typingSpeed));
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

    private void ShowSchoolChoice()
    {
        waitingForChoice = true;
        if (nextButton != null) nextButton.SetActive(false);
        if (choiceContainer != null) choiceContainer.SetActive(true);

        SetupChoiceButton(choiceButton1, choiceButton1Text, cutClassChoiceText, () =>
        {
            PlayerDatabase.RecordChoice("act1_cut_class", "Elijah: Let's cut the first subject!", cutClassChoiceText, -1, "cut");
            MakeChoice(cutClassLines);
        });

        SetupChoiceButton(choiceButton2, choiceButton2Text, goToClassChoiceText, () =>
        {
            PlayerDatabase.RecordChoice("act1_cut_class", "Elijah: Let's cut the first subject!", goToClassChoiceText, 1, "go_to_class");
            MakeChoice(goToClassLines);
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

    private void MakeChoice(List<A1SchoolDialogueLine> lines)
    {
        if (!waitingForChoice)
            return;

        waitingForChoice = false;
        choiceMade = true;
        if (choiceContainer != null) choiceContainer.SetActive(false);
        StartLines(lines, false);
    }

    // The school fades to black (and its sounds die down) before the classroom loads.
    private IEnumerator FadeOutScene()
    {
        isEnding = true;
        StopJitter();
        if (nextButton != null) nextButton.SetActive(false);
        if (choiceContainer != null) choiceContainer.SetActive(false);
        if (dialogueBox != null) dialogueBox.SetActive(false);

        if (ambientAudioSource != null && ambientAudioSource.isPlaying)
            StartCoroutine(FadeAudio(ambientAudioSource, ambientAudioSource.volume, 0f, 1.2f));

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
            nextSceneName = "A1_Classroom";

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
}
