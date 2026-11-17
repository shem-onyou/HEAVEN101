using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// The first scene of Act 2: the act title shows on the black screen, then the player wakes up early,
// determined to do everything right today, gets dressed and heads to the living room.
public class A2_Broom_Control : MonoBehaviour
{
    [Header("Act Title (shown on the black screen before the scene)")]
    [SerializeField] private string actTitle = "ACT 2";
    [SerializeField] private string actSubtitle = "Doing Everything Right";
    [SerializeField] private float actTitleFontSize = 72f;
    [SerializeField] private float actSubtitleFontSize = 34f;
    [SerializeField] private float actTitleHoldDuration = 2.5f;

    [Header("Cutscene Objects (auto-found by name if empty)")]
    public GameObject fadeScreenIn;
    public GameObject fadeOutScreen;
    public GameObject dialogueBox;
    public GameObject nextButton;
    public GameObject choiceContainer;

    [Header("Text UI References (auto-found by name if empty)")]
    public TMP_Text charNameText;
    public TMP_Text messageInputText;

    [Header("Typewriter Speed Settings")]
    [SerializeField] private float typingSpeed = 0.03f;

    [Header("Dialogue")]
    [SerializeField] private string playerName = "You";
    [Tooltip("Said right after waking up, before getting dressed.")]
    public List<string> wakeUpLines = new List<string>();
    [Tooltip("Said after getting dressed, right before leaving the room.")]
    public List<string> readyLines = new List<string>();

    [Header("Sounds (auto-found by object or clip name if empty)")]
    [SerializeField] private AudioSource roosterAudioSource;
    [SerializeField] private AudioSource birdsAudioSource;
    [SerializeField] private AudioSource fanAudioSource;
    [SerializeField] private AudioSource wearingClothesAudioSource;
    [SerializeField] private AudioSource doorAudioSource;
    [Tooltip("Optional: an AudioSource with the next_click clip.")]
    [SerializeField] private AudioSource clickAudioSource;
    [Range(0f, 1f)] [SerializeField] private float birdsVolume = 0.35f;

    [Header("Scene Transition")]
    [SerializeField] private string nextSceneName = "A2_Lroom";

    private readonly List<string> playLines = new List<string>();
    private int getDressedBeforeIndex;
    private int currentLineIndex = 0;
    private Coroutine typingCoroutine;
    private bool isTyping;
    private bool isPlayingAction;
    private bool isEnding;
    private float fanVolume = 1f;

    void Start()
    {
        // This time the player is up early and full of resolve, not yet knowing today will test them.
        if (wakeUpLines.Count == 0)
        {
            wakeUpLines.Add("(...I'm awake. And I'm not late this time!)");
            wakeUpLines.Add("(I still can't stop thinking about what God told me.)");
            wakeUpLines.Add("(Starting today, I'll do everything right. No more mistakes.)");
            wakeUpLines.Add("(If I'm good enough... maybe that's the answer He's looking for.)");
        }

        if (readyLines.Count == 0)
        {
            readyLines.Add("(All right. Today's going to be a good day!)");
        }

        playLines.AddRange(wakeUpLines);
        getDressedBeforeIndex = playLines.Count;
        playLines.AddRange(readyLines);

        FindSceneReferences();

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

        FindSounds();

        if (canvasTransform == null)
            return;

        if (dialogueBox == null) dialogueBox = FindDeepObject(canvasTransform, "DialogueBox");
        if (fadeScreenIn == null) fadeScreenIn = FindDeepObject(canvasTransform, "FadeIn");
        if (fadeOutScreen == null) fadeOutScreen = FindDeepObject(canvasTransform, "FadeOut");
        if (choiceContainer == null) choiceContainer = FindDeepObject(canvasTransform, "choice_Container");

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

    // Matches AudioSources by a word in their object name or their clip name. Every sound in the scene
    // is silenced first (the bedroom copy may still have its alarm, heartbeat, etc.); only the ones
    // this script uses get played, at the right moment.
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
            else if (doorAudioSource == null && SoundMatches(audioSource, "door"))
                doorAudioSource = audioSource;
            else if (clickAudioSource == null && SoundMatches(audioSource, "click"))
                clickAudioSource = audioSource;
        }

        if (fanAudioSource != null)
            fanVolume = fanAudioSource.volume;

        foreach (AudioSource audioSource in audioSources)
        {
            audioSource.playOnAwake = false;
            audioSource.Stop();
            audioSource.loop = audioSource == birdsAudioSource || audioSource == fanAudioSource;
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

    IEnumerator StartCutscene()
    {
        yield return ShowActTitle();

        // Morning: the rooster crows, the fan hums and the room fades in.
        PlayEffect(roosterAudioSource);
        if (fanAudioSource != null) StartCoroutine(FadeAudio(fanAudioSource, 0f, fanVolume, 2f));
        if (birdsAudioSource != null) StartCoroutine(FadeAudio(birdsAudioSource, 0f, birdsVolume, 2f));

        yield return FadeFromBlack(2f);
        yield return new WaitForSeconds(0.5f);

        if (playLines.Count > 0)
            StartCoroutine(PlayLine(currentLineIndex));
    }

    // On the black screen: "Previously..." (what the player chose in Act 1), then "ACT 2" and the act's title.
    private IEnumerator ShowActTitle()
    {
        Transform canvasTransform = fadeScreenIn != null ? fadeScreenIn.transform.parent : null;
        if (canvasTransform == null && dialogueBox != null)
            canvasTransform = dialogueBox.transform.parent;

        yield return new WaitForSeconds(0.8f);

        if (canvasTransform == null)
            yield break;

        yield return ChapterCard.Previously(canvasTransform, 1);
        yield return ChapterCard.Title(canvasTransform, actTitle, actSubtitle, actTitleFontSize, actSubtitleFontSize, actTitleHoldDuration);
        yield return new WaitForSeconds(0.2f);
    }

    // The FadeIn Animator is turned off so the screen stays black until the act title is done.
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
        if (isPlayingAction || isEnding)
            return;

        PlayEffect(clickAudioSource);

        if (isTyping)
        {
            CompleteTypingInstantly();
            return;
        }

        if (currentLineIndex + 1 >= playLines.Count)
        {
            StartCoroutine(LeaveRoom());
            return;
        }

        currentLineIndex++;
        StartCoroutine(PlayLine(currentLineIndex));
    }

    private IEnumerator PlayLine(int index)
    {
        // Getting dressed happens between the wake-up thoughts and the "ready" line.
        if (index == getDressedBeforeIndex && index > 0)
        {
            isPlayingAction = true;
            if (nextButton != null) nextButton.SetActive(false);
            yield return GetDressed();
            isPlayingAction = false;
        }

        DisplayLine(playLines[index]);
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

    private void DisplayLine(string message)
    {
        if (dialogueBox != null) dialogueBox.SetActive(true);
        if (nextButton != null) nextButton.SetActive(true);

        if (charNameText != null)
            charNameText.text = playerName;

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        typingCoroutine = StartCoroutine(TypeText(message));
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

    // The door opens and closes while the room fades out and the morning sounds die down.
    private IEnumerator LeaveRoom()
    {
        isEnding = true;
        if (nextButton != null) nextButton.SetActive(false);
        if (dialogueBox != null) dialogueBox.SetActive(false);

        if (doorAudioSource != null && doorAudioSource.clip != null)
            doorAudioSource.Play();

        const float fadeDuration = 1.2f;
        if (fanAudioSource != null && fanAudioSource.isPlaying)
            StartCoroutine(FadeAudio(fanAudioSource, fanAudioSource.volume, 0f, fadeDuration));
        if (birdsAudioSource != null && birdsAudioSource.isPlaying)
            StartCoroutine(FadeAudio(birdsAudioSource, birdsAudioSource.volume, 0f, fadeDuration));

        Image blackScreen = PrepareFadeOutImage();
        if (blackScreen != null)
            yield return FadeImageAlpha(blackScreen, 0f, 1f, fadeDuration);

        while (doorAudioSource != null && doorAudioSource.isPlaying)
            yield return null;

        yield return new WaitForSeconds(0.3f);

        if (string.IsNullOrEmpty(nextSceneName))
            nextSceneName = "A2_Lroom";

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

    // Starts the sound if needed and slides its volume; stops it when it reaches silence.
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
