using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public enum A1BedroomAction
{
    None,
    Flashback,
    Panic,
    GetDressed
}

[System.Serializable]
public class A1BedroomDialogueLine
{
    public A1BedroomAction actionBefore;
    public string speakerName;
    [TextArea(2, 5)] public string message;

    public A1BedroomDialogueLine(string name, string msg, A1BedroomAction action = A1BedroomAction.None)
    {
        speakerName = name;
        message = msg;
        actionBefore = action;
    }
}

public class A1_bedroom_control : MonoBehaviour
{
    // Choice id that SceneController saves for God's question in Act One ("Act One" scene, line 10).
    private const string GodQuestionChoiceId = "Act One_line10";

    [Header("Cutscene Objects (auto-found by name if empty)")]
    public GameObject fadeScreenIn;
    public GameObject fadeOutScreen;
    public GameObject charLord;
    public GameObject dialogueBox;
    public GameObject nextButton;
    [Tooltip("The bedroom background; it shakes when the player panics.")]
    public Transform backgroundTransform;

    [Header("Choice System Objects (auto-found by name if empty)")]
    public GameObject choiceContainer;
    public Button choiceButton1;
    public TMP_Text choiceButton1Text;
    [SerializeField, HideInInspector] private Button choiceButton2;
    [SerializeField] private string goChoiceText = "Go to the living room.";

    [Header("Text UI References (auto-found by name if empty)")]
    public TMP_Text charNameText;
    public TMP_Text messageInputText;

    [Header("Typewriter Speed Settings")]
    [SerializeField] private float typingSpeed = 0.03f;

    [Header("Dialogue Sequence")]
    public List<A1BedroomDialogueLine> dialogueList = new List<A1BedroomDialogueLine>();

    [Header("Waking Up")]
    [Tooltip("Seconds of darkness before the eyes start opening.")]
    [SerializeField] private float wakeUpDelay = 1f;
    [SerializeField] private float eyesOpenDuration = 1.5f;

    [Header("Flashback")]
    [Tooltip("Drag a Heaven picture here (e.g. Assets/Backgrounds/Heaven/SCENE). If empty, the flashback is a white flash.")]
    [SerializeField] private Sprite flashbackSprite;
    [TextArea(1, 3)]
    [SerializeField] private string flashbackEcho = "\"Tell me, child... Why should I open the gates to you?\"";
    [SerializeField] private float flashbackHoldDuration = 2.5f;

    [Header("Panic")]
    [SerializeField] private float panicShakeStrength = 12f;
    [SerializeField] private float panicShakeDuration = 0.5f;

    [Header("Sounds (auto-found by object or clip name if empty)")]
    [SerializeField] private AudioSource roosterAudioSource;
    [SerializeField] private AudioSource birdsAudioSource;
    [SerializeField] private AudioSource fanAudioSource;
    [SerializeField] private AudioSource wearingClothesAudioSource;
    [SerializeField] private AudioSource doorOpenCloseAudioSource;
    [Tooltip("Optional: an AudioSource with the next_click clip.")]
    [SerializeField] private AudioSource clickAudioSource;
    [Tooltip("Rings when the player remembers school. Found by \"alarm\" in the object or clip name.")]
    [SerializeField] private AudioSource alarmAudioSource;
    [Tooltip("Beats during the flashback. Found by \"beat\" in the object or clip name.")]
    [SerializeField] private AudioSource heartbeatAudioSource;
    [Range(0f, 1f)] [SerializeField] private float birdsVolume = 0.35f;
    [Tooltip("How long the alarm rings before it fades out.")]
    [SerializeField] private float alarmRingDuration = 2.5f;

    [Header("Scene Transition")]
    [SerializeField] private string nextSceneName = "A1_LivingRoom";

    private int currentLineIndex = 0;
    private Coroutine typingCoroutine;
    private bool isTyping;
    private bool isPlayingAction;
    private bool waitingForChoice;
    private bool isEndingDialogue;
    private float fanVolume = 1f;
    private float alarmVolume = 1f;
    private float heartbeatVolume = 1f;

    void Start()
    {
        if (dialogueList.Count == 0)
        {
            dialogueList.Add(new A1BedroomDialogueLine("You", "Huhhh?"));
            dialogueList.Add(new A1BedroomDialogueLine("You", "I'm alive."));
            dialogueList.Add(new A1BedroomDialogueLine("You", "Did that really happen?", A1BedroomAction.Flashback));
            dialogueList.Add(new A1BedroomDialogueLine("You", "God..."));
            foreach (string memory in GetGodAnswerMemory())
                dialogueList.Add(new A1BedroomDialogueLine("You", memory));
            dialogueList.Add(new A1BedroomDialogueLine("You", "How will I enter the gates of Heaven?"));
            dialogueList.Add(new A1BedroomDialogueLine("You", "Hmmmmm..."));
            dialogueList.Add(new A1BedroomDialogueLine("You", "Huh? Oh right!", A1BedroomAction.Panic));
            dialogueList.Add(new A1BedroomDialogueLine("You", "I gotta go to school, it's already late!"));
            dialogueList.Add(new A1BedroomDialogueLine("You", "All right, I'm set.", A1BedroomAction.GetDressed));
            dialogueList.Add(new A1BedroomDialogueLine("You", "..."));
        }

        FindSceneReferences();

        if (choiceContainer != null) choiceContainer.SetActive(false);
        if (fadeOutScreen != null) fadeOutScreen.SetActive(false);
        if (dialogueBox != null) dialogueBox.SetActive(false);
        HoldBlackScreen();
        PrepareSounds();

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

    // What the player remembers about their answer to God in Act One.
    private static string[] GetGodAnswerMemory()
    {
        string answer = PlayerDatabase.GetChoice(GodQuestionChoiceId);

        if (!string.IsNullOrEmpty(answer) && answer.ToLowerInvariant().Contains("good"))
            return new[]
            {
                "I told Him I've been good...",
                "Then why did it feel like that wasn't enough?"
            };

        if (!string.IsNullOrEmpty(answer) && answer.ToLowerInvariant().Contains("don't know"))
            return new[]
            {
                "He asked me why He should open the gates for me...",
                "And I couldn't even answer Him."
            };

        return new[] { "He asked me why He should open the gates for me..." };
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

        if (backgroundTransform == null)
        {
            GameObject backgroundObject = GameObject.Find("BG_bedroom");
            if (backgroundObject != null)
                backgroundTransform = backgroundObject.transform;
        }

        if (canvasTransform == null)
            return;

        if (dialogueBox == null) dialogueBox = FindDeepObject(canvasTransform, "DialogueBox");
        if (fadeScreenIn == null) fadeScreenIn = FindDeepObject(canvasTransform, "FadeIn");
        if (fadeOutScreen == null) fadeOutScreen = FindDeepObject(canvasTransform, "FadeOut");
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

    // Matches AudioSources by a word in their object name or their clip name.
    private void FindSounds()
    {
        AudioSource[] audioSources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include);

        foreach (AudioSource audioSource in audioSources)
        {
            if (roosterAudioSource == null && SoundMatches(audioSource, "rooster"))
                roosterAudioSource = audioSource;
            else if (birdsAudioSource == null && SoundMatches(audioSource, "bird"))
                birdsAudioSource = audioSource;
            else if (fanAudioSource == null && SoundMatches(audioSource, "fan"))
                fanAudioSource = audioSource;
            else if (wearingClothesAudioSource == null && SoundMatches(audioSource, "cloth"))
                wearingClothesAudioSource = audioSource;
            else if (doorOpenCloseAudioSource == null && SoundMatches(audioSource, "door"))
                doorOpenCloseAudioSource = audioSource;
            else if (clickAudioSource == null && SoundMatches(audioSource, "click"))
                clickAudioSource = audioSource;
            else if (alarmAudioSource == null && SoundMatches(audioSource, "alarm"))
                alarmAudioSource = audioSource;
            else if (heartbeatAudioSource == null && SoundMatches(audioSource, "beat"))
                heartbeatAudioSource = audioSource;
        }
    }

    private static bool SoundMatches(AudioSource audioSource, string keyword)
    {
        if (audioSource.gameObject.name.ToLowerInvariant().Contains(keyword))
            return true;

        return audioSource.clip != null && audioSource.clip.name.ToLowerInvariant().Contains(keyword);
    }

    // Everything stays silent until the player wakes up; the script decides when each sound plays.
    private void PrepareSounds()
    {
        foreach (AudioSource effect in new[] { roosterAudioSource, wearingClothesAudioSource, doorOpenCloseAudioSource, clickAudioSource })
        {
            if (effect == null)
                continue;

            effect.playOnAwake = false;
            effect.loop = false;
            effect.Stop();
        }

        if (birdsAudioSource != null)
        {
            birdsAudioSource.Stop();
            birdsAudioSource.loop = true;
        }

        if (fanAudioSource != null)
        {
            fanVolume = fanAudioSource.volume;
            fanAudioSource.Stop();
            fanAudioSource.loop = true;
        }

        if (alarmAudioSource != null)
        {
            alarmVolume = alarmAudioSource.volume;
            alarmAudioSource.playOnAwake = false;
            alarmAudioSource.Stop();
            alarmAudioSource.loop = true;
        }

        if (heartbeatAudioSource != null)
        {
            heartbeatVolume = heartbeatAudioSource.volume;
            heartbeatAudioSource.playOnAwake = false;
            heartbeatAudioSource.Stop();
            heartbeatAudioSource.loop = true;
        }
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
        yield return WakeUp();
        yield return new WaitForSeconds(0.5f);

        if (dialogueList.Count > 0)
            StartCoroutine(PlayLine(currentLineIndex));
    }

    // The FadeIn Animator would clear the screen by itself, so it is turned off;
    // the script opens the eyes instead.
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

    // The eyes open a little, close again, then open fully while the rooster crows
    // and the morning sounds come in.
    private IEnumerator WakeUp()
    {
        Image eyelids = fadeScreenIn != null ? fadeScreenIn.GetComponent<Image>() : null;

        yield return new WaitForSeconds(wakeUpDelay);
        PlayEffect(roosterAudioSource);

        if (fanAudioSource != null)
            StartCoroutine(FadeAudio(fanAudioSource, 0f, fanVolume, 3f));

        if (eyelids != null)
        {
            yield return FadeImageAlpha(eyelids, 1f, 0.6f, 0.6f);
            yield return FadeImageAlpha(eyelids, 0.6f, 1f, 0.3f);
            yield return new WaitForSeconds(0.4f);
            yield return FadeImageAlpha(eyelids, 1f, 0.3f, 0.5f);
            yield return FadeImageAlpha(eyelids, 0.3f, 0.85f, 0.25f);
            yield return new WaitForSeconds(0.2f);
            yield return FadeImageAlpha(eyelids, 0.85f, 0f, eyesOpenDuration);
        }

        if (birdsAudioSource != null)
            StartCoroutine(FadeAudio(birdsAudioSource, 0f, birdsVolume, 2f));

        if (fadeScreenIn != null)
            fadeScreenIn.SetActive(false);
    }

    public void NextButton()
    {
        if (isPlayingAction || waitingForChoice || isEndingDialogue)
            return;

        PlayEffect(clickAudioSource);

        if (isTyping)
        {
            CompleteTypingInstantly();
            return;
        }

        if (currentLineIndex + 1 >= dialogueList.Count)
        {
            ShowLivingRoomChoice();
            return;
        }

        currentLineIndex++;
        StartCoroutine(PlayLine(currentLineIndex));
    }

    private IEnumerator PlayLine(int index)
    {
        A1BedroomDialogueLine line = dialogueList[index];

        if (line.actionBefore != A1BedroomAction.None)
        {
            isPlayingAction = true;
            if (nextButton != null) nextButton.SetActive(false);
            yield return PerformAction(line.actionBefore);
            isPlayingAction = false;
        }

        DisplayLine(line);
    }

    private IEnumerator PerformAction(A1BedroomAction action)
    {
        switch (action)
        {
            case A1BedroomAction.Flashback:
                yield return Flashback();
                break;

            case A1BedroomAction.Panic:
                yield return Panic();
                break;

            case A1BedroomAction.GetDressed:
                yield return GetDressed();
                break;
        }
    }

    // A quick flash of Heaven while God's question echoes; the room goes quiet and the heart pounds.
    private IEnumerator Flashback()
    {
        Transform canvasTransform = dialogueBox != null ? dialogueBox.transform.parent : null;
        if (canvasTransform == null)
            yield break;

        if (dialogueBox != null) dialogueBox.SetActive(false);

        float birdsStartVolume = birdsAudioSource != null ? birdsAudioSource.volume : 0f;
        if (fanAudioSource != null) StartCoroutine(FadeAudio(fanAudioSource, fanAudioSource.volume, fanVolume * 0.2f, 0.4f));
        if (birdsAudioSource != null) StartCoroutine(FadeAudio(birdsAudioSource, birdsStartVolume, birdsStartVolume * 0.2f, 0.4f));
        if (heartbeatAudioSource != null) StartCoroutine(FadeAudio(heartbeatAudioSource, 0f, heartbeatVolume, 0.4f));

        Image flashImage = CreateFullScreenImage(canvasTransform, "FlashbackImage");
        if (flashbackSprite != null)
            flashImage.sprite = flashbackSprite;
        flashImage.color = new Color(1f, 1f, 1f, 0f);

        TMP_Text echoText = CreateCenterText(canvasTransform, "FlashbackEcho", flashbackEcho);

        yield return FadeImageAlpha(flashImage, 0f, 1f, 0.4f);
        yield return FadeTextAlpha(echoText, 0f, 1f, 0.8f);
        yield return new WaitForSeconds(flashbackHoldDuration);
        yield return FadeTextAlpha(echoText, 1f, 0f, 0.8f);
        yield return FadeImageAlpha(flashImage, 1f, 0f, 0.8f);

        Destroy(echoText.gameObject);
        Destroy(flashImage.gameObject);

        if (fanAudioSource != null) StartCoroutine(FadeAudio(fanAudioSource, fanAudioSource.volume, fanVolume, 0.8f));
        if (birdsAudioSource != null) StartCoroutine(FadeAudio(birdsAudioSource, birdsAudioSource.volume, birdsStartVolume, 0.8f));
        if (heartbeatAudioSource != null) StartCoroutine(FadeAudio(heartbeatAudioSource, heartbeatAudioSource.volume, 0f, 1.2f));

        yield return new WaitForSeconds(0.3f);
    }

    // The player suddenly remembers school: the alarm rings, the room jolts and the birds stop.
    private IEnumerator Panic()
    {
        if (birdsAudioSource != null)
            StartCoroutine(FadeAudio(birdsAudioSource, birdsAudioSource.volume, 0f, 0.3f));

        if (alarmAudioSource != null && alarmAudioSource.clip != null)
            StartCoroutine(RingAlarm());

        Transform boxTransform = dialogueBox != null ? dialogueBox.transform : null;
        Vector3 boxRestPosition = boxTransform != null ? boxTransform.localPosition : Vector3.zero;
        Vector3 backgroundRestPosition = backgroundTransform != null ? backgroundTransform.localPosition : Vector3.zero;

        float duration = Mathf.Max(panicShakeDuration, 0.01f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float strength = 1f - Mathf.Clamp01(elapsed / duration);
            Vector3 direction = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f);

            if (backgroundTransform != null)
                backgroundTransform.localPosition = backgroundRestPosition + direction * panicShakeStrength * strength;
            if (boxTransform != null)
                boxTransform.localPosition = boxRestPosition + direction * panicShakeStrength * strength;
            yield return null;
        }

        if (backgroundTransform != null) backgroundTransform.localPosition = backgroundRestPosition;
        if (boxTransform != null) boxTransform.localPosition = boxRestPosition;
    }

    private IEnumerator RingAlarm()
    {
        alarmAudioSource.volume = alarmVolume;
        alarmAudioSource.Play();
        yield return new WaitForSeconds(alarmRingDuration);
        yield return FadeAudio(alarmAudioSource, alarmAudioSource.volume, 0f, 1f);
        alarmAudioSource.volume = alarmVolume;
    }

    // The screen goes dark while the player gets dressed, then comes back.
    private IEnumerator GetDressed()
    {
        if (dialogueBox != null) dialogueBox.SetActive(false);

        Image blackScreen = PrepareFadeOutImage();
        if (blackScreen != null)
            yield return FadeImageAlpha(blackScreen, 0f, 1f, 0.6f);

        float dressingTime = 1.5f;
        if (wearingClothesAudioSource != null && wearingClothesAudioSource.clip != null)
        {
            PlayEffect(wearingClothesAudioSource);
            dressingTime = Mathf.Max(dressingTime, wearingClothesAudioSource.clip.length);
        }

        yield return new WaitForSeconds(dressingTime);

        if (blackScreen != null)
        {
            yield return FadeImageAlpha(blackScreen, 1f, 0f, 0.6f);
            fadeOutScreen.SetActive(false);
        }
    }

    private void DisplayLine(A1BedroomDialogueLine line)
    {
        if (dialogueBox != null) dialogueBox.SetActive(true);
        if (nextButton != null) nextButton.SetActive(true);
        if (choiceContainer != null) choiceContainer.SetActive(false);

        if (charNameText != null)
            charNameText.text = line.speakerName;

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        typingCoroutine = StartCoroutine(TypeText(line.message));
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

        if (currentLineIndex == dialogueList.Count - 1)
            ShowLivingRoomChoice();
    }

    private void ShowLivingRoomChoice()
    {
        waitingForChoice = true;
        if (nextButton != null) nextButton.SetActive(false);
        if (choiceContainer != null) choiceContainer.SetActive(true);

        if (choiceButton2 != null)
        {
            choiceButton2.onClick.RemoveAllListeners();
            choiceButton2.gameObject.SetActive(false);
        }

        if (choiceButton1 != null)
        {
            choiceButton1.gameObject.SetActive(true);
            if (choiceButton1Text != null) choiceButton1Text.text = goChoiceText;
            choiceButton1.onClick.RemoveAllListeners();
            choiceButton1.onClick.AddListener(() =>
            {
                PlayEffect(clickAudioSource);
                EndDialogue();
            });
        }
    }

    private void EndDialogue()
    {
        if (isEndingDialogue)
            return;

        isEndingDialogue = true;
        StartCoroutine(LeaveRoom());
    }

    // The door opens and closes while the room fades out and the morning sounds die down.
    private IEnumerator LeaveRoom()
    {
        if (nextButton != null) nextButton.SetActive(false);
        if (choiceContainer != null) choiceContainer.SetActive(false);
        if (dialogueBox != null) dialogueBox.SetActive(false);

        if (doorOpenCloseAudioSource != null && doorOpenCloseAudioSource.clip != null)
            doorOpenCloseAudioSource.Play();

        const float fadeDuration = 1.2f;
        if (fanAudioSource != null) StartCoroutine(FadeAudio(fanAudioSource, fanAudioSource.volume, 0f, fadeDuration));
        if (birdsAudioSource != null) StartCoroutine(FadeAudio(birdsAudioSource, birdsAudioSource.volume, 0f, fadeDuration));

        Image blackScreen = PrepareFadeOutImage();
        if (blackScreen != null)
            yield return FadeImageAlpha(blackScreen, 0f, 1f, fadeDuration);

        while (doorOpenCloseAudioSource != null && doorOpenCloseAudioSource.isPlaying)
            yield return null;

        yield return new WaitForSeconds(0.3f);

        if (string.IsNullOrEmpty(nextSceneName))
            nextSceneName = "A1_LivingRoom";

        SceneManager.LoadScene(nextSceneName);
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

    private static Image CreateFullScreenImage(Transform canvasTransform, string objectName)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform imageTransform = imageObject.GetComponent<RectTransform>();
        imageTransform.SetParent(canvasTransform, false);
        imageTransform.anchorMin = Vector2.zero;
        imageTransform.anchorMax = Vector2.one;
        imageTransform.offsetMin = Vector2.zero;
        imageTransform.offsetMax = Vector2.zero;
        imageTransform.SetAsLastSibling();

        Image image = imageObject.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private TMP_Text CreateCenterText(Transform canvasTransform, string objectName, string message)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
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
        text.fontSize = 32f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.alpha = 0f;
        text.raycastTarget = false;
        text.text = message;
        return text;
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

    // Starts the sound if needed and slides its volume; stops it when it reaches silence.
    private static IEnumerator FadeAudio(AudioSource audioSource, float fromVolume, float toVolume, float fadeDuration)
    {
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
