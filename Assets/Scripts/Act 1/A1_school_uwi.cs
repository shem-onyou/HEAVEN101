using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public enum A1UwianAction
{
    None,
    Classmate1Enters,
    Classmate2Enters,
    Playful,
    PulubiApproaches,
    PulubiLeaves,
    GiveMoney,
    LeaveTogether
}

[System.Serializable]
public class A1UwianDialogueLine
{
    public A1UwianAction actionBefore;
    public string speakerName;
    [TextArea(2, 5)] public string message;

    public A1UwianDialogueLine(string name, string msg, A1UwianAction action = A1UwianAction.None)
    {
        speakerName = name;
        message = msg;
        actionBefore = action;
    }
}

public class A1_school_uwi : MonoBehaviour
{
    // Saved in PlayerDatabase so later scenes can check: did the player give money to the pulubi?
    public const string PulubiChoiceId = "act1_pulubi";
    public const string GaveMoneyAnswer = "give";
    public const string RefusedAnswer = "refuse";

    public static bool GaveMoneyToPulubi => PlayerDatabase.GetChoice(PulubiChoiceId) == GaveMoneyAnswer;

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
    [SerializeField] private string giveChoiceText = "Give some money";
    [SerializeField] private string refuseChoiceText = "Sorry, I can't";

    [Header("Characters (auto-found by name if empty)")]
    public Image classmate1Image;
    public Image classmate2Image;
    public Image pulubiImage;
    [SerializeField] private string classmate1Name = "Elijah";
    [SerializeField] private string classmate2Name = "Classmate";
    [SerializeField] private string pulubiName = "Pulubi";
    [SerializeField] private float walkInDuration = 1f;
    [SerializeField] private float pulubiWalkDuration = 1.5f;
    [SerializeField] private bool spreadClassmates = true;
    [Tooltip("Gap between the two classmates, as a fraction of the canvas width.")]
    [Range(0f, 1f)] [SerializeField] private float classmateSpacing = 0.3f;

    [Header("Speaking Effects")]
    [SerializeField] private Color darkenedTint = new Color(0.45f, 0.45f, 0.45f, 1f);
    [SerializeField] private float tintDuration = 0.15f;
    [SerializeField] private float jitterDuration = 0.3f;
    [SerializeField] private float jitterStrength = 6f;
    [Tooltip("The pulubi trembles weakly instead of hopping.")]
    [SerializeField] private float pulubiTrembleStrength = 2f;
    [SerializeField] private float pulubiTrembleDuration = 0.8f;

    [Header("Sounds (auto-found by object or clip name if empty)")]
    [Tooltip("Afternoon street/school sounds, looped. Found by \"ambient\". Stays silent until it has a clip.")]
    [SerializeField] private AudioSource ambientAudioSource;
    [Tooltip("Optional: an AudioSource with the next_click clip.")]
    [SerializeField] private AudioSource clickAudioSource;
    [Tooltip("How loud the ambient stays while the pulubi is talking, so the moment feels quieter.")]
    [Range(0f, 1f)] [SerializeField] private float pulubiMomentVolume = 0.4f;

    [Header("Text UI References (auto-found by name if empty)")]
    public TMP_Text charNameText;
    public TMP_Text messageInputText;

    [Header("Typewriter Speed Settings")]
    [SerializeField] private float typingSpeed = 0.03f;

    // Renamed from nextSceneName: the scene still had "A1_LivingRoom" (the morning room) saved there,
    // so a new field drops that old value and points to the after-school living room instead.
    [Header("Scene Transition")]
    [SerializeField] private string homeSceneName = "A1_LR_umuwi";

    [Header("Dialogue Sequence")]
    public List<A1UwianDialogueLine> introLines = new List<A1UwianDialogueLine>();
    public List<A1UwianDialogueLine> giveLines = new List<A1UwianDialogueLine>();
    public List<A1UwianDialogueLine> refuseLines = new List<A1UwianDialogueLine>();

    private class UwianCharacter
    {
        public Image image;
        public string speakerName;
        public Vector2 spotPosition;
        public Color baseColor = Color.white;
        public Coroutine tintCoroutine;
    }

    private UwianCharacter classmate1;
    private UwianCharacter classmate2;
    private UwianCharacter pulubi;
    private UwianCharacter[] characters;

    private List<A1UwianDialogueLine> currentLines;
    private int currentLineIndex = 0;
    private Coroutine typingCoroutine;
    private bool isTyping;
    private bool isPlayingAction;
    private bool waitingForChoice;
    private bool choiceMade;
    private bool dialogueFinished;
    private Coroutine jitterCoroutine;
    private RectTransform jitterTarget;
    private Vector2 jitterRestPosition;
    private float ambientVolume = 1f;

    void Start()
    {
        if (introLines.Count == 0)
        {
            introLines.Add(new A1UwianDialogueLine(classmate1Name, "Fishball time! Tara, tara!", A1UwianAction.Classmate1Enters));
            introLines.Add(new A1UwianDialogueLine(classmate2Name, "Hoy, don't forget the kwek-kwek you promised!", A1UwianAction.Classmate2Enters));
            introLines.Add(new A1UwianDialogueLine("You", "And mine too, right?"));
            introLines.Add(new A1UwianDialogueLine(classmate1Name, "Ha? I never said that! HAHAHA!"));
            introLines.Add(new A1UwianDialogueLine(classmate2Name, "Hay nako, ang kuripot talaga!", A1UwianAction.Playful));
            introLines.Add(new A1UwianDialogueLine(classmate1Name, "Aray! Okay fine, sige na!"));
            introLines.Add(new A1UwianDialogueLine(pulubiName, "Anak... excuse me po...", A1UwianAction.PulubiApproaches));
            introLines.Add(new A1UwianDialogueLine(pulubiName, "Can you spare some coins? I haven't eaten since this morning..."));
            introLines.Add(new A1UwianDialogueLine("You", "(This is the same person from this morning...)"));
        }

        if (giveLines.Count == 0)
        {
            giveLines.Add(new A1UwianDialogueLine("You", "Here po, buy yourself some food."));
            giveLines.Add(new A1UwianDialogueLine(pulubiName, "Salamat, anak... God bless you."));
            giveLines.Add(new A1UwianDialogueLine("You", "(Wait... that was my fare money...)", A1UwianAction.PulubiLeaves));
            giveLines.Add(new A1UwianDialogueLine(classmate2Name, "Hoy. I saw that. You gave away your pamasahe, didn't you?"));
            giveLines.Add(new A1UwianDialogueLine("You", "Hehe... it's fine, I can walk."));
            giveLines.Add(new A1UwianDialogueLine(classmate2Name, "Hay nako. Here, dagdag pamasahe.", A1UwianAction.GiveMoney));
            giveLines.Add(new A1UwianDialogueLine("You", "Huh? Are you sure?"));
            giveLines.Add(new A1UwianDialogueLine(classmate2Name, "Of course! What you did was really kind."));
            giveLines.Add(new A1UwianDialogueLine(classmate1Name, "Wow, ang bait niyo. Fishball lang kaya ko, eh."));
            giveLines.Add(new A1UwianDialogueLine("You", "Hoy, wait for me!", A1UwianAction.LeaveTogether));
        }

        if (refuseLines.Count == 0)
        {
            refuseLines.Add(new A1UwianDialogueLine("You", "Sorry po... I don't have extra money."));
            refuseLines.Add(new A1UwianDialogueLine(pulubiName, "...It's okay, anak. I understand."));
            refuseLines.Add(new A1UwianDialogueLine(classmate2Name, "Wait lang po!", A1UwianAction.GiveMoney));
            refuseLines.Add(new A1UwianDialogueLine(classmate2Name, "Here po. It's not much, but please buy some food."));
            refuseLines.Add(new A1UwianDialogueLine(pulubiName, "Salamat, anak... God bless you."));
            refuseLines.Add(new A1UwianDialogueLine("You", "(...)", A1UwianAction.PulubiLeaves));
            refuseLines.Add(new A1UwianDialogueLine(classmate1Name, "Tara, let's go home na."));
            refuseLines.Add(new A1UwianDialogueLine("You", "Hoy, wait for me!", A1UwianAction.LeaveTogether));
        }

        FindSceneReferences();

        classmate1 = CreateCharacter(classmate1Image, classmate1Name);
        classmate2 = CreateCharacter(classmate2Image, classmate2Name);
        pulubi = CreateCharacter(pulubiImage, pulubiName);
        characters = new[] { classmate1, classmate2, pulubi };

        if (spreadClassmates)
            SpreadClassmates();

        KeepOnScreen(classmate1, classmate2);
        KeepOnScreen(pulubi);

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

    private UwianCharacter CreateCharacter(Image characterImage, string speakerName)
    {
        UwianCharacter character = new UwianCharacter { image = characterImage, speakerName = speakerName };
        if (characterImage != null)
        {
            character.spotPosition = characterImage.rectTransform.anchoredPosition;
            character.baseColor = characterImage.color;
            characterImage.gameObject.SetActive(false);
        }

        return character;
    }

    // Places the two classmates side by side around their shared middle point,
    // so they don't stand on top of each other.
    private void SpreadClassmates()
    {
        if (classmate1.image == null || classmate2.image == null)
            return;

        RectTransform parentTransform = classmate1.image.rectTransform.parent as RectTransform;
        if (parentTransform == null)
            return;

        Vector2 middle = (classmate1.spotPosition + classmate2.spotPosition) * 0.5f;
        float halfGap = parentTransform.rect.width * classmateSpacing * 0.5f;
        classmate1.spotPosition = middle - new Vector2(halfGap, 0f);
        classmate2.spotPosition = middle + new Vector2(halfGap, 0f);
    }

    // Slides the given characters together (keeping their gap) until none of them sticks out
    // past the left or right edge of the screen.
    private static void KeepOnScreen(params UwianCharacter[] group)
    {
        float overRight = 0f;
        float overLeft = 0f;

        foreach (UwianCharacter character in group)
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

        foreach (UwianCharacter character in group)
            character.spotPosition += new Vector2(shift, 0f);
    }

    // Characters on the left half of the screen come and go through the left edge, and vice versa.
    private static float SideOf(UwianCharacter character)
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
        if (pulubiImage == null) pulubiImage = FindImage(canvasTransform, "pulubi");

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
        if (isPlayingAction || waitingForChoice || dialogueFinished)
            return;

        PlayClick();

        if (isTyping)
        {
            CompleteTypingInstantly();
            return;
        }

        if (currentLineIndex + 1 >= currentLines.Count)
        {
            if (!choiceMade)
            {
                ShowPulubiChoice();
                return;
            }

            dialogueFinished = true;
            StartCoroutine(FadeOutScene());
            return;
        }

        currentLineIndex++;
        StartCoroutine(PlayLine(currentLineIndex));
    }

    private void ShowPulubiChoice()
    {
        waitingForChoice = true;
        if (nextButton != null) nextButton.SetActive(false);
        if (choiceContainer != null) choiceContainer.SetActive(true);

        if (choiceButton1 != null)
        {
            choiceButton1.gameObject.SetActive(true);
            if (choiceButton1Text != null) choiceButton1Text.text = giveChoiceText;
            choiceButton1.onClick.RemoveAllListeners();
            choiceButton1.onClick.AddListener(() =>
            {
                PlayClick();
                MakeChoice(true);
            });
        }

        if (choiceButton2 != null)
        {
            choiceButton2.gameObject.SetActive(true);
            if (choiceButton2Text != null) choiceButton2Text.text = refuseChoiceText;
            choiceButton2.onClick.RemoveAllListeners();
            choiceButton2.onClick.AddListener(() =>
            {
                PlayClick();
                MakeChoice(false);
            });
        }
    }

    private void MakeChoice(bool giveMoney)
    {
        if (!waitingForChoice)
            return;

        waitingForChoice = false;
        choiceMade = true;
        PlayerDatabase.RecordChoice(PulubiChoiceId,
            "A pulubi asked for money to buy food.",
            giveMoney ? giveChoiceText : refuseChoiceText,
            giveMoney ? 2 : 0,
            giveMoney ? GaveMoneyAnswer : RefusedAnswer);

        if (choiceContainer != null) choiceContainer.SetActive(false);

        currentLines = giveMoney ? giveLines : refuseLines;
        currentLineIndex = 0;

        if (currentLines.Count > 0)
            StartCoroutine(PlayLine(currentLineIndex));
        else
        {
            dialogueFinished = true;
            StartCoroutine(FadeOutScene());
        }
    }

    private IEnumerator PlayLine(int index)
    {
        A1UwianDialogueLine line = currentLines[index];

        if (line.actionBefore != A1UwianAction.None)
        {
            isPlayingAction = true;
            if (nextButton != null) nextButton.SetActive(false);
            StopJitter();
            yield return PerformAction(line.actionBefore);
            isPlayingAction = false;
        }

        DisplayLine(line);
    }

    private IEnumerator PerformAction(A1UwianAction action)
    {
        switch (action)
        {
            case A1UwianAction.Classmate1Enters:
                yield return WalkIn(classmate1, SideOf(classmate1), walkInDuration);
                break;

            case A1UwianAction.Classmate2Enters:
                yield return WalkIn(classmate2, SideOf(classmate2), walkInDuration);
                break;

            case A1UwianAction.Playful:
                yield return PlayfulHit();
                break;

            case A1UwianAction.PulubiApproaches:
                // The street noise drops so the pulubi's quiet voice stands out.
                StartCoroutine(FadeAmbient(ambientVolume * pulubiMomentVolume, 1f));
                yield return WalkIn(pulubi, SideOf(pulubi), pulubiWalkDuration);
                break;

            case A1UwianAction.PulubiLeaves:
                yield return RunOffScreen(pulubi, pulubiWalkDuration, SideOf(pulubi));
                StartCoroutine(FadeAmbient(ambientVolume, 1.5f));
                break;

            case A1UwianAction.GiveMoney:
                yield return Classmate2GivesMoney();
                break;

            case A1UwianAction.LeaveTogether:
                yield return LeaveTogether();
                break;
        }
    }

    // side: -1 enters from the left edge, 1 enters from the right edge
    private IEnumerator WalkIn(UwianCharacter character, float side, float duration)
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

    // side: -1 leaves through the left edge, 1 through the right edge (home is to the right).
    private IEnumerator RunOffScreen(UwianCharacter character, float duration, float side = 1f)
    {
        if (character.image == null || !character.image.gameObject.activeSelf)
            yield break;

        RectTransform imageTransform = character.image.rectTransform;
        RectTransform parentTransform = imageTransform.parent as RectTransform;
        if (parentTransform == null)
            yield break;

        Vector2 startPosition = imageTransform.anchoredPosition;
        Vector2 exitPosition = startPosition + new Vector2(side * parentTransform.rect.width, 0f);

        yield return MoveImage(imageTransform, startPosition, exitPosition, duration);
        character.image.gameObject.SetActive(false);
    }

    // Classmate2 lunges at classmate1 for a playful hit, classmate1 gets knocked back a little.
    private IEnumerator PlayfulHit()
    {
        if (!IsOnScreen(classmate1) || !IsOnScreen(classmate2))
            yield break;

        RectTransform hitterTransform = classmate2.image.rectTransform;
        RectTransform targetTransform = classmate1.image.rectTransform;

        float towardTarget = classmate1.spotPosition.x - classmate2.spotPosition.x;
        Vector2 lungePosition = classmate2.spotPosition + new Vector2(towardTarget * 0.4f, 0f);
        Vector2 knockedPosition = classmate1.spotPosition + new Vector2(Mathf.Sign(towardTarget) * 30f, 0f);

        yield return MoveImage(hitterTransform, classmate2.spotPosition, lungePosition, 0.12f);

        Coroutine hitterBack = StartCoroutine(MoveImage(hitterTransform, lungePosition, classmate2.spotPosition, 0.25f));
        yield return MoveImage(targetTransform, classmate1.spotPosition, knockedPosition, 0.08f);
        yield return MoveImage(targetTransform, knockedPosition, classmate1.spotPosition, 0.2f);
        yield return hitterBack;
    }

    // If the pulubi is still here, classmate2 steps toward them to hand over coins.
    // Otherwise classmate2 steps toward the player (grows a bit, like coming closer to the camera).
    private IEnumerator Classmate2GivesMoney()
    {
        if (!IsOnScreen(classmate2))
            yield break;

        RectTransform giverTransform = classmate2.image.rectTransform;

        if (IsOnScreen(pulubi))
        {
            float towardPulubi = pulubi.spotPosition.x - classmate2.spotPosition.x;
            Vector2 handOverPosition = classmate2.spotPosition + new Vector2(towardPulubi * 0.4f, 0f);
            yield return MoveImage(giverTransform, classmate2.spotPosition, handOverPosition, 0.3f);
            yield return new WaitForSeconds(0.4f);
            yield return MoveImage(giverTransform, handOverPosition, classmate2.spotPosition, 0.3f);
            yield break;
        }

        Vector3 baseScale = giverTransform.localScale;
        Vector3 closerScale = baseScale * 1.12f;
        yield return ScaleImage(giverTransform, baseScale, closerScale, 0.2f);
        yield return new WaitForSeconds(0.4f);
        yield return ScaleImage(giverTransform, closerScale, baseScale, 0.2f);
    }

    // Classmate2 runs off first, classmate1 chases after.
    private IEnumerator LeaveTogether()
    {
        if (dialogueBox != null) dialogueBox.SetActive(false);

        Coroutine classmate2Run = StartCoroutine(RunOffScreen(classmate2, walkInDuration));
        yield return new WaitForSeconds(0.3f);
        Coroutine classmate1Run = StartCoroutine(RunOffScreen(classmate1, walkInDuration));

        yield return classmate2Run;
        yield return classmate1Run;
        yield return new WaitForSeconds(0.3f);
    }

    private static bool IsOnScreen(UwianCharacter character)
    {
        return character.image != null && character.image.gameObject.activeSelf;
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

    private IEnumerator ScaleImage(RectTransform imageTransform, Vector3 startScale, Vector3 targetScale, float scaleDuration)
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

    private void DisplayLine(A1UwianDialogueLine line)
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

    // The speaking character stays bright and jitters; everyone else darkens.
    // When You is speaking, all characters on screen darken.
    private void UpdateSpeakerEffects(string speakerName)
    {
        StopJitter();

        foreach (UwianCharacter character in characters)
        {
            if (character.image == null)
                continue;

            bool isSpeaking = speakerName == character.speakerName;
            Color targetColor = isSpeaking ? character.baseColor : Darken(character.baseColor);

            if (character.tintCoroutine != null)
                StopCoroutine(character.tintCoroutine);

            if (character.image.gameObject.activeInHierarchy)
                character.tintCoroutine = StartCoroutine(TintImage(character.image, targetColor));
            else
            {
                character.image.color = targetColor;
                character.tintCoroutine = null;
            }

            if (isSpeaking)
            {
                BringToFront(character.image);
                bool trembling = character == pulubi;
                StartJitter(character.image,
                    trembling ? pulubiTrembleStrength : jitterStrength,
                    trembling ? pulubiTrembleDuration : jitterDuration,
                    !trembling);
            }
        }
    }

    // Moves the speaker in front of the other characters while keeping all of them
    // behind the dialogue box (the speaker takes the highest index among the characters).
    private void BringToFront(Image characterImage)
    {
        Transform speakerTransform = characterImage.transform;
        int frontIndex = speakerTransform.GetSiblingIndex();

        foreach (UwianCharacter other in characters)
        {
            if (other.image != null && other.image.transform.parent == speakerTransform.parent)
                frontIndex = Mathf.Max(frontIndex, other.image.transform.GetSiblingIndex());
        }

        speakerTransform.SetSiblingIndex(frontIndex);
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

    private void StartJitter(Image characterImage, float strength, float duration, bool hop)
    {
        if (characterImage == null || !characterImage.gameObject.activeInHierarchy)
            return;

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

    private IEnumerator FadeOutScene()
    {
        StopJitter();
        if (nextButton != null) nextButton.SetActive(false);
        if (dialogueBox != null) dialogueBox.SetActive(false);
        StartCoroutine(FadeAmbient(0f, 1f));

        if (fadeOutScreen != null)
            yield return FadeBlackScreen(0f, 1f);

        LoadNextScene();
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

    private void LoadNextScene()
    {
        if (string.IsNullOrEmpty(homeSceneName))
            homeSceneName = "A1_LR_umuwi";

        SceneManager.LoadScene(homeSceneName);
    }
}
