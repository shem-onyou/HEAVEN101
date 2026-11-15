using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public enum A1UmuwiAction
{
    None,
    NanayEnters
}

[System.Serializable]
public class A1UmuwiDialogueLine
{
    public A1UmuwiAction actionBefore;
    public string speakerName;
    [TextArea(2, 5)] public string message;

    public A1UmuwiDialogueLine(string name, string msg, A1UmuwiAction action = A1UmuwiAction.None)
    {
        speakerName = name;
        message = msg;
        actionBefore = action;
    }
}

public class A1_livingroom_uwii : MonoBehaviour
{
    [Header("Cutscene Objects (auto-found by name if empty)")]
    public GameObject fadeScreenIn;
    public GameObject fadeOutScreen;
    public GameObject dialogueBox;
    public GameObject nextButton;
    public GameObject choiceContainer;

    [Header("Nanay (auto-found by name if empty)")]
    public Image nanayImage;
    [SerializeField] private string nanayName = "Nanay";
    [SerializeField] private float nanayWalkDuration = 1.2f;

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
    [Tooltip("Evening house sounds, looped. Found by \"ambient\". Stays silent until it has a clip.")]
    [SerializeField] private AudioSource ambientAudioSource;
    [Tooltip("Optional: an AudioSource with the next_click clip.")]
    [SerializeField] private AudioSource clickAudioSource;
    [Tooltip("The front door as the player comes home. Found by \"door\".")]
    [SerializeField] private AudioSource doorAudioSource;
    [Tooltip("The player going up to the bedroom at the end. Found by \"stair\".")]
    [SerializeField] private AudioSource stairsAudioSource;

    // A new field (not the old nextSceneName, which is saved empty in the scene) so it points
    // to the night bedroom by default. The Act 1 results now show at the end of that scene.
    [Header("Scene Transition")]
    [SerializeField] private string bedroomSceneName = "A1_BR_lastscene";

    [Header("Dialogue Sequence")]
    public List<A1UmuwiDialogueLine> introLines = new List<A1UmuwiDialogueLine>();
    [Tooltip("Played when the player gave money to the pulubi in A1_School_Uwian.")]
    public List<A1UmuwiDialogueLine> gaveMoneyLines = new List<A1UmuwiDialogueLine>();
    [Tooltip("Played when the player did not give money to the pulubi in A1_School_Uwian.")]
    public List<A1UmuwiDialogueLine> refusedLines = new List<A1UmuwiDialogueLine>();
    public List<A1UmuwiDialogueLine> outroLines = new List<A1UmuwiDialogueLine>();

    private readonly List<A1UmuwiDialogueLine> playLines = new List<A1UmuwiDialogueLine>();
    private int currentLineIndex = 0;
    private Coroutine typingCoroutine;
    private bool isTyping;
    private bool isPlayingAction;
    private bool dialogueFinished;
    private Vector2 nanaySpotPosition;
    private Color nanayBaseColor = Color.white;
    private Coroutine nanayTintCoroutine;
    private Coroutine jitterCoroutine;
    private RectTransform jitterTarget;
    private Vector2 jitterRestPosition;
    private float ambientVolume = 1f;

    void Start()
    {
        if (introLines.Count == 0)
        {
            introLines.Add(new A1UmuwiDialogueLine("You", "Nanay, I'm home!"));
            introLines.Add(new A1UmuwiDialogueLine(nanayName, "Oh, anak! You're home. Kumusta ang school?", A1UmuwiAction.NanayEnters));
            introLines.Add(new A1UmuwiDialogueLine("You", "It was okay, Nanay. Ma'am taught us a lot today. We have a quiz tomorrow."));
            introLines.Add(new A1UmuwiDialogueLine(nanayName, "Hay nako, you look so tired. Did you eat already?"));
            introLines.Add(new A1UmuwiDialogueLine("You", "Opo. Elijah treated us to fishball on the way home."));
        }

        if (gaveMoneyLines.Count == 0)
        {
            gaveMoneyLines.Add(new A1UmuwiDialogueLine("You", "And Nanay... I gave some of my money to a pulubi near the school."));
            gaveMoneyLines.Add(new A1UmuwiDialogueLine(nanayName, "Ay, talaga? That was very kind of you, anak."));
            gaveMoneyLines.Add(new A1UmuwiDialogueLine(nanayName, "But wait... how did you get home? That was your pamasahe, right?"));
            gaveMoneyLines.Add(new A1UmuwiDialogueLine("You", "Hehe... my classmate gave me extra pamasahe."));
            gaveMoneyLines.Add(new A1UmuwiDialogueLine(nanayName, "Hay nako, you have good friends. Don't forget to thank them, ha?"));
        }

        if (refusedLines.Count == 0)
        {
            refusedLines.Add(new A1UmuwiDialogueLine("You", "Nanay... there was a pulubi near the school asking for money."));
            refusedLines.Add(new A1UmuwiDialogueLine("You", "I didn't give anything... but my classmate did."));
            refusedLines.Add(new A1UmuwiDialogueLine(nanayName, "I see... It's okay, anak."));
            refusedLines.Add(new A1UmuwiDialogueLine(nanayName, "But next time, if you can, help those who need it, ha?"));
            refusedLines.Add(new A1UmuwiDialogueLine("You", "Opo, Nanay."));
        }

        if (outroLines.Count == 0)
        {
            outroLines.Add(new A1UmuwiDialogueLine(nanayName, "Sige na, go upstairs and rest. Change out of your uniform, okay?"));
            outroLines.Add(new A1UmuwiDialogueLine(nanayName, "And study for your quiz, ha!"));
            outroLines.Add(new A1UmuwiDialogueLine("You", "Opo, Nanay!"));
        }

        playLines.AddRange(introLines);
        playLines.AddRange(A1_school_uwi.GaveMoneyToPulubi ? gaveMoneyLines : refusedLines);
        playLines.AddRange(outroLines);

        FindSceneReferences();

        if (choiceContainer != null) choiceContainer.SetActive(false);
        if (fadeOutScreen != null) fadeOutScreen.SetActive(false);
        if (dialogueBox != null) dialogueBox.SetActive(false);
        HoldBlackScreen();

        if (nanayImage != null)
        {
            nanaySpotPosition = nanayImage.rectTransform.anchoredPosition;
            nanayBaseColor = nanayImage.color;
            nanayImage.gameObject.SetActive(false);
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

        if (nanayImage == null)
        {
            GameObject nanayObject = FindDeepObject(canvasTransform, "Nanay");
            if (nanayObject != null) nanayImage = nanayObject.GetComponent<Image>();
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
            if (ambientAudioSource == null && SoundMatches(audioSource, "ambient"))
                ambientAudioSource = audioSource;
            else if (clickAudioSource == null && SoundMatches(audioSource, "click"))
                clickAudioSource = audioSource;
            else if (doorAudioSource == null && SoundMatches(audioSource, "door"))
                doorAudioSource = audioSource;
            else if (stairsAudioSource == null && SoundMatches(audioSource, "stair"))
                stairsAudioSource = audioSource;
        }

        // Every sound waits for its moment; nothing plays by itself when the scene starts.
        foreach (AudioSource effect in new[] { clickAudioSource, doorAudioSource, stairsAudioSource })
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

    // The front door opens in the dark, then the living room fades in.
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
        yield return FadeFromBlack(1.5f);
        StartCoroutine(FadeAmbient(ambientVolume, 1.5f));
        yield return new WaitForSeconds(0.5f);

        if (playLines.Count > 0)
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

    private IEnumerator FadeFromBlack(float fadeDuration)
    {
        if (fadeScreenIn == null)
            yield break;

        Image fadeImage = fadeScreenIn.GetComponent<Image>();
        if (fadeImage != null)
        {
            float duration = Mathf.Max(fadeDuration, 0.01f);
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
        if (isPlayingAction || dialogueFinished)
            return;

        PlayEffect(clickAudioSource);

        if (isTyping)
        {
            CompleteTypingInstantly();
            return;
        }

        if (currentLineIndex + 1 >= playLines.Count)
        {
            dialogueFinished = true;
            StartCoroutine(FadeOutScene());
            return;
        }

        currentLineIndex++;
        StartCoroutine(PlayLine(currentLineIndex));
    }

    private IEnumerator PlayLine(int index)
    {
        A1UmuwiDialogueLine line = playLines[index];

        if (line.actionBefore != A1UmuwiAction.None)
        {
            isPlayingAction = true;
            if (nextButton != null) nextButton.SetActive(false);
            StopJitter();
            yield return PerformAction(line.actionBefore);
            isPlayingAction = false;
        }

        DisplayLine(line);
    }

    private IEnumerator PerformAction(A1UmuwiAction action)
    {
        switch (action)
        {
            case A1UmuwiAction.NanayEnters:
                yield return NanayWalksIn();
                break;
        }
    }

    // Nanay walks in from the right edge to where she is placed in the editor.
    private IEnumerator NanayWalksIn()
    {
        if (nanayImage == null)
            yield break;

        RectTransform imageTransform = nanayImage.rectTransform;
        RectTransform parentTransform = imageTransform.parent as RectTransform;
        if (parentTransform == null)
            yield break;

        Vector2 startPosition = nanaySpotPosition + new Vector2(parentTransform.rect.width, 0f);
        imageTransform.anchoredPosition = startPosition;
        nanayImage.gameObject.SetActive(true);

        yield return MoveImage(imageTransform, startPosition, nanaySpotPosition, nanayWalkDuration);
    }

    private IEnumerator MoveImage(RectTransform imageTransform, Vector2 startPosition, Vector2 targetPosition, float moveDuration)
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

    private void DisplayLine(A1UmuwiDialogueLine line)
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

    // Nanay stays bright and jitters when she talks; she darkens when You is speaking.
    private void UpdateSpeakerEffects(string speakerName)
    {
        StopJitter();

        if (nanayImage == null)
            return;

        bool nanaySpeaking = speakerName == nanayName;
        Color targetColor = nanaySpeaking ? nanayBaseColor : Darken(nanayBaseColor);

        if (nanayTintCoroutine != null)
            StopCoroutine(nanayTintCoroutine);

        if (nanayImage.gameObject.activeInHierarchy)
            nanayTintCoroutine = StartCoroutine(TintImage(nanayImage, targetColor));
        else
        {
            nanayImage.color = targetColor;
            nanayTintCoroutine = null;
        }

        if (nanaySpeaking)
            StartJitter(nanayImage);
    }

    private Color Darken(Color baseColor)
    {
        return new Color(baseColor.r * darkenedTint.r, baseColor.g * darkenedTint.g, baseColor.b * darkenedTint.b, baseColor.a);
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
        if (characterImage == null || !characterImage.gameObject.activeInHierarchy)
            return;

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

    // The player heads upstairs (footsteps) while the room and its sounds fade away.
    private IEnumerator FadeOutScene()
    {
        StopJitter();
        if (nextButton != null) nextButton.SetActive(false);
        if (dialogueBox != null) dialogueBox.SetActive(false);

        if (stairsAudioSource != null && stairsAudioSource.clip != null)
            stairsAudioSource.Play();
        StartCoroutine(FadeAmbient(0f, 1.2f));

        if (fadeOutScreen != null)
            yield return FadeBlackScreen(0f, 1f);

        while (stairsAudioSource != null && stairsAudioSource.isPlaying)
            yield return null;

        yield return new WaitForSeconds(0.5f);

        if (string.IsNullOrEmpty(bedroomSceneName))
            bedroomSceneName = "A1_BR_lastscene";

        SceneManager.LoadScene(bedroomSceneName);
    }

    // The FadeOut object may carry an Animator (copied from FadeIn) that would fight
    // the script over the alpha, so it is turned off while the script drives the fade.
    private void PrepareFadeScreen(out CanvasGroup canvasGroup, out Image fadeImage)
    {
        fadeOutScreen.SetActive(true);

        Animator fadeAnimator = fadeOutScreen.GetComponent<Animator>();
        if (fadeAnimator != null)
            fadeAnimator.enabled = false;

        canvasGroup = fadeOutScreen.GetComponent<CanvasGroup>();
        fadeImage = fadeOutScreen.GetComponent<Image>();

        if (fadeImage != null)
            fadeImage.color = Color.black;

        if (canvasGroup == null && fadeImage == null)
            canvasGroup = fadeOutScreen.AddComponent<CanvasGroup>();
    }

    private IEnumerator FadeBlackScreen(float fromAlpha, float toAlpha)
    {
        PrepareFadeScreen(out CanvasGroup canvasGroup, out Image fadeImage);
        SetFadeAlpha(canvasGroup, fadeImage, fromAlpha);

        const float duration = 1f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetFadeAlpha(canvasGroup, fadeImage, Mathf.Lerp(fromAlpha, toAlpha, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetFadeAlpha(canvasGroup, fadeImage, toAlpha);
    }

    private static void SetFadeAlpha(CanvasGroup canvasGroup, Image fadeImage, float alpha)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = alpha;
            return;
        }

        Color color = fadeImage.color;
        color.a = alpha;
        fadeImage.color = color;
    }
}
