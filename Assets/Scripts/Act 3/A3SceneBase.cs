using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// One line of Act 3 dialogue. "actions" run before the line is shown, separated by ';', for example:
//   "enter:Nanay"     walk a character in      "leave:pulubi"   walk a character out
//   "show:Nanay"      appear on the spot        "hide:Nanay"     disappear
//   "deed"            +1 on the Good Deeds counter
//   "pause" / "pause:2"   wait with the dialogue box hidden
//   "shake"           shake the screen          "sfx:cough"      play a sound found by that word
// A line with an empty message only runs its actions, then moves on by itself.
[System.Serializable]
public class A3Line
{
    [Tooltip("Optional actions before the line, separated by ';' (e.g. \"enter:Nanay;sfx:cough\").")]
    public string actions;
    public string speakerName;
    [TextArea(2, 5)] public string message;

    public A3Line(string name, string msg, string actionsBefore = "")
    {
        speakerName = name;
        message = msg;
        actions = actionsBefore;
    }
}

// The number the player keeps in the corner of the screen during Act 3.
// It is only the player's own count: it gives no points by itself.
public static class A3GoodDeeds
{
    public const string ActionId = "act3_good_deeds";

    public static int Count
    {
        get
        {
            PlayerActionRecord record = PlayerDatabase.Data.actions.Find(action => action.actionId == ActionId);
            return record != null ? Mathf.RoundToInt(record.value) : 0;
        }
    }

    public static void Reset()
    {
        Save(0);
    }

    public static int Add(int amount = 1)
    {
        int count = Count + amount;
        Save(count);
        return count;
    }

    private static void Save(int count)
    {
        PlayerDatabase.RecordAction(ActionId, $"Counted {count} good deeds in Act 3", count, 0);
    }
}

// Shared engine for every Act 3 scene: finds the scene objects by name, plays dialogue lines with
// actions, shows choices, fades, sounds, speaker dim/jitter, and the Good Deeds counter.
// Each scene script only sets up its characters and lines, and decides the order of things.
public abstract class A3SceneBase : MonoBehaviour
{
    [Header("Cutscene Objects (auto-found by name if empty)")]
    public GameObject fadeScreenIn;
    public GameObject fadeOutScreen;
    public GameObject dialogueBox;
    public GameObject nextButton;
    public GameObject choiceContainer;
    public Button choiceButton1;
    public TMP_Text choiceButton1Text;
    public Button choiceButton2;
    public TMP_Text choiceButton2Text;

    [Header("Text UI References (auto-found by name if empty)")]
    public TMP_Text charNameText;
    public TMP_Text messageInputText;

    [Header("Names")]
    [SerializeField] protected string playerName = "You";

    [Header("Speaking Effects")]
    [SerializeField] protected Color darkenedTint = new Color(0.45f, 0.45f, 0.45f, 1f);
    [SerializeField] protected float tintDuration = 0.15f;
    [SerializeField] protected float jitterDuration = 0.3f;
    [SerializeField] protected float jitterStrength = 6f;
    [SerializeField] protected float trembleStrength = 2f;
    [SerializeField] protected float trembleDuration = 0.8f;
    [SerializeField] protected float walkDuration = 1f;

    [Header("Typewriter")]
    [SerializeField] protected float typingSpeed = 0.03f;
    [Tooltip("The player's quiet thoughts \"(...)\" type out slower.")]
    [SerializeField] protected float slowTypingSpeed = 0.055f;

    [Header("Good Deeds Counter")]
    [SerializeField] protected bool showGoodDeedsCounter = true;
    [SerializeField] protected float counterFontSize = 30f;

    protected class A3Character
    {
        public Image image;
        public string speakerName;
        public Vector2 spotPosition;
        public Vector3 baseScale = Vector3.one;
        public Color baseColor = Color.white;
        public bool trembles;
        public Coroutine tintCoroutine;
    }

    protected Transform canvasTransform;
    protected SpriteRenderer backgroundRenderer;
    protected Color backgroundBaseColor = Color.white;
    protected Vector3 backgroundRestPosition;
    protected AudioSource clickAudioSource;
    protected AudioSource ambientAudioSource;
    protected TMP_Text counterText;
    protected bool counterShouldShow;
    protected bool isEnding;
    protected bool waitingForChoice;

    private readonly Dictionary<string, A3Character> characters = new Dictionary<string, A3Character>();
    private readonly List<AudioSource> sceneSounds = new List<AudioSource>();
    private readonly Dictionary<AudioSource, float> originalVolumes = new Dictionary<AudioSource, float>();

    private List<A3Line> currentLines;
    private int currentLineIndex;
    private System.Action onLinesFinished;
    private Coroutine typingCoroutine;
    private bool isTyping;
    private bool isPlayingAction;
    private Coroutine jitterCoroutine;
    private RectTransform jitterTarget;
    private Vector2 jitterRestPosition;
    private Vector3 counterBaseScale = Vector3.one;
    private Coroutine counterBumpCoroutine;

    // ------------------------------------------------------------------ setup

    protected virtual void Start()
    {
        FindCoreReferences();
        CollectSounds();
        SetupScene();

        if (choiceContainer != null) choiceContainer.SetActive(false);
        if (fadeOutScreen != null) fadeOutScreen.SetActive(false);
        if (dialogueBox != null) dialogueBox.SetActive(false);
        HoldBlackScreen();
        BindNextButton();

        if (showGoodDeedsCounter)
            CreateCounter(CounterVisibleAtStart);

        StartCoroutine(RunScene());
    }

    // Register characters, find extra sounds, fill the default lines.
    protected abstract void SetupScene();

    // The scene's whole flow, from the black screen to loading the next scene.
    protected abstract IEnumerator RunScene();

    // Scene-specific actions (anything the base does not know).
    protected virtual IEnumerator PerformCustomAction(string action, string argument)
    {
        yield break;
    }

    protected virtual bool CounterVisibleAtStart => true;

    // Replace {tokens} in a line before it is shown.
    protected virtual string FillTokens(string message)
    {
        return message.Replace("{deeds}", A3GoodDeeds.Count.ToString());
    }

    private void FindCoreReferences()
    {
        GameObject canvasObject = GameObject.Find("Canvas");
        if (canvasObject != null)
            canvasTransform = canvasObject.transform;
        else
        {
            Canvas anyCanvas = FindAnyObjectByType<Canvas>();
            if (anyCanvas != null) canvasTransform = anyCanvas.transform;
        }

        foreach (SpriteRenderer sprite in FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (sprite.gameObject.name.StartsWith("BG_"))
            {
                backgroundRenderer = sprite;
                backgroundBaseColor = sprite.color;
                backgroundRestPosition = sprite.transform.localPosition;
                break;
            }
        }

        if (canvasTransform == null)
            return;

        if (dialogueBox == null) dialogueBox = FindDeepObject(canvasTransform, "DialogueBox");
        if (fadeScreenIn == null) fadeScreenIn = FindDeepObject(canvasTransform, "FadeIn");
        if (fadeOutScreen == null) fadeOutScreen = FindDeepObject(canvasTransform, "FadeOut");
        if (choiceContainer == null) choiceContainer = FindDeepObject(canvasTransform, "choice_Container");

        if (choiceContainer != null)
        {
            if (choiceButton1 == null) choiceButton1 = FindComponent<Button>(choiceContainer.transform, "choice_Button1");
            if (choiceButton2 == null) choiceButton2 = FindComponent<Button>(choiceContainer.transform, "choice_Button2");

            // Some scenes only have one choice button: the second one is made from the first.
            if (choiceButton2 == null && choiceButton1 != null)
            {
                GameObject copy = Instantiate(choiceButton1.gameObject, choiceButton1.transform.parent);
                copy.name = "choice_Button2";
                copy.transform.SetSiblingIndex(choiceButton1.transform.GetSiblingIndex() + 1);
                choiceButton2 = copy.GetComponent<Button>();

                if (choiceButton1.transform.parent.GetComponent<LayoutGroup>() == null)
                {
                    RectTransform firstRect = choiceButton1.transform as RectTransform;
                    RectTransform copyRect = copy.transform as RectTransform;
                    if (firstRect != null && copyRect != null)
                        copyRect.anchoredPosition = firstRect.anchoredPosition - new Vector2(0f, firstRect.rect.height * Mathf.Abs(firstRect.localScale.y) * 1.3f);
                }
            }

            if (choiceButton1Text == null && choiceButton1 != null) choiceButton1Text = choiceButton1.GetComponentInChildren<TMP_Text>(true);
            if (choiceButton2Text == null && choiceButton2 != null) choiceButton2Text = choiceButton2.GetComponentInChildren<TMP_Text>(true);
        }

        if (dialogueBox == null)
            return;

        if (charNameText == null) charNameText = FindComponent<TMP_Text>(dialogueBox.transform, "CharacterName");
        if (messageInputText == null) messageInputText = FindComponent<TMP_Text>(dialogueBox.transform, "MessageOutput");

        if (nextButton == null)
        {
            Button foundButton = dialogueBox.GetComponentInChildren<Button>(true);
            if (foundButton != null) nextButton = foundButton.gameObject;
        }
    }

    private void BindNextButton()
    {
        if (nextButton == null)
            return;

        Button button = nextButton.GetComponent<Button>();
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(NextButton);
    }

    protected static GameObject FindDeepObject(Transform parent, string objectName)
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

    protected static T FindComponent<T>(Transform parent, string objectName) where T : Component
    {
        GameObject found = FindDeepObject(parent, objectName);
        return found != null ? found.GetComponent<T>() : null;
    }

    // Finds an object anywhere in the scene (even inactive, even outside the Canvas).
    protected static GameObject FindSceneObject(string objectName)
    {
        foreach (Transform sceneTransform in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (sceneTransform.name == objectName && sceneTransform.gameObject.scene.IsValid())
                return sceneTransform.gameObject;
        }

        return null;
    }

    // ------------------------------------------------------------------ sounds

    // Every sound copied from other scenes is stopped; each one waits for its moment.
    private void CollectSounds()
    {
        foreach (AudioSource audioSource in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            sceneSounds.Add(audioSource);
            originalVolumes[audioSource] = audioSource.volume;
            audioSource.playOnAwake = false;
            audioSource.Stop();
        }

        clickAudioSource = FindSound("click");
        ambientAudioSource = FindSound("ambient", "ambien");
        if (clickAudioSource != null) clickAudioSource.loop = false;
        if (ambientAudioSource != null) ambientAudioSource.loop = true;
    }

    // Finds a sound by a word in its object name or its clip name.
    protected AudioSource FindSound(params string[] keywords)
    {
        foreach (AudioSource audioSource in sceneSounds)
        {
            foreach (string keyword in keywords)
            {
                if (SoundMatches(audioSource, keyword))
                    return audioSource;
            }
        }

        return null;
    }

    private static bool SoundMatches(AudioSource audioSource, string keyword)
    {
        if (audioSource == null)
            return false;

        if (audioSource.gameObject.name.ToLowerInvariant().Contains(keyword))
            return true;

        return audioSource.clip != null && audioSource.clip.name.ToLowerInvariant().Contains(keyword);
    }

    protected float OriginalVolume(AudioSource audioSource)
    {
        return audioSource != null && originalVolumes.TryGetValue(audioSource, out float volume) ? volume : 1f;
    }

    protected static void PlayEffect(AudioSource effect)
    {
        if (effect != null && effect.clip != null)
            effect.PlayOneShot(effect.clip);
    }

    protected static float EffectLength(AudioSource effect, float fallback, float max)
    {
        return effect != null && effect.clip != null ? Mathf.Min(effect.clip.length, max) : fallback;
    }

    // Starts the sound if needed and slides its volume; stops it when it reaches silence.
    protected IEnumerator FadeAudio(AudioSource audioSource, float toVolume, float fadeDuration)
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

    protected void StartLoop(AudioSource audioSource, float volumeScale, float fadeDuration)
    {
        if (audioSource == null)
            return;

        audioSource.loop = true;
        StartCoroutine(FadeAudio(audioSource, OriginalVolume(audioSource) * volumeScale, fadeDuration));
    }

    protected void StopAllSounds(float fadeDuration)
    {
        foreach (AudioSource audioSource in sceneSounds)
        {
            if (audioSource != null && audioSource.isPlaying)
                StartCoroutine(FadeAudio(audioSource, 0f, fadeDuration));
        }
    }

    // ------------------------------------------------------------------ characters

    protected A3Character AddCharacter(string objectName, string speakerName, bool trembles = false)
    {
        Image image = null;
        if (canvasTransform != null) image = FindComponent<Image>(canvasTransform, objectName);
        if (image == null)
        {
            GameObject found = FindSceneObject(objectName);
            if (found != null) image = found.GetComponent<Image>();
        }

        A3Character character = new A3Character { image = image, speakerName = speakerName, trembles = trembles };
        if (image != null)
        {
            character.spotPosition = image.rectTransform.anchoredPosition;
            character.baseScale = image.rectTransform.localScale;
            character.baseColor = image.color;
            image.gameObject.SetActive(false);
        }

        characters[objectName] = character;
        return character;
    }

    protected A3Character GetCharacter(string objectName)
    {
        return objectName != null && characters.TryGetValue(objectName, out A3Character character) ? character : null;
    }

    protected static bool IsOnScreen(A3Character character)
    {
        return character != null && character.image != null && character.image.gameObject.activeSelf;
    }

    protected void ShowCharacter(A3Character character)
    {
        if (character == null || character.image == null)
            return;

        character.image.rectTransform.anchoredPosition = character.spotPosition;
        character.image.rectTransform.localScale = character.baseScale;
        character.image.gameObject.SetActive(true);
    }

    // If two characters were left standing on top of each other in the editor, spread them apart.
    protected static void SpreadIfStacked(A3Character left, A3Character right, float gapFraction = 0.3f)
    {
        if (left?.image == null || right?.image == null)
            return;

        RectTransform parentTransform = left.image.rectTransform.parent as RectTransform;
        if (parentTransform == null)
            return;

        float gap = parentTransform.rect.width * gapFraction;
        if (Mathf.Abs(left.spotPosition.x - right.spotPosition.x) >= gap * 0.5f)
            return;

        Vector2 middle = (left.spotPosition + right.spotPosition) * 0.5f;
        left.spotPosition = middle - new Vector2(gap * 0.5f, 0f);
        right.spotPosition = middle + new Vector2(gap * 0.5f, 0f);
    }

    protected static float SideOf(A3Character character)
    {
        return character.spotPosition.x < 0f ? -1f : 1f;
    }

    protected IEnumerator WalkIn(A3Character character, float duration)
    {
        if (character == null || character.image == null)
            yield break;

        RectTransform imageTransform = character.image.rectTransform;
        RectTransform parentTransform = imageTransform.parent as RectTransform;
        float distance = parentTransform != null ? parentTransform.rect.width : 1500f;

        Vector2 startPosition = character.spotPosition + new Vector2(SideOf(character) * distance, 0f);
        imageTransform.anchoredPosition = startPosition;
        character.image.gameObject.SetActive(true);

        yield return MoveImage(imageTransform, startPosition, character.spotPosition, duration);
    }

    protected IEnumerator WalkOut(A3Character character, float duration)
    {
        if (!IsOnScreen(character))
            yield break;

        RectTransform imageTransform = character.image.rectTransform;
        RectTransform parentTransform = imageTransform.parent as RectTransform;
        float distance = parentTransform != null ? parentTransform.rect.width : 1500f;

        Vector2 startPosition = imageTransform.anchoredPosition;
        yield return MoveImage(imageTransform, startPosition, startPosition + new Vector2(SideOf(character) * distance, 0f), duration);
        character.image.gameObject.SetActive(false);
    }

    protected IEnumerator MoveImage(RectTransform imageTransform, Vector2 startPosition, Vector2 targetPosition, float moveDuration)
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

    protected IEnumerator ScaleImage(RectTransform imageTransform, Vector3 startScale, Vector3 targetScale, float scaleDuration)
    {
        float duration = Mathf.Max(scaleDuration, 0.01f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            imageTransform.localScale = Vector3.Lerp(startScale, targetScale, progress);
            yield return null;
        }

        imageTransform.localScale = targetScale;
    }

    // ------------------------------------------------------------------ dialogue

    // Plays a list of lines; when the player presses Next on the last one, onFinished runs.
    protected void StartLines(List<A3Line> lines, System.Action onFinished)
    {
        currentLines = lines;
        currentLineIndex = 0;
        onLinesFinished = onFinished;

        if (currentLines != null && currentLines.Count > 0)
            StartCoroutine(PlayLine(currentLineIndex));
        else
            FinishLines();
    }

    // Same as StartLines, but can be waited on inside a coroutine.
    protected IEnumerator PlayLines(List<A3Line> lines)
    {
        bool finished = false;
        StartLines(lines, () => finished = true);
        while (!finished)
            yield return null;
    }

    private void FinishLines()
    {
        System.Action next = onLinesFinished;
        onLinesFinished = null;
        next?.Invoke();
    }

    public void NextButton()
    {
        if (isPlayingAction || waitingForChoice || isEnding || currentLines == null)
            return;

        PlayEffect(clickAudioSource);

        if (isTyping)
        {
            CompleteTypingInstantly();
            return;
        }

        GoToNextLine();
    }

    private void GoToNextLine()
    {
        if (currentLineIndex + 1 < currentLines.Count)
        {
            currentLineIndex++;
            StartCoroutine(PlayLine(currentLineIndex));
            return;
        }

        FinishLines();
    }

    private IEnumerator PlayLine(int index)
    {
        A3Line line = currentLines[index];

        if (!string.IsNullOrWhiteSpace(line.actions))
        {
            isPlayingAction = true;
            if (nextButton != null) nextButton.SetActive(false);
            StopJitter();

            foreach (string part in line.actions.Split(';'))
            {
                string trimmed = part.Trim();
                if (trimmed.Length == 0)
                    continue;

                int colon = trimmed.IndexOf(':');
                string action = colon >= 0 ? trimmed.Substring(0, colon).Trim().ToLowerInvariant() : trimmed.ToLowerInvariant();
                string argument = colon >= 0 ? trimmed.Substring(colon + 1).Trim() : "";
                yield return PerformAction(action, argument);
            }

            isPlayingAction = false;
        }

        // An action-only line moves on by itself.
        if (string.IsNullOrEmpty(line.message))
        {
            GoToNextLine();
            yield break;
        }

        DisplayLine(line);
    }

    private IEnumerator PerformAction(string action, string argument)
    {
        A3Character character = GetCharacter(argument);

        switch (action)
        {
            case "enter":
                yield return WalkIn(character, walkDuration);
                break;
            case "enterslow":
                yield return WalkIn(character, walkDuration * 2f);
                break;
            case "leave":
                yield return WalkOut(character, walkDuration);
                break;
            case "leaveslow":
                yield return WalkOut(character, walkDuration * 2.5f);
                break;
            case "show":
                ShowCharacter(character);
                break;
            case "hide":
                if (character != null && character.image != null) character.image.gameObject.SetActive(false);
                break;
            case "deed":
                AddDeed(string.IsNullOrEmpty(argument) ? 1 : ParseInt(argument, 1));
                yield return new WaitForSeconds(0.35f);
                break;
            case "counter":
                yield return ShowCounter(0.8f);
                break;
            case "pause":
                HideDialogueBox();
                yield return new WaitForSeconds(string.IsNullOrEmpty(argument) ? 1.2f : ParseFloat(argument, 1.2f));
                break;
            case "shake":
                yield return ShakeScreen(string.IsNullOrEmpty(argument) ? 12f : ParseFloat(argument, 12f), 0.5f);
                break;
            case "sfx":
                PlayEffect(FindSound(argument.ToLowerInvariant()));
                break;
            case "hidebox":
                HideDialogueBox();
                break;
            default:
                yield return PerformCustomAction(action, argument);
                break;
        }
    }

    protected static float ParseFloat(string text, float fallback)
    {
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : fallback;
    }

    protected static int ParseInt(string text, int fallback)
    {
        return int.TryParse(text, out int value) ? value : fallback;
    }

    protected void HideDialogueBox()
    {
        if (dialogueBox != null) dialogueBox.SetActive(false);
    }

    private void DisplayLine(A3Line line)
    {
        if (dialogueBox != null) dialogueBox.SetActive(true);
        if (nextButton != null) nextButton.SetActive(true);
        if (choiceContainer != null) choiceContainer.SetActive(false);

        if (charNameText != null)
            charNameText.text = line.speakerName;

        UpdateSpeakerEffects(line.speakerName);
        OnLineShown(line);

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        typingCoroutine = StartCoroutine(TypeText(FillTokens(line.message), TypingSpeedFor(line)));
    }

    // For scene-specific speaker effects (e.g. God, who is a SpriteRenderer, not a UI Image).
    protected virtual void OnLineShown(A3Line line)
    {
    }

    protected virtual float TypingSpeedFor(A3Line line)
    {
        bool isThought = line.speakerName == playerName && line.message.StartsWith("(");
        return isThought ? slowTypingSpeed : typingSpeed;
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
            yield return new WaitForSeconds(speed);
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

    // ------------------------------------------------------------------ choices

    // secondLabel = null shows a single button.
    protected void ShowChoice(string firstLabel, System.Action firstChosen, string secondLabel = null, System.Action secondChosen = null)
    {
        waitingForChoice = true;
        StopJitter();
        if (nextButton != null) nextButton.SetActive(false);
        if (choiceContainer != null) choiceContainer.SetActive(true);

        SetupChoiceButton(choiceButton1, choiceButton1Text, firstLabel, firstChosen);

        if (secondLabel != null)
            SetupChoiceButton(choiceButton2, choiceButton2Text, secondLabel, secondChosen);
        else if (choiceButton2 != null)
            choiceButton2.gameObject.SetActive(false);
    }

    // Waits inside a coroutine until the player picks; returns 0 (first) or 1 (second) through the callback.
    protected IEnumerator WaitForChoice(string firstLabel, string secondLabel, System.Action<int> onPicked)
    {
        int picked = -1;
        ShowChoice(firstLabel, () => picked = 0, secondLabel, secondLabel != null ? () => picked = 1 : (System.Action)null);
        while (picked < 0)
            yield return null;
        onPicked?.Invoke(picked);
    }

    private void SetupChoiceButton(Button button, TMP_Text buttonText, string label, System.Action onChosen)
    {
        if (button == null)
            return;

        button.gameObject.SetActive(true);
        button.interactable = true;
        if (buttonText != null) buttonText.text = label;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            if (!waitingForChoice)
                return;

            waitingForChoice = false;
            PlayEffect(clickAudioSource);
            if (choiceContainer != null) choiceContainer.SetActive(false);
            onChosen?.Invoke();
        });
    }

    // ------------------------------------------------------------------ speaker effects

    // The speaking character stays bright and jitters (or trembles); everyone else darkens.
    private void UpdateSpeakerEffects(string speakerName)
    {
        StopJitter();

        foreach (A3Character character in characters.Values)
        {
            if (character.image == null)
                continue;

            bool isSpeaking = speakerName == character.speakerName;
            Color targetColor = isSpeaking ? character.baseColor : Multiply(character.baseColor, darkenedTint);

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
                StartJitter(character.image,
                    character.trembles ? trembleStrength : jitterStrength,
                    character.trembles ? trembleDuration : jitterDuration,
                    !character.trembles);
            }
        }
    }

    // Puts every character in the given tint at once (e.g. everyone dims for a quiet moment).
    protected void TintAllCharacters(Color tint)
    {
        foreach (A3Character character in characters.Values)
        {
            if (character.image == null)
                continue;

            if (character.tintCoroutine != null)
                StopCoroutine(character.tintCoroutine);
            character.tintCoroutine = character.image.gameObject.activeInHierarchy
                ? StartCoroutine(TintImage(character.image, Multiply(character.baseColor, tint)))
                : null;
        }
    }

    private void BringToFront(Image characterImage)
    {
        Transform speakerTransform = characterImage.transform;
        int frontIndex = speakerTransform.GetSiblingIndex();

        foreach (A3Character other in characters.Values)
        {
            if (other.image != null && other.image.transform.parent == speakerTransform.parent)
                frontIndex = Mathf.Max(frontIndex, other.image.transform.GetSiblingIndex());
        }

        speakerTransform.SetSiblingIndex(frontIndex);
    }

    protected static Color Multiply(Color baseColor, Color tint)
    {
        return new Color(baseColor.r * tint.r, baseColor.g * tint.g, baseColor.b * tint.b, baseColor.a);
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
        if (characterImage == null || !characterImage.gameObject.activeInHierarchy)
            return;

        jitterTarget = characterImage.rectTransform;
        jitterRestPosition = jitterTarget.anchoredPosition;
        jitterCoroutine = StartCoroutine(Jitter(strength, duration, hop));
    }

    protected void StopJitter()
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

    // ------------------------------------------------------------------ screen effects

    protected IEnumerator ShakeScreen(float strength, float duration)
    {
        PlayEffect(FindSound("shake"));

        Transform boxTransform = dialogueBox != null ? dialogueBox.transform : null;
        Vector3 boxRestPosition = boxTransform != null ? boxTransform.localPosition : Vector3.zero;
        Transform backgroundTransform = backgroundRenderer != null ? backgroundRenderer.transform : null;

        float shakeDuration = Mathf.Max(duration, 0.01f);
        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float power = strength * (1f - Mathf.Clamp01(elapsed / shakeDuration));
            Vector3 offset = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * power;

            if (boxTransform != null) boxTransform.localPosition = boxRestPosition + offset;
            if (backgroundTransform != null) backgroundTransform.localPosition = backgroundRestPosition + offset * 0.5f;
            yield return null;
        }

        if (boxTransform != null) boxTransform.localPosition = boxRestPosition;
        if (backgroundTransform != null) backgroundTransform.localPosition = backgroundRestPosition;
    }

    // Tints the background SpriteRenderer (multiplied with its original color).
    protected IEnumerator TintBackground(Color tint, float duration)
    {
        if (backgroundRenderer == null)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        Color startColor = backgroundRenderer.color;
        Color targetColor = Multiply(backgroundBaseColor, tint);
        float fadeDuration = Mathf.Max(duration, 0.01f);
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            backgroundRenderer.color = Color.Lerp(startColor, targetColor, Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }

        backgroundRenderer.color = targetColor;
    }

    protected void SetBackgroundTint(Color tint)
    {
        if (backgroundRenderer != null)
            backgroundRenderer.color = Multiply(backgroundBaseColor, tint);
    }

    // ------------------------------------------------------------------ fades

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

    protected IEnumerator FadeFromBlack(float duration)
    {
        if (fadeScreenIn != null)
        {
            Image fadeImage = fadeScreenIn.GetComponent<Image>();
            if (fadeImage != null)
                yield return FadeImageAlpha(fadeImage, fadeImage.color.a, 0f, duration);

            fadeScreenIn.SetActive(false);
        }

        if (counterShouldShow && counterText != null)
            StartCoroutine(FadeTextAlpha(counterText, counterText.alpha, 1f, 0.5f));
    }

    // Covers the screen with the FadeOut image (black by default).
    protected IEnumerator FadeToColor(Color color, float duration)
    {
        if (counterText != null && counterText.alpha > 0f)
            StartCoroutine(FadeTextAlpha(counterText, counterText.alpha, 0f, Mathf.Min(duration, 0.5f)));

        Image fadeImage = PrepareFadeOutImage(color);
        if (fadeImage != null)
            yield return FadeImageAlpha(fadeImage, fadeImage.color.a, 1f, duration);
    }

    protected IEnumerator FadeToBlack(float duration)
    {
        yield return FadeToColor(Color.black, duration);
    }

    // Uncovers the screen after FadeToBlack/FadeToColor.
    protected IEnumerator FadeOutCover(float duration)
    {
        if (fadeOutScreen == null)
            yield break;

        Image fadeImage = fadeOutScreen.GetComponent<Image>();
        if (fadeImage != null)
            yield return FadeImageAlpha(fadeImage, fadeImage.color.a, 0f, duration);

        fadeOutScreen.SetActive(false);

        if (counterShouldShow && counterText != null)
            StartCoroutine(FadeTextAlpha(counterText, counterText.alpha, 1f, 0.5f));
    }

    // FadeOut may carry an Animator (copied from FadeIn) that would fight the script, so it is turned off.
    protected Image PrepareFadeOutImage(Color color)
    {
        if (fadeOutScreen == null)
            return null;

        bool wasActive = fadeOutScreen.activeSelf;
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
            float alpha = wasActive ? fadeImage.color.a : 0f;
            fadeImage.color = new Color(color.r, color.g, color.b, alpha);
        }

        return fadeImage;
    }

    protected static IEnumerator FadeImageAlpha(Image image, float fromAlpha, float toAlpha, float fadeDuration)
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

    protected static IEnumerator FadeTextAlpha(TMP_Text text, float fromAlpha, float toAlpha, float fadeDuration)
    {
        if (text == null)
            yield break;

        float duration = Mathf.Max(fadeDuration, 0.01f);
        float elapsed = 0f;
        while (elapsed < duration && text != null)
        {
            elapsed += Time.deltaTime;
            text.alpha = Mathf.Lerp(fromAlpha, toAlpha, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }

        if (text != null)
            text.alpha = toAlpha;
    }

    // ------------------------------------------------------------------ text on screen

    protected TMP_Text CreateText(string objectName, string message, float fontSize, Vector2 anchorMin, Vector2 anchorMax,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        if (canvasTransform == null)
            return null;

        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textTransform = textObject.GetComponent<RectTransform>();
        textTransform.SetParent(canvasTransform, false);
        textTransform.anchorMin = anchorMin;
        textTransform.anchorMax = anchorMax;
        textTransform.offsetMin = Vector2.zero;
        textTransform.offsetMax = Vector2.zero;
        textTransform.SetAsLastSibling();

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (messageInputText != null)
            text.font = messageInputText.font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.alpha = 0f;
        text.raycastTarget = false;
        text.text = message;
        return text;
    }

    // Text in the middle of the screen that fades in, stays, and fades out.
    protected IEnumerator ShowCenterText(string message, float fontSize, float holdDuration)
    {
        TMP_Text text = CreateText("CenterText", message, fontSize, new Vector2(0.1f, 0.35f), new Vector2(0.9f, 0.65f));
        if (text == null)
            yield break;

        yield return FadeTextAlpha(text, 0f, 1f, 0.8f);
        yield return new WaitForSeconds(holdDuration);
        yield return FadeTextAlpha(text, 1f, 0f, 0.8f);
        Destroy(text.gameObject);
    }

    // "ACT 3" and the act's title on the black screen.
    protected IEnumerator ShowTitleCard(string title, string subtitle)
    {
        TMP_Text titleText = CreateText("ActTitle", title, 72f, new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.7f));
        TMP_Text subtitleText = CreateText("ActSubtitle", subtitle, 40f, new Vector2(0.1f, 0.35f), new Vector2(0.9f, 0.5f));
        if (titleText == null)
            yield break;

        yield return FadeTextAlpha(titleText, 0f, 1f, 1f);
        yield return FadeTextAlpha(subtitleText, 0f, 1f, 1f);
        yield return new WaitForSeconds(2f);
        StartCoroutine(FadeTextAlpha(titleText, 1f, 0f, 1f));
        yield return FadeTextAlpha(subtitleText, 1f, 0f, 1f);
        Destroy(titleText.gameObject);
        Destroy(subtitleText.gameObject);
    }

    // ------------------------------------------------------------------ Good Deeds counter

    private void CreateCounter(bool visible)
    {
        counterText = CreateText("GoodDeedsCounter", CounterLabel(), counterFontSize,
            new Vector2(0.02f, 0.87f), new Vector2(0.6f, 0.98f), TextAlignmentOptions.TopLeft);
        if (counterText == null)
            return;

        counterText.rectTransform.pivot = new Vector2(0f, 1f);
        counterText.outlineWidth = 0.25f;
        counterText.outlineColor = Color.black;
        counterText.alpha = 0f;
        counterBaseScale = counterText.rectTransform.localScale;
        counterShouldShow = visible;
    }

    private static string CounterLabel()
    {
        return $"Good Deeds: {A3GoodDeeds.Count}";
    }

    protected IEnumerator ShowCounter(float duration)
    {
        counterShouldShow = true;
        if (counterText == null)
            yield break;

        counterText.text = CounterLabel();
        yield return FadeTextAlpha(counterText, counterText.alpha, 1f, duration);
    }

    // +1 good deed: the number goes up with a little green punch.
    protected void AddDeed(int amount = 1)
    {
        A3GoodDeeds.Add(amount);
        if (counterText == null)
            return;

        counterText.text = CounterLabel();
        if (counterBumpCoroutine != null)
            StopCoroutine(counterBumpCoroutine);
        counterBumpCoroutine = StartCoroutine(BumpCounter());
    }

    private IEnumerator BumpCounter()
    {
        Color normal = Color.white;
        Color flash = new Color(0.55f, 1f, 0.55f);
        Transform counterTransform = counterText.transform;

        float elapsed = 0f;
        const float duration = 0.35f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float bump = Mathf.Sin(progress * Mathf.PI);
            counterTransform.localScale = counterBaseScale * (1f + 0.3f * bump);
            Color color = Color.Lerp(flash, normal, progress);
            color.a = counterText.alpha;
            counterText.color = color;
            yield return null;
        }

        counterTransform.localScale = counterBaseScale;
        counterBumpCoroutine = null;
    }

    // The number flickers once, like it doesn't mean anything.
    protected IEnumerator FlickerCounter()
    {
        if (counterText == null)
            yield break;

        float startAlpha = counterText.alpha;
        for (int i = 0; i < 3; i++)
        {
            counterText.alpha = 0.2f;
            yield return new WaitForSeconds(0.07f);
            counterText.alpha = startAlpha;
            yield return new WaitForSeconds(0.1f);
        }
    }

    // The counter glitches with random numbers, turns red, then breaks apart and is gone.
    protected IEnumerator BreakCounter()
    {
        counterShouldShow = false;
        if (counterText == null)
            yield break;

        Transform counterTransform = counterText.transform;
        Vector3 restPosition = counterTransform.localPosition;
        counterText.alpha = 1f;

        float elapsed = 0f;
        const float glitchDuration = 1.2f;
        while (elapsed < glitchDuration)
        {
            counterText.text = $"Good Deeds: {Random.Range(0, 999)}";
            counterText.color = Color.Lerp(Color.white, new Color(1f, 0.35f, 0.35f), elapsed / glitchDuration);
            counterTransform.localPosition = restPosition + (Vector3)(Random.insideUnitCircle * 6f);
            yield return new WaitForSeconds(0.04f);
            elapsed += 0.04f;
        }

        counterText.text = "Good Deeds: ---";
        counterTransform.localPosition = restPosition;

        elapsed = 0f;
        const float breakDuration = 0.8f;
        while (elapsed < breakDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / breakDuration);
            counterTransform.localScale = counterBaseScale * (1f + progress * 0.6f);
            counterText.alpha = 1f - progress;
            counterText.characterSpacing = progress * 40f;
            yield return null;
        }

        Destroy(counterText.gameObject);
        counterText = null;
    }

    // ------------------------------------------------------------------ scene changes

    protected IEnumerator FadeOutAndLoad(string sceneName, string fallbackSceneName, float fadeDuration, AudioSource soundAfterFade = null)
    {
        isEnding = true;
        StopJitter();
        if (nextButton != null) nextButton.SetActive(false);
        HideDialogueBox();
        StopAllSounds(fadeDuration);

        yield return FadeToBlack(fadeDuration);

        if (soundAfterFade != null && soundAfterFade.clip != null)
        {
            PlayEffect(soundAfterFade);
            yield return new WaitForSeconds(EffectLength(soundAfterFade, 0.5f, 2.5f));
        }
        else
        {
            yield return new WaitForSeconds(0.4f);
        }

        SceneManager.LoadScene(string.IsNullOrEmpty(sceneName) ? fallbackSceneName : sceneName);
    }
}
