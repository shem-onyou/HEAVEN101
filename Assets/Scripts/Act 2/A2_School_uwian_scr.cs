using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public enum A2UwianAction
{
    None,
    Classmate1Enters,
    Classmate2Enters,
    PulubiApproaches,
    PulubiLeaves,
    PulubiLeavesSlowly,
    FriendsLeave,
    PulledAway,
    LeaveTogether,
    Pause
}

[System.Serializable]
public class A2UwianDialogueLine
{
    public A2UwianAction actionBefore;
    public string speakerName;
    [TextArea(2, 5)] public string message;

    public A2UwianDialogueLine(string name, string msg, A2UwianAction action = A2UwianAction.None)
    {
        speakerName = name;
        message = msg;
        actionBefore = action;
    }
}

// Act 2 after school: the pulubi from Act 1 comes back, this time asking for his sick child's medicine.
// Unlike Act 1, nobody can help: the player only has their pamasahe left, and the choice has a timer.
public class A2_School_uwian_scr : MonoBehaviour
{
    // Saved in PlayerDatabase so the living room and the bedroom can react to it.
    public const string PulubiChoiceId = "act2_pulubi_medicine";
    public const string GaveFareAnswer = "gave_fare";
    public const string KeptFareAnswer = "kept_fare";
    public const string HesitatedAnswer = "hesitated";

    public static bool WalkedHomeAlone => PlayerDatabase.GetChoice(PulubiChoiceId) == GaveFareAnswer;

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

    [Header("Choice: the pulubi's medicine")]
    [SerializeField] private string giveChoiceText = "Give him my pamasahe";
    [SerializeField] private string keepChoiceText = "Sorry po, I can't";
    [SerializeField] private int givePoints = 1;
    [SerializeField] private int keepPoints = 0;
    [Tooltip("Seconds before the friends pull you away. 0 = no timer.")]
    [SerializeField] private float choiceTimeLimit = 10f;

    [Header("Characters (auto-found by name if empty)")]
    public Image classmate1Image;
    public Image classmate2Image;
    public Image pulubiImage;
    [SerializeField] private string playerName = "You";
    [SerializeField] private string classmate1Name = "Elijah";
    [SerializeField] private string classmate2Name = "Perzifanie";
    [SerializeField] private string pulubiName = "Pulubi";
    [SerializeField] private float walkInDuration = 1f;
    [SerializeField] private float pulubiWalkDuration = 1.5f;
    [SerializeField] private float pulubiSlowWalkDuration = 3f;
    [Tooltip("Elijah and Perzifanie stand together on the right side, past the pulubi's spot, so they never cover him " +
             "(and Perzifanie faces the same way as Elijah). Off = the old spread around the middle.")]
    [SerializeField] private bool classmatesRightOfPulubi = true;
    [SerializeField] private bool spreadClassmates = true;
    [Tooltip("Gap between the two classmates, as a fraction of the canvas width.")]
    [Range(0f, 1f)] [SerializeField] private float classmateSpacing = 0.3f;

    [Header("Speaking Effects")]
    [SerializeField] private Color darkenedTint = new Color(0.45f, 0.45f, 0.45f, 1f);
    [SerializeField] private float tintDuration = 0.15f;
    [SerializeField] private float jitterDuration = 0.3f;
    [SerializeField] private float jitterStrength = 6f;
    [SerializeField] private float pulubiTrembleStrength = 2f;
    [SerializeField] private float pulubiTrembleDuration = 0.8f;

    [Header("Sounds (auto-found by object or clip name if empty)")]
    [Tooltip("Afternoon street sounds, looped. Found by \"ambient\".")]
    [SerializeField] private AudioSource ambientAudioSource;
    [SerializeField] private AudioSource clickAudioSource;
    [Tooltip("Optional: plays while the timer runs. Found by \"beat\" (e.g. hearbeat).")]
    [SerializeField] private AudioSource heartbeatAudioSource;
    [Range(0f, 1f)] [SerializeField] private float pulubiMomentVolume = 0.35f;
    [Range(0f, 1f)] [SerializeField] private float aloneVolume = 0.15f;

    [Header("Text UI References (auto-found by name if empty)")]
    public TMP_Text charNameText;
    public TMP_Text messageInputText;

    [Header("Typewriter Speed Settings")]
    [SerializeField] private float typingSpeed = 0.03f;
    [Tooltip("Slower typing for the heavy lines (the player's \"(...)\" thoughts).")]
    [SerializeField] private float slowTypingSpeed = 0.06f;

    [Header("Scene Transition")]
    [SerializeField] private string homeSceneName = "A2_Lroom_uwi";

    [Header("Dialogue Sequence")]
    public List<A2UwianDialogueLine> introLines = new List<A2UwianDialogueLine>();
    public List<A2UwianDialogueLine> letCopyMoodLines = new List<A2UwianDialogueLine>();
    public List<A2UwianDialogueLine> refusedCopyMoodLines = new List<A2UwianDialogueLine>();
    public List<A2UwianDialogueLine> hungryLines = new List<A2UwianDialogueLine>();
    public List<A2UwianDialogueLine> fullLines = new List<A2UwianDialogueLine>();
    public List<A2UwianDialogueLine> pulubiLines = new List<A2UwianDialogueLine>();
    public List<A2UwianDialogueLine> giveLines = new List<A2UwianDialogueLine>();
    public List<A2UwianDialogueLine> keepLines = new List<A2UwianDialogueLine>();
    public List<A2UwianDialogueLine> timeoutLines = new List<A2UwianDialogueLine>();

    private class UwianCharacter
    {
        public Image image;
        public string speakerName;
        public Vector2 spotPosition;
        public Vector3 baseScale = Vector3.one;
        public Color baseColor = Color.white;
        public Coroutine tintCoroutine;
    }

    private UwianCharacter classmate1;
    private UwianCharacter classmate2;
    private UwianCharacter pulubi;
    private UwianCharacter[] characters;

    private List<A2UwianDialogueLine> currentLines;
    private int currentLineIndex;
    private System.Action onLinesFinished;
    private Coroutine typingCoroutine;
    private bool isTyping;
    private bool isPlayingAction;
    private bool waitingForChoice;
    private bool isEnding;
    private Coroutine jitterCoroutine;
    private RectTransform jitterTarget;
    private Vector2 jitterRestPosition;
    private float ambientVolume = 1f;
    private Coroutine countdownCoroutine;
    private TMP_Text countdownText;

    void Start()
    {
        FillDefaultLines();
        FindSceneReferences();

        classmate1 = CreateCharacter(classmate1Image, classmate1Name);
        classmate2 = CreateCharacter(classmate2Image, classmate2Name);
        pulubi = CreateCharacter(pulubiImage, pulubiName);
        characters = new[] { classmate1, classmate2, pulubi };

        bool placedOnRight = classmatesRightOfPulubi && PlaceClassmatesRightOfPulubi();
        if (!placedOnRight && spreadClassmates)
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

        StartCoroutine(StartCutscene());
    }

    private void FillDefaultLines()
    {
        if (introLines.Count == 0)
        {
            introLines.Add(new A2UwianDialogueLine(classmate2Name, "Finally, uwian na! My brain is so tired.", A2UwianAction.Classmate2Enters));
            introLines.Add(new A2UwianDialogueLine(classmate1Name, "Same... that quiz drained my soul.", A2UwianAction.Classmate1Enters));
        }

        // Elijah remembers what happened during the quiz.
        if (letCopyMoodLines.Count == 0)
        {
            letCopyMoodLines.Add(new A2UwianDialogueLine(classmate1Name, "Hoy, thanks again for earlier ha. You saved me, hehe."));
            letCopyMoodLines.Add(new A2UwianDialogueLine(playerName, "Hehe... no problem."));
            letCopyMoodLines.Add(new A2UwianDialogueLine(playerName, "(...Did I really save him, though?)"));
        }

        if (refusedCopyMoodLines.Count == 0)
        {
            refusedCopyMoodLines.Add(new A2UwianDialogueLine(classmate1Name, "..."));
            refusedCopyMoodLines.Add(new A2UwianDialogueLine(classmate2Name, "Uy Elijah, are you still sulking about the quiz?"));
            refusedCopyMoodLines.Add(new A2UwianDialogueLine(classmate1Name, "I'm not sulking. I'm just... tired."));
            refusedCopyMoodLines.Add(new A2UwianDialogueLine(playerName, "(He hasn't looked at me since the quiz.)"));
        }

        // What happened with the lunch money.
        if (hungryLines.Count == 0)
        {
            hungryLines.Add(new A2UwianDialogueLine(classmate2Name, "You okay? You didn't eat anything at lunch."));
            hungryLines.Add(new A2UwianDialogueLine(playerName, "I'm fine, hehe. I'll just eat at home."));
            hungryLines.Add(new A2UwianDialogueLine(classmate1Name, "Don't worry, I'll pay you back tomorrow! Promise!"));
            hungryLines.Add(new A2UwianDialogueLine(playerName, "(My stomach hurts... At least I still have my pamasahe.)"));
        }

        if (fullLines.Count == 0)
        {
            fullLines.Add(new A2UwianDialogueLine(classmate1Name, "Haaay, I'm still hungry. Lunch was so small..."));
            fullLines.Add(new A2UwianDialogueLine(classmate2Name, "Hoy, you ate half of my baon!"));
            fullLines.Add(new A2UwianDialogueLine(classmate1Name, "Hehe... sorry na."));
            fullLines.Add(new A2UwianDialogueLine(playerName, "(All I have left now is my pamasahe.)"));
        }

        if (pulubiLines.Count == 0)
        {
            pulubiLines.Add(new A2UwianDialogueLine(pulubiName, "Anak... anak, wait lang po...", A2UwianAction.PulubiApproaches));
            pulubiLines.Add(new A2UwianDialogueLine(playerName, "(It's him again... the man from before.)"));
            pulubiLines.Add(new A2UwianDialogueLine(pulubiName, "Pasensya na po sa abala... my child has a high fever since last night."));
            pulubiLines.Add(new A2UwianDialogueLine(pulubiName, "I just need a little more... para sa gamot niya. Kahit magkano lang po..."));
            pulubiLines.Add(new A2UwianDialogueLine(classmate1Name, "(Psst... baka modus lang 'yan. Tara na.)"));
            pulubiLines.Add(new A2UwianDialogueLine(classmate2Name, "I'm sorry po... I already spent all my money on our project."));
            pulubiLines.Add(new A2UwianDialogueLine(playerName, "(All I have is my pamasahe... If I give it, I'll have to walk home. Alone.)"));
        }

        if (giveLines.Count == 0)
        {
            giveLines.Add(new A2UwianDialogueLine(playerName, "Here po... it's all I have. I hope it helps."));
            giveLines.Add(new A2UwianDialogueLine(pulubiName, "Anak... salamat. Salamat talaga... God bless you."));
            giveLines.Add(new A2UwianDialogueLine(classmate1Name, "Hala... that was your pamasahe!", A2UwianAction.PulubiLeaves));
            giveLines.Add(new A2UwianDialogueLine(classmate2Name, "I'm sorry... I really don't have anything left to lend you."));
            giveLines.Add(new A2UwianDialogueLine(playerName, "It's okay. I'll just walk."));
            giveLines.Add(new A2UwianDialogueLine(classmate1Name, "Our jeep is here... Sige, ingat ka ha. Text us when you get home."));
            giveLines.Add(new A2UwianDialogueLine(playerName, "...", A2UwianAction.FriendsLeave));
            giveLines.Add(new A2UwianDialogueLine(playerName, "(It's getting dark. Better start walking.)", A2UwianAction.Pause));
        }

        if (keepLines.Count == 0)
        {
            keepLines.Add(new A2UwianDialogueLine(playerName, "Sorry po... I only have enough for my fare."));
            keepLines.Add(new A2UwianDialogueLine(pulubiName, "...Okay lang, anak. Pasensya na sa abala."));
            keepLines.Add(new A2UwianDialogueLine(playerName, "...", A2UwianAction.PulubiLeavesSlowly));
            keepLines.Add(new A2UwianDialogueLine(classmate1Name, "...Tara na."));
            keepLines.Add(new A2UwianDialogueLine(playerName, "(I wonder if his child will be okay...)"));
            keepLines.Add(new A2UwianDialogueLine(playerName, "Wait for me...", A2UwianAction.LeaveTogether));
        }

        // The player didn't answer in time.
        if (timeoutLines.Count == 0)
        {
            timeoutLines.Add(new A2UwianDialogueLine(classmate1Name, "Hoy, tara na! The jeep is leaving!", A2UwianAction.PulledAway));
            timeoutLines.Add(new A2UwianDialogueLine(playerName, "W-wait..."));
            timeoutLines.Add(new A2UwianDialogueLine(pulubiName, "...", A2UwianAction.PulubiLeavesSlowly));
            timeoutLines.Add(new A2UwianDialogueLine(playerName, "(I didn't even say anything to him.)"));
            timeoutLines.Add(new A2UwianDialogueLine(classmate2Name, "Come on, let's go.", A2UwianAction.LeaveTogether));
        }
    }

    private UwianCharacter CreateCharacter(Image characterImage, string speakerName)
    {
        UwianCharacter character = new UwianCharacter { image = characterImage, speakerName = speakerName };
        if (characterImage != null)
        {
            character.spotPosition = characterImage.rectTransform.anchoredPosition;
            character.baseScale = characterImage.rectTransform.localScale;
            character.baseColor = characterImage.color;
            characterImage.gameObject.SetActive(false);
        }

        return character;
    }

    // Places the two classmates side by side around their shared middle point.
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

    // Elijah and Perzifanie stand together on the right: Elijah just past the pulubi's spot, Perzifanie near the
    // right edge, overlapping a little like friends standing side by side. The pulubi keeps his spot on the left,
    // so nobody covers him when he comes. Perzifanie faces the same way as Elijah.
    // Returns false (nothing changed) if one of the three is missing.
    private bool PlaceClassmatesRightOfPulubi()
    {
        if (classmate1.image == null || classmate2.image == null || pulubi.image == null)
            return false;

        RectTransform parentTransform = classmate1.image.rectTransform.parent as RectTransform;
        if (parentTransform == null)
            return false;

        classmate2.image.rectTransform.localRotation = classmate1.image.rectTransform.localRotation;

        const float edgeMargin = 10f;
        float halfScreen = parentTransform.rect.width * 0.5f;
        float elijahHalf = VisibleHalfWidth(classmate1.image);
        float perzifanieHalf = VisibleHalfWidth(classmate2.image);

        float left = pulubi.spotPosition.x + VisibleHalfWidth(pulubi.image) + edgeMargin;
        float elijahX = left + elijahHalf;
        float perzifanieX = Mathf.Max(halfScreen - edgeMargin - perzifanieHalf, elijahX);

        // On very wide screens they would drift apart: keep them close together (around the same middle).
        float closest = (elijahHalf + perzifanieHalf) * 0.95f;
        if (perzifanieX - elijahX > closest)
        {
            float middle = (elijahX + perzifanieX) * 0.5f;
            elijahX = middle - closest * 0.5f;
            perzifanieX = middle + closest * 0.5f;
        }

        classmate1.spotPosition = new Vector2(elijahX, classmate1.spotPosition.y);
        classmate2.spotPosition = new Vector2(perzifanieX, classmate2.spotPosition.y);
        return true;
    }

    // Half the width of what is drawn: with Preserve Aspect on (SceneAtmosphere turns it on), the drawing is
    // narrower than its box.
    private static float VisibleHalfWidth(Image image)
    {
        RectTransform imageTransform = image.rectTransform;
        float width = imageTransform.rect.width * Mathf.Abs(imageTransform.localScale.x);
        if (image.preserveAspect && image.sprite != null && image.sprite.rect.height > 0f)
        {
            float height = imageTransform.rect.height * Mathf.Abs(imageTransform.localScale.y);
            width = Mathf.Min(width, height * image.sprite.rect.width / image.sprite.rect.height);
        }

        return width * 0.5f;
    }

    // Slides the given characters together until none of them sticks out past the screen edges.
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

            float halfImage = VisibleHalfWidth(character.image);
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
    // Everything is stopped first so sounds copied from other scenes stay silent.
    private void FindSounds()
    {
        foreach (AudioSource audioSource in FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
        {
            if (ambientAudioSource == null && (SoundMatches(audioSource, "ambient") || SoundMatches(audioSource, "ambien")))
                ambientAudioSource = audioSource;
            else if (clickAudioSource == null && SoundMatches(audioSource, "click"))
                clickAudioSource = audioSource;
            else if (heartbeatAudioSource == null && SoundMatches(audioSource, "beat"))
                heartbeatAudioSource = audioSource;

            audioSource.playOnAwake = false;
            audioSource.Stop();
        }

        if (clickAudioSource != null)
            clickAudioSource.loop = false;

        if (heartbeatAudioSource != null)
            heartbeatAudioSource.loop = true;

        if (ambientAudioSource != null)
        {
            ambientVolume = ambientAudioSource.volume;
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

        // intro → Elijah's mood (from the quiz) → the lunch money → the pulubi → the choice
        bool letCopy = PlayerDatabase.GetChoice(A2_Croom_control.LetCopyChoiceId) == "let_copy";
        bool lentMoney = PlayerDatabase.GetChoice(A2_Croom_control.LendMoneyChoiceId) == "lent";

        StartLines(introLines, () =>
            StartLines(letCopy ? letCopyMoodLines : refusedCopyMoodLines, () =>
                StartLines(lentMoney ? hungryLines : fullLines, () =>
                    StartLines(pulubiLines, ShowPulubiChoice))));
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

    // Plays a list of lines; when the player presses Next on the last one, onFinished runs.
    private void StartLines(List<A2UwianDialogueLine> lines, System.Action onFinished)
    {
        currentLines = lines;
        currentLineIndex = 0;
        onLinesFinished = onFinished;

        if (currentLines.Count > 0)
            StartCoroutine(PlayLine(currentLineIndex));
        else
            FinishLines();
    }

    private void FinishLines()
    {
        System.Action next = onLinesFinished;
        onLinesFinished = null;
        next?.Invoke();
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

        FinishLines();
    }

    private void ShowPulubiChoice()
    {
        const string question = "The pulubi asked for money for his sick child's medicine. All you had left was your pamasahe.";

        ShowChoice(
            giveChoiceText, () =>
            {
                PlayerDatabase.RecordChoice(PulubiChoiceId, question, giveChoiceText, givePoints, GaveFareAnswer);
                StartLines(giveLines, () => StartCoroutine(FadeOutScene(true)));
            },
            keepChoiceText, () =>
            {
                PlayerDatabase.RecordChoice(PulubiChoiceId, question, keepChoiceText, keepPoints, KeptFareAnswer);
                StartLines(keepLines, () => StartCoroutine(FadeOutScene(false)));
            },
            () =>
            {
                PlayerDatabase.RecordChoice(PulubiChoiceId, question, "(Said nothing and was pulled away)", 0, HesitatedAnswer);
                StartLines(timeoutLines, () => StartCoroutine(FadeOutScene(false)));
            });
    }

    private void ShowChoice(string firstLabel, System.Action firstChosen, string secondLabel, System.Action secondChosen, System.Action onTimeout)
    {
        waitingForChoice = true;
        StopJitter();
        if (nextButton != null) nextButton.SetActive(false);
        if (choiceContainer != null) choiceContainer.SetActive(true);

        SetupChoiceButton(choiceButton1, choiceButton1Text, firstLabel, firstChosen);
        SetupChoiceButton(choiceButton2, choiceButton2Text, secondLabel, secondChosen);

        if (choiceTimeLimit > 0f && onTimeout != null)
            countdownCoroutine = StartCoroutine(ChoiceCountdown(onTimeout));
    }

    private void SetupChoiceButton(Button button, TMP_Text buttonText, string label, System.Action onChosen)
    {
        if (button == null)
            return;

        button.gameObject.SetActive(true);
        if (buttonText != null) buttonText.text = label;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            if (!waitingForChoice)
                return;

            waitingForChoice = false;
            PlayClick();
            EndCountdown();
            if (choiceContainer != null) choiceContainer.SetActive(false);
            onChosen();
        });
    }

    // A big number counts down above the choices. The street noise fades and a heartbeat
    // (if there is one) takes over. When it reaches zero, the friends decide for you.
    private IEnumerator ChoiceCountdown(System.Action onTimeout)
    {
        countdownText = CreateCountdownText();
        StartCoroutine(FadeAmbient(ambientVolume * 0.15f, 1f));
        if (heartbeatAudioSource != null && heartbeatAudioSource.clip != null)
            heartbeatAudioSource.Play();

        float timeLeft = choiceTimeLimit;
        while (timeLeft > 0f && waitingForChoice)
        {
            timeLeft -= Time.deltaTime;

            if (countdownText != null)
            {
                int secondsLeft = Mathf.CeilToInt(Mathf.Max(timeLeft, 0f));
                countdownText.text = secondsLeft.ToString();
                countdownText.color = secondsLeft <= 3 ? new Color(1f, 0.45f, 0.45f) : Color.white;

                // A small pulse at the start of each second.
                float secondProgress = 1f - (timeLeft - Mathf.Floor(timeLeft));
                countdownText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, Mathf.Clamp01(secondProgress * 3f));
            }

            yield return null;
        }

        countdownCoroutine = null;
        if (!waitingForChoice)
            yield break;

        waitingForChoice = false;
        EndCountdown();
        if (choiceContainer != null) choiceContainer.SetActive(false);
        onTimeout();
    }

    private void EndCountdown()
    {
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
            countdownCoroutine = null;
        }

        if (countdownText != null)
        {
            Destroy(countdownText.gameObject);
            countdownText = null;
        }

        if (heartbeatAudioSource != null)
            heartbeatAudioSource.Stop();
    }

    private TMP_Text CreateCountdownText()
    {
        Transform canvasTransform = dialogueBox != null ? dialogueBox.transform.parent : null;
        if (canvasTransform == null)
            return null;

        GameObject textObject = new GameObject("ChoiceCountdown", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textTransform = textObject.GetComponent<RectTransform>();
        textTransform.SetParent(canvasTransform, false);
        textTransform.anchorMin = new Vector2(0.35f, 0.8f);
        textTransform.anchorMax = new Vector2(0.65f, 0.97f);
        textTransform.offsetMin = Vector2.zero;
        textTransform.offsetMax = Vector2.zero;
        textTransform.SetAsLastSibling();

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (messageInputText != null)
            text.font = messageInputText.font;
        text.fontSize = 72f;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.outlineWidth = 0.25f;
        text.outlineColor = Color.black;
        text.text = Mathf.CeilToInt(choiceTimeLimit).ToString();
        return text;
    }

    private IEnumerator PlayLine(int index)
    {
        A2UwianDialogueLine line = currentLines[index];

        if (line.actionBefore != A2UwianAction.None)
        {
            isPlayingAction = true;
            if (nextButton != null) nextButton.SetActive(false);
            StopJitter();
            yield return PerformAction(line.actionBefore);
            isPlayingAction = false;
        }

        DisplayLine(line);
    }

    private IEnumerator PerformAction(A2UwianAction action)
    {
        switch (action)
        {
            case A2UwianAction.Classmate1Enters:
                yield return WalkIn(classmate1, SideOf(classmate1), walkInDuration);
                break;

            case A2UwianAction.Classmate2Enters:
                yield return WalkIn(classmate2, SideOf(classmate2), walkInDuration);
                break;

            case A2UwianAction.PulubiApproaches:
                if (dialogueBox != null) dialogueBox.SetActive(false);
                StartCoroutine(FadeAmbient(ambientVolume * pulubiMomentVolume, 1f));
                yield return WalkIn(pulubi, SideOf(pulubi), pulubiWalkDuration);
                break;

            case A2UwianAction.PulubiLeaves:
                if (dialogueBox != null) dialogueBox.SetActive(false);
                yield return RunOffScreen(pulubi, pulubiWalkDuration, SideOf(pulubi));
                break;

            case A2UwianAction.PulubiLeavesSlowly:
                // He walks away slowly, and nobody says anything.
                if (dialogueBox != null) dialogueBox.SetActive(false);
                yield return new WaitForSeconds(0.5f);
                yield return RunOffScreen(pulubi, pulubiSlowWalkDuration, SideOf(pulubi));
                yield return new WaitForSeconds(0.8f);
                break;

            case A2UwianAction.FriendsLeave:
                yield return FriendsLeave();
                break;

            case A2UwianAction.PulledAway:
                yield return PulledAway();
                break;

            case A2UwianAction.LeaveTogether:
                yield return LeaveTogether();
                break;

            case A2UwianAction.Pause:
                if (dialogueBox != null) dialogueBox.SetActive(false);
                yield return new WaitForSeconds(1.2f);
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

    // side: -1 leaves through the left edge, 1 through the right edge.
    private IEnumerator RunOffScreen(UwianCharacter character, float duration, float side = 1f)
    {
        if (!IsOnScreen(character))
            yield break;

        RectTransform imageTransform = character.image.rectTransform;
        RectTransform parentTransform = imageTransform.parent as RectTransform;
        if (parentTransform == null)
            yield break;

        Vector2 startPosition = imageTransform.anchoredPosition;
        Vector2 exitPosition = startPosition + new Vector2(side * parentTransform.rect.width, 0f);

        // Turns to face the edge they leave through, then goes.
        Quaternion restRotation = imageTransform.localRotation;
        yield return CharacterTurn.FaceExit(imageTransform, side);
        yield return MoveImage(imageTransform, startPosition, exitPosition, duration);
        character.image.gameObject.SetActive(false);
        imageTransform.localRotation = restRotation;
    }

    // The friends ride the jeep home. Elijah stops once to look back before following.
    private IEnumerator FriendsLeave()
    {
        if (dialogueBox != null) dialogueBox.SetActive(false);

        Coroutine classmate2Leave = StartCoroutine(RunOffScreen(classmate2, walkInDuration * 1.5f));
        yield return new WaitForSeconds(0.6f);

        if (IsOnScreen(classmate1))
        {
            RectTransform elijahTransform = classmate1.image.rectTransform;
            Vector2 lookBackPosition = classmate1.spotPosition + new Vector2(60f, 0f);
            yield return MoveImage(elijahTransform, classmate1.spotPosition, lookBackPosition, 0.4f);
            yield return new WaitForSeconds(0.7f);
            yield return RunOffScreen(classmate1, walkInDuration * 1.5f);
        }

        yield return classmate2Leave;

        // Now it's just you. The street goes quiet.
        StartCoroutine(FadeAmbient(ambientVolume * aloneVolume, 2f));
        yield return new WaitForSeconds(1f);
    }

    // Elijah rushes toward the player (grows, like grabbing you by the arm).
    private IEnumerator PulledAway()
    {
        if (dialogueBox != null) dialogueBox.SetActive(false);
        if (!IsOnScreen(classmate1))
            yield break;

        RectTransform elijahTransform = classmate1.image.rectTransform;
        Vector3 closerScale = classmate1.baseScale * 1.15f;
        Vector2 closerPosition = Vector2.Lerp(classmate1.spotPosition, Vector2.zero, 0.3f);

        Coroutine move = StartCoroutine(MoveImage(elijahTransform, classmate1.spotPosition, closerPosition, 0.2f));
        yield return ScaleImage(elijahTransform, classmate1.baseScale, closerScale, 0.2f);
        yield return move;
        yield return new WaitForSeconds(0.3f);

        move = StartCoroutine(MoveImage(elijahTransform, closerPosition, classmate1.spotPosition, 0.25f));
        yield return ScaleImage(elijahTransform, closerScale, classmate1.baseScale, 0.25f);
        yield return move;
    }

    private IEnumerator LeaveTogether()
    {
        if (dialogueBox != null) dialogueBox.SetActive(false);

        Coroutine classmate2Run = StartCoroutine(RunOffScreen(classmate2, walkInDuration * 1.3f));
        yield return new WaitForSeconds(0.3f);
        Coroutine classmate1Run = StartCoroutine(RunOffScreen(classmate1, walkInDuration * 1.3f));

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

    private void DisplayLine(A2UwianDialogueLine line)
    {
        if (dialogueBox != null) dialogueBox.SetActive(true);
        if (nextButton != null) nextButton.SetActive(true);
        if (choiceContainer != null) choiceContainer.SetActive(false);

        if (charNameText != null)
            charNameText.text = line.speakerName;

        UpdateSpeakerEffects(line.speakerName);

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        // The player's quiet thoughts type out slower, so they sink in.
        bool isHeavyThought = line.speakerName == playerName && line.message.StartsWith("(");
        typingCoroutine = StartCoroutine(TypeText(line.message, isHeavyThought ? slowTypingSpeed : typingSpeed));
    }

    // The speaking character stays bright and jitters; everyone else darkens.
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

    // Moves the speaker in front of the other characters while keeping all of them behind the dialogue box.
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
            CharacterVoice.Speak(charNameText != null ? charNameText.text : null, messageInputText, i);
            yield return new WaitForSeconds(DialoguePacing.Delay(messageInputText, i, speed));
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

    // walkingAlone: a slower, quieter fade, like the long walk home.
    private IEnumerator FadeOutScene(bool walkingAlone)
    {
        isEnding = true;
        StopJitter();
        if (nextButton != null) nextButton.SetActive(false);
        if (dialogueBox != null) dialogueBox.SetActive(false);

        float fadeDuration = walkingAlone ? 2.5f : 1f;
        StartCoroutine(FadeAmbient(0f, fadeDuration));

        if (fadeOutScreen != null)
            yield return FadeBlackScreen(0f, 1f, fadeDuration);

        yield return new WaitForSeconds(walkingAlone ? 1f : 0.3f);
        LoadNextScene();
    }

    // FadeOut may carry an Animator (copied from FadeIn) that would fight the script, so it is turned off.
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

    private IEnumerator FadeBlackScreen(float fromAlpha, float toAlpha, float fadeDuration)
    {
        PrepareFadeScreen(out CanvasGroup canvasGroup, out Image fadeImage);
        SetFadeAlpha(canvasGroup, fadeImage, fromAlpha);

        float duration = Mathf.Max(fadeDuration, 0.01f);
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
            homeSceneName = "A2_Lroom_uwi";

        SceneManager.LoadScene(homeSceneName);
    }
}
