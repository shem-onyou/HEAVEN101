using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The last scene of Act 1: back in the bedroom in the afternoon after school, the player briefly looks back
// on the day. How they feel depends on their good points; the Act Results right after list the details.
public class A1_bedroom_lastscene : MonoBehaviour
{
    [Header("Cutscene Objects (auto-found by name if empty)")]
    public GameObject fadeScreenIn;
    public GameObject fadeOutScreen;
    public GameObject dialogueBox;
    public GameObject nextButton;
    public GameObject choiceContainer;
    [Tooltip("The bedroom background; Room Tint is applied to it.")]
    public GameObject background;

    [Header("Text UI References (auto-found by name if empty)")]
    public TMP_Text charNameText;
    public TMP_Text messageInputText;

    [Header("Typewriter Speed Settings")]
    [SerializeField] private float typingSpeed = 0.03f;

    [Header("Room Look")]
    [Tooltip("White keeps the bedroom as drawn. A soft warm color (e.g. R1 G0.93 B0.8) gives late-afternoon light.")]
    [SerializeField] private Color roomTint = Color.white;

    [Header("Reflection")]
    [Tooltip("Good points at or above this feel hopeful; below it, the player asks for forgiveness. Act 1 can reach up to 5.")]
    [SerializeField] private int goodPointsThreshold = 3;
    [SerializeField] private string playerName = "You";

    [Header("Dialogue (filled in automatically if empty)")]
    public List<string> introLines = new List<string>();
    [Tooltip("Said when the good points reach the threshold.")]
    public List<string> hopefulLines = new List<string>();
    [Tooltip("Said when the good points are below the threshold.")]
    public List<string> sorryLines = new List<string>();
    public List<string> outroLines = new List<string>();

    [Header("Sounds (auto-found by object or clip name if empty)")]
    [Tooltip("Afternoon sounds outside the window, looped. Found by \"ambient\". Stays silent until it has a clip.")]
    [SerializeField] private AudioSource ambientAudioSource;
    [Tooltip("The electric fan, looped. Found by \"fan\".")]
    [SerializeField] private AudioSource fanAudioSource;
    [Tooltip("The bedroom door as the player comes in. Found by \"door\".")]
    [SerializeField] private AudioSource doorAudioSource;
    [Tooltip("Optional: an AudioSource with the next_click clip.")]
    [SerializeField] private AudioSource clickAudioSource;
    [Tooltip("Plays when the Act Results show. Found by \"victory\" or \"bell\".")]
    [SerializeField] private AudioSource actCompleteAudioSource;

    [Header("Act Results (end of Act 1)")]
    [Tooltip("Marked as finished when the results show, which unlocks the next act on the Acts screen.")]
    [SerializeField] private int actNumber = 1;
    [SerializeField] private string actResultsTitle = "Act 1 Complete";
    [SerializeField] private string mainMenuSceneName = "Main Menu";
    [Tooltip("First scene of the next act. Empty means \"A2_Broom\".")]
    [SerializeField] private string nextActSceneName = "A2_Broom";

    private readonly List<string> playLines = new List<string>();
    private int currentLineIndex = 0;
    private Coroutine typingCoroutine;
    private bool isTyping;
    private bool isEnding;
    private float ambientVolume = 1f;
    private float fanVolume = 1f;

    void Start()
    {
        FillDefaultLines();

        playLines.AddRange(introLines);
        playLines.AddRange(PlayerDatabase.GoodPoints >= goodPointsThreshold ? hopefulLines : sorryLines);
        playLines.AddRange(outroLines);

        FindSceneReferences();

        if (choiceContainer != null) choiceContainer.SetActive(false);
        if (fadeOutScreen != null) fadeOutScreen.SetActive(false);
        if (dialogueBox != null) dialogueBox.SetActive(false);
        HoldBlackScreen();
        ApplyRoomTint();

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
        if (introLines.Count == 0)
        {
            introLines.Add("(Finally... my room.)");
            introLines.Add("(So much happened today...)");
        }

        if (hopefulLines.Count == 0)
        {
            hopefulLines.Add("(I think I tried to do what's right today.)");
            hopefulLines.Add("(Lord... is this what You were looking for in me?)");
        }

        if (sorryLines.Count == 0)
        {
            sorryLines.Add("(I don't think I did enough today...)");
            sorryLines.Add("(Lord... forgive me for where I fell short.)");
        }

        if (outroLines.Count == 0)
        {
            outroLines.Add("(I'll keep searching for the answer.)");
            outroLines.Add("(For now... I'll rest a little.)");
        }
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

        if (background == null)
            background = GameObject.Find("BG_bedroom");

        if (canvasTransform == null)
            return;

        if (dialogueBox == null) dialogueBox = FindDeepObject(canvasTransform, "DialogueBox");
        if (fadeScreenIn == null) fadeScreenIn = FindDeepObject(canvasTransform, "FadeIn");
        if (fadeOutScreen == null) fadeOutScreen = FindDeepObject(canvasTransform, "FadeOut");
        if (choiceContainer == null) choiceContainer = FindDeepObject(canvasTransform, "choice_Container");
        if (background == null) background = FindDeepObject(canvasTransform, "BG_bedroom");

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

    // This scene is a copy of the morning bedroom, so its rooster, birds, alarm etc. are silenced first;
    // only the sounds this script uses get played, at the right moment.
    private void FindSounds()
    {
        AudioSource[] audioSources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include);

        foreach (AudioSource audioSource in audioSources)
        {
            if (ambientAudioSource == null && SoundMatches(audioSource, "ambient"))
                ambientAudioSource = audioSource;
            else if (fanAudioSource == null && SoundMatches(audioSource, "fan"))
                fanAudioSource = audioSource;
            else if (doorAudioSource == null && SoundMatches(audioSource, "door"))
                doorAudioSource = audioSource;
            else if (clickAudioSource == null && SoundMatches(audioSource, "click"))
                clickAudioSource = audioSource;
            else if (actCompleteAudioSource == null && (SoundMatches(audioSource, "victory") || SoundMatches(audioSource, "bell")))
                actCompleteAudioSource = audioSource;
        }

        if (ambientAudioSource != null) ambientVolume = ambientAudioSource.volume;
        if (fanAudioSource != null) fanVolume = fanAudioSource.volume;

        foreach (AudioSource audioSource in audioSources)
        {
            audioSource.playOnAwake = false;
            audioSource.Stop();
            audioSource.loop = audioSource == ambientAudioSource || audioSource == fanAudioSource;
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

    private void ApplyRoomTint()
    {
        if (background == null)
            return;

        SpriteRenderer sprite = background.GetComponent<SpriteRenderer>();
        if (sprite != null) sprite.color = roomTint;

        Graphic graphic = background.GetComponent<Graphic>();
        if (graphic != null) graphic.color = roomTint;
    }

    // The bedroom door opens in the dark, then the room fades in with the fan humming.
    IEnumerator StartCutscene()
    {
        yield return new WaitForSeconds(0.5f);

        float doorWait = 0.5f;
        if (doorAudioSource != null && doorAudioSource.clip != null)
        {
            doorAudioSource.Play();
            doorWait = Mathf.Min(doorAudioSource.clip.length, 1.5f);
        }

        yield return new WaitForSeconds(doorWait);

        if (fanAudioSource != null) StartCoroutine(FadeAudio(fanAudioSource, 0f, fanVolume, 2f));
        if (ambientAudioSource != null) StartCoroutine(FadeAudio(ambientAudioSource, 0f, ambientVolume, 2f));

        yield return FadeFromBlack(2f);
        yield return new WaitForSeconds(0.5f);

        if (playLines.Count > 0)
            DisplayLine(playLines[currentLineIndex]);
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
        if (isEnding)
            return;

        PlayEffect(clickAudioSource);

        if (isTyping)
        {
            CompleteTypingInstantly();
            return;
        }

        if (currentLineIndex + 1 >= playLines.Count)
        {
            StartCoroutine(LieDownToRest());
            return;
        }

        currentLineIndex++;
        DisplayLine(playLines[currentLineIndex]);
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
            yield return new WaitForSeconds(typingSpeed);
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

    // The player lies down for an afternoon rest: the room slowly fades out with its sounds,
    // then the Act Results show.
    private IEnumerator LieDownToRest()
    {
        isEnding = true;
        if (nextButton != null) nextButton.SetActive(false);
        if (dialogueBox != null) dialogueBox.SetActive(false);

        const float restDuration = 2.5f;
        if (fanAudioSource != null && fanAudioSource.isPlaying)
            StartCoroutine(FadeAudio(fanAudioSource, fanAudioSource.volume, 0f, restDuration));
        if (ambientAudioSource != null && ambientAudioSource.isPlaying)
            StartCoroutine(FadeAudio(ambientAudioSource, ambientAudioSource.volume, 0f, restDuration));

        Image blackScreen = PrepareFadeOutImage();
        if (blackScreen != null)
            yield return FadeImageAlpha(blackScreen, 0f, 1f, restDuration);

        yield return new WaitForSeconds(1f);
        ShowActResults();
    }

    private void ShowActResults()
    {
        PlayerDatabase.MarkActCompleted(actNumber);
        PlayEffect(actCompleteAudioSource);

        Transform canvasTransform = fadeOutScreen != null ? fadeOutScreen.transform.parent : null;
        if (canvasTransform == null && dialogueBox != null)
            canvasTransform = dialogueBox.transform.parent;
        if (canvasTransform == null)
            return;

        // The scene was saved before Act 2 existed, so an empty value means the default.
        if (string.IsNullOrEmpty(nextActSceneName))
            nextActSceneName = "A2_Broom";

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
