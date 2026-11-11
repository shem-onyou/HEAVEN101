using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public enum A2CroomAction
{
    None,
    ElijahEnters,
    ClassmateEnters,
    TeacherArrives,
    SitDown,
    QuizStarts,
    ElijahApproaches,
    ElijahGoesBack,
    ShowScore,
    FriendsReturn
}

[System.Serializable]
public class A2QuizQuestion
{
    [TextArea(1, 3)] public string question;
    public List<string> options = new List<string>();
    [Tooltip("Which option is correct (0 = first).")]
    public int correctIndex;

    public A2QuizQuestion(string text, int correct, params string[] answers)
    {
        question = text;
        correctIndex = correct;
        options = new List<string>(answers);
    }
}

[System.Serializable]
public class A2CroomDialogueLine
{
    public A2CroomAction actionBefore;
    public string speakerName;
    [TextArea(2, 5)] public string message;

    public A2CroomDialogueLine(string name, string msg, A2CroomAction action = A2CroomAction.None)
    {
        speakerName = name;
        message = msg;
        actionBefore = action;
    }
}

// Act 2 classroom: the friends chat, Ma'am arrives and the quiz begins as a mini-game (answer buttons + timer).
// Midway, Elijah sneaks over to copy the answer the player just picked.
// Choice 1: let him copy or refuse. If he copies, he asks if the answer is really right.
// Choice 2: admit you're not sure (you never reviewed) or insist it's right. Then the quiz goes on.
// Dialogue can use {number} (the question Elijah asks about), {answer} (what the player picked for it),
// {score} and {total}.
public class A2_Croom_control : MonoBehaviour
{
    public const string LetCopyChoiceId = "act2_let_copy";
    public const string AnswerSureChoiceId = "act2_answer_sure";
    public const string LendMoneyChoiceId = "act2_lend_money";

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

    [Header("Choice 1: Elijah wants to copy")]
    [SerializeField] private string letCopyChoiceText = "Fine, copy mine.";
    [SerializeField] private string refuseChoiceText = "Sorry, I can't.";
    [SerializeField] private int letCopyPoints = -1;
    [SerializeField] private int refusePoints = 1;

    [Header("Choice 2: \"Is that really right?\"")]
    [SerializeField] private string truthChoiceText = "Honestly... I'm not sure.";
    [SerializeField] private string insistChoiceText = "Yeah, it's right!";
    [SerializeField] private int truthPoints = 1;
    [SerializeField] private int insistPoints = -1;

    [Header("Choice 3: Elijah wants to borrow lunch money")]
    [SerializeField] private string lendChoiceText = "Sure, here.";
    [SerializeField] private string keepMoneyChoiceText = "Sorry, I need it too.";
    [SerializeField] private int lendPoints = 1;
    [SerializeField] private int keepMoneyPoints = 0;

    [Header("Characters (auto-found by name if empty)")]
    public Image classmate1Image;
    public Image classmate2Image;
    [SerializeField] private string classmate1Name = "Elijah";
    [SerializeField] private string classmate2Name = "Perzifanie";
    [SerializeField] private string teacherName = "Ma'am";
    [SerializeField] private float walkInDuration = 1f;
    [SerializeField] private float sitDownDuration = 0.8f;
    [Tooltip("How much bigger Elijah looks when he leans in close to whisper.")]
    [SerializeField] private float leanInScale = 1.15f;

    [Header("Speaking Effects")]
    [SerializeField] private Color darkenedTint = new Color(0.45f, 0.45f, 0.45f, 1f);
    [SerializeField] private float tintDuration = 0.15f;
    [SerializeField] private float jitterDuration = 0.3f;
    [SerializeField] private float jitterStrength = 6f;

    [Header("Quiz")]
    [SerializeField] private string quizTitle = "QUIZ TIME";
    [SerializeField] private float quizTitleHoldDuration = 1.5f;
    [Tooltip("Up to 7 questions, each with 2 answers (shown on the two choice buttons).")]
    public List<A2QuizQuestion> quizQuestions = new List<A2QuizQuestion>();
    [Tooltip("Elijah interrupts right after the player answers this question number (1 = first).")]
    [SerializeField] private int interruptAfterQuestion = 3;
    [Tooltip("Lowest score that passes. With 7 questions, 4 means 3/7 and below fails.")]
    [SerializeField] private int passingScore = 4;
    [Tooltip("How loud the classroom stays during the quiz (everyone is quiet).")]
    [Range(0f, 1f)] [SerializeField] private float quietClassVolume = 0.2f;

    [Header("Text UI References (auto-found by name if empty)")]
    public TMP_Text charNameText;
    public TMP_Text messageInputText;

    [Header("Typewriter Speed Settings")]
    [SerializeField] private float typingSpeed = 0.03f;

    [Header("Sounds (auto-found by object or clip name if empty)")]
    [Tooltip("Classroom chatter, looped. Found by \"ambien\". Stays silent until it has a clip.")]
    [SerializeField] private AudioSource ambientAudioSource;
    [Tooltip("Optional: an AudioSource with the next_click clip.")]
    [SerializeField] private AudioSource clickAudioSource;
    [Tooltip("Optional: rings when Ma'am arrives. Found by \"bell\" or \"teacher\".")]
    [SerializeField] private AudioSource teacherAudioSource;

    // Renamed from nextSceneName: the scene saved an empty value there, which kept the game on black.
    [Header("Scene Transition")]
    [SerializeField] private string uwianSceneName = "A2_School_uwian";

    [Header("Dialogue Sequence")]
    [Tooltip("Chat and Ma'am arrives, up to the start of the quiz.")]
    public List<A2CroomDialogueLine> introLines = new List<A2CroomDialogueLine>();
    [Tooltip("Midway through the quiz: Elijah leans in and asks to copy.")]
    public List<A2CroomDialogueLine> copyRequestLines = new List<A2CroomDialogueLine>();
    [Tooltip("Choice 1 = let him copy. Ends with Elijah asking if the answer is right.")]
    public List<A2CroomDialogueLine> letCopyLines = new List<A2CroomDialogueLine>();
    [Tooltip("Played instead of letCopyLines when the player admitted in A2_School that they didn't review. " +
             "Elijah already knows, so he doesn't ask if the answer is right.")]
    public List<A2CroomDialogueLine> honestLetCopyLines = new List<A2CroomDialogueLine>();
    [Tooltip("Choice 1 = refuse.")]
    public List<A2CroomDialogueLine> refuseLines = new List<A2CroomDialogueLine>();
    [Tooltip("Choice 2 = admit you're not sure.")]
    public List<A2CroomDialogueLine> truthLines = new List<A2CroomDialogueLine>();
    [Tooltip("Choice 2 = insist the answer is right.")]
    public List<A2CroomDialogueLine> insistLines = new List<A2CroomDialogueLine>();
    [Tooltip("The quiz ends and the score is shown.")]
    public List<A2CroomDialogueLine> outroLines = new List<A2CroomDialogueLine>();
    [Tooltip("The friends come back and ask about the score.")]
    public List<A2CroomDialogueLine> afterQuizLines = new List<A2CroomDialogueLine>();
    [Tooltip("Played if the score is at or above Passing Score.")]
    public List<A2CroomDialogueLine> passLines = new List<A2CroomDialogueLine>();
    [Tooltip("Played if the score is below Passing Score.")]
    public List<A2CroomDialogueLine> failLines = new List<A2CroomDialogueLine>();
    [Tooltip("After the score talk: Elijah asks to borrow money for lunch, but the player needs it too.")]
    public List<A2CroomDialogueLine> moneyRequestLines = new List<A2CroomDialogueLine>();
    [Tooltip("Choice 3 = lend the money (the player will be short).")]
    public List<A2CroomDialogueLine> lendLines = new List<A2CroomDialogueLine>();
    [Tooltip("Choice 3 = keep the money.")]
    public List<A2CroomDialogueLine> keepMoneyLines = new List<A2CroomDialogueLine>();

    private class ClassCharacter
    {
        public Image image;
        public string speakerName;
        public Vector2 spotPosition;
        public Vector3 baseScale = Vector3.one;
        public Color baseColor = Color.white;
        public Coroutine tintCoroutine;
    }

    private ClassCharacter elijah;
    private ClassCharacter classmate;
    private ClassCharacter[] characters;

    private List<A2CroomDialogueLine> currentLines;
    private System.Action onLinesFinished;
    private int currentLineIndex = 0;
    private Coroutine typingCoroutine;
    private bool isTyping;
    private bool isPlayingAction;
    private bool waitingForChoice;
    private bool isEnding;
    private float ambientVolume = 1f;

    // Quiz mini-game state.
    private int[] playerAnswers;
    private int currentQuestion;
    private bool elijahInterrupted;

    private Coroutine jitterCoroutine;
    private RectTransform jitterTarget;
    private Vector2 jitterRestPosition;

    void Start()
    {
        FillDefaultLines();
        FindSceneReferences();

        elijah = CreateCharacter(classmate1Image, classmate1Name);
        classmate = CreateCharacter(classmate2Image, classmate2Name);
        characters = new[] { elijah, classmate };

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
            introLines.Add(new A2CroomDialogueLine(classmate1Name, "Haaay... I'm so nervous for the quiz.", A2CroomAction.ElijahEnters));
            introLines.Add(new A2CroomDialogueLine(classmate2Name, "Hay nako, you should've studied last night!", A2CroomAction.ClassmateEnters));
            introLines.Add(new A2CroomDialogueLine(classmate1Name, "I tried! I fell asleep on page one. Hehe."));
            introLines.Add(new A2CroomDialogueLine("You", "(I didn't study either... I just have to do my best.)"));
            introLines.Add(new A2CroomDialogueLine(teacherName, "Good morning, class! Take out one half sheet of paper. We'll have our quiz now.", A2CroomAction.TeacherArrives));
            introLines.Add(new A2CroomDialogueLine(classmate2Name, "Ayan na! Good luck, everyone!"));
            introLines.Add(new A2CroomDialogueLine(teacherName, "No talking and no copying, okay? You may begin.", A2CroomAction.SitDown));
            introLines.Add(new A2CroomDialogueLine("You", "(Okay... focus. I can do this.)"));
        }

        if (copyRequestLines.Count == 0)
        {
            copyRequestLines.Add(new A2CroomDialogueLine(classmate1Name, "Psst... psst!", A2CroomAction.ElijahApproaches));
            copyRequestLines.Add(new A2CroomDialogueLine(classmate1Name, "What's your answer for number {number}? Pakopya naman, please...!"));
        }

        if (letCopyLines.Count == 0)
        {
            letCopyLines.Add(new A2CroomDialogueLine("You", "(...Ugh.) It's \"{answer}\". Quick, before Ma'am sees."));
            letCopyLines.Add(new A2CroomDialogueLine(classmate1Name, "Salamat! You're the best!"));
            // If the player lied this morning, Elijah trusts them because they "reviewed".
            letCopyLines.Add(new A2CroomDialogueLine(classmate1Name, A2_School_control.LiedAboutReviewing
                ? "Wait... you reviewed last night, right? So that's really the right answer?"
                : "Wait... are you sure that's really the right answer?"));
        }

        if (honestLetCopyLines.Count == 0)
        {
            honestLetCopyLines.Add(new A2CroomDialogueLine("You", "(...Ugh.) It's \"{answer}\". Quick, before Ma'am sees."));
            honestLetCopyLines.Add(new A2CroomDialogueLine(classmate1Name, "Salamat! I know you didn't review either, but it's still better than my guess. Hehe."));
            honestLetCopyLines.Add(new A2CroomDialogueLine("You", "(I hope it's even right...)", A2CroomAction.ElijahGoesBack));
        }

        if (refuseLines.Count == 0)
        {
            refuseLines.Add(new A2CroomDialogueLine("You", "Sorry, Elijah... I can't. That's cheating."));
            refuseLines.Add(new A2CroomDialogueLine(classmate1Name, "Tsk... kuripot! Fine, fine."));
            refuseLines.Add(new A2CroomDialogueLine("You", "(I hope he's not mad at me...)", A2CroomAction.ElijahGoesBack));
        }

        if (truthLines.Count == 0)
        {
            truthLines.Add(new A2CroomDialogueLine("You", "Honestly... I'm not sure. I didn't really review."));
            truthLines.Add(new A2CroomDialogueLine(classmate1Name, "Ha?! Hay nako... okay, I'll just trust my own guess then."));
            truthLines.Add(new A2CroomDialogueLine("You", "(At least I didn't lie to him this time.)", A2CroomAction.ElijahGoesBack));
        }

        if (insistLines.Count == 0)
        {
            insistLines.Add(new A2CroomDialogueLine("You", "Y-yeah! Of course it's right. Trust me."));
            insistLines.Add(new A2CroomDialogueLine(classmate1Name, "Nice! I owe you one!"));
            insistLines.Add(new A2CroomDialogueLine("You", "(...What am I doing? I don't even know if it's right.)", A2CroomAction.ElijahGoesBack));
        }

        if (outroLines.Count == 0)
        {
            outroLines.Add(new A2CroomDialogueLine(teacherName, "Time's up! Pass your papers forward."));
            outroLines.Add(new A2CroomDialogueLine(teacherName, "Let's check them together. Exchange papers... okay, here are your scores."));
        }

        if (afterQuizLines.Count == 0)
        {
            afterQuizLines.Add(new A2CroomDialogueLine(classmate1Name, "So?! What did you get? Patingin!", A2CroomAction.FriendsReturn));
        }

        if (passLines.Count == 0)
        {
            passLines.Add(new A2CroomDialogueLine("You", "I got {score} out of {total}!", A2CroomAction.ShowScore));
            passLines.Add(new A2CroomDialogueLine(classmate2Name, "Wow! Congrats! Pasado ka!"));
            passLines.Add(new A2CroomDialogueLine(classmate1Name, "Ang galing! Libre mo naman kami mamaya, hehe!"));
            passLines.Add(new A2CroomDialogueLine("You", "Hehe, thanks! (Phew... I passed.)"));
        }

        if (failLines.Count == 0)
        {
            failLines.Add(new A2CroomDialogueLine("You", "...I only got {score} out of {total}.", A2CroomAction.ShowScore));
            failLines.Add(new A2CroomDialogueLine(classmate2Name, "Hala... it's okay. You'll do better next time."));
            failLines.Add(new A2CroomDialogueLine(classmate1Name, "Same tayo, hehe... apir na lang?"));
            failLines.Add(new A2CroomDialogueLine("You", "(I wanted today to be perfect... and I failed.)"));
        }

        if (moneyRequestLines.Count == 0)
        {
            moneyRequestLines.Add(new A2CroomDialogueLine(classmate1Name, "Uy... can I ask a favor?"));
            moneyRequestLines.Add(new A2CroomDialogueLine(classmate1Name, "Can I borrow some money? I'm short for lunch today... hehe."));
            moneyRequestLines.Add(new A2CroomDialogueLine("You", "(I only have enough for my own lunch...)"));
            moneyRequestLines.Add(new A2CroomDialogueLine("You", "(If I lend it to him, I'll be the one who goes hungry.)"));
        }

        if (lendLines.Count == 0)
        {
            lendLines.Add(new A2CroomDialogueLine("You", "Sure... here."));
            lendLines.Add(new A2CroomDialogueLine(classmate1Name, "Wow, thank you! I'll pay you back tomorrow, promise!"));
            lendLines.Add(new A2CroomDialogueLine(classmate2Name, "Hala, what about your lunch?"));
            lendLines.Add(new A2CroomDialogueLine("You", "Hehe... I'm not that hungry anyway."));
        }

        if (keepMoneyLines.Count == 0)
        {
            keepMoneyLines.Add(new A2CroomDialogueLine("You", "Sorry, Elijah... I only have enough for myself."));
            keepMoneyLines.Add(new A2CroomDialogueLine(classmate1Name, "Ah... okay, it's fine. I'll figure something out."));
            keepMoneyLines.Add(new A2CroomDialogueLine(classmate2Name, "Here, Elijah. Let's just share my baon."));
        }

        // Basic high school math, two answers each. The correct one is not always the first button.
        if (quizQuestions.Count == 0)
        {
            quizQuestions.Add(new A2QuizQuestion("What is 12 x 8?", 0, "96", "86"));
            quizQuestions.Add(new A2QuizQuestion("Solve for x: x + 7 = 15", 1, "x = 22", "x = 8"));
            quizQuestions.Add(new A2QuizQuestion("What is 25% of 80?", 0, "20", "25"));
            quizQuestions.Add(new A2QuizQuestion("What is the square root of 144?", 1, "14", "12"));
            quizQuestions.Add(new A2QuizQuestion("Solve for x: 3x = 27", 0, "x = 9", "x = 24"));
            quizQuestions.Add(new A2QuizQuestion("What is (-4) x (-5)?", 1, "-20", "20"));
            quizQuestions.Add(new A2QuizQuestion("What is the area of a rectangle that is 6 cm long and 4 cm wide?", 0, "24 square cm", "20 square cm"));
        }
    }

    // Fills in {number}, {answer}, {score} and {total} in a dialogue line.
    private string FillTokens(string message)
    {
        int askedIndex = Mathf.Clamp(interruptAfterQuestion, 1, Mathf.Max(quizQuestions.Count, 1)) - 1;
        string askedAnswer = "...";
        if (playerAnswers != null && askedIndex < quizQuestions.Count && playerAnswers[askedIndex] >= 0)
            askedAnswer = quizQuestions[askedIndex].options[playerAnswers[askedIndex]];

        return message
            .Replace("{number}", (askedIndex + 1).ToString())
            .Replace("{answer}", askedAnswer)
            .Replace("{score}", CountCorrectAnswers().ToString())
            .Replace("{total}", quizQuestions.Count.ToString());
    }

    private int CountCorrectAnswers()
    {
        if (playerAnswers == null)
            return 0;

        int correct = 0;
        for (int i = 0; i < quizQuestions.Count; i++)
        {
            if (playerAnswers[i] == quizQuestions[i].correctIndex)
                correct++;
        }

        return correct;
    }

    private ClassCharacter CreateCharacter(Image characterImage, string speakerName)
    {
        ClassCharacter character = new ClassCharacter { image = characterImage, speakerName = speakerName };
        if (characterImage != null)
        {
            character.spotPosition = characterImage.rectTransform.anchoredPosition;
            character.baseScale = characterImage.rectTransform.localScale;
            character.baseColor = characterImage.color;
            characterImage.gameObject.SetActive(false);
        }

        return character;
    }

    // Characters on the left half of the screen come and go through the left edge, and vice versa.
    private static float SideOf(ClassCharacter character)
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
            else if (teacherAudioSource == null && (SoundMatches(audioSource, "bell") || SoundMatches(audioSource, "teacher")))
                teacherAudioSource = audioSource;
        }

        foreach (AudioSource effect in new[] { clickAudioSource, teacherAudioSource })
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

    IEnumerator StartCutscene()
    {
        yield return new WaitForSeconds(0.5f);
        yield return FadeFromBlack(1.5f);
        StartCoroutine(FadeAmbient(ambientVolume, 1.5f));
        yield return new WaitForSeconds(0.5f);

        StartLines(introLines, () => StartCoroutine(StartQuiz()));
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

    // Plays a list of lines; when the player presses Next on the last one, onFinished runs.
    private void StartLines(List<A2CroomDialogueLine> lines, System.Action onFinished)
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

        PlayEffect(clickAudioSource);

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

    private void ShowCopyChoice()
    {
        string question = copyRequestLines.Count > 0 ? FillTokens(copyRequestLines[copyRequestLines.Count - 1].message) : "Can I copy your answer?";

        ShowChoice(letCopyChoiceText, () =>
        {
            PlayerDatabase.RecordChoice(LetCopyChoiceId, "Elijah: " + question, letCopyChoiceText, letCopyPoints, "let_copy");

            // The player told Elijah this morning that they didn't review, so he won't ask "are you sure?".
            if (PlayerDatabase.GetChoice(A2_School_control.ReviewChoiceId) == A2_School_control.HonestAnswer)
            {
                // Clear an old answer from a previous playthrough so it doesn't show in the results.
                PlayerDatabase.Data.choices.RemoveAll(choice => choice.choiceId == AnswerSureChoiceId);
                PlayerDatabase.Save();
                StartLines(honestLetCopyLines, ResumeQuiz);
                return;
            }

            StartLines(letCopyLines, ShowAnswerSureChoice);
        },
        refuseChoiceText, () =>
        {
            PlayerDatabase.RecordChoice(LetCopyChoiceId, "Elijah: " + question, refuseChoiceText, refusePoints, "refused");
            StartLines(refuseLines, ResumeQuiz);
        });
    }

    private void ShowAnswerSureChoice()
    {
        string question = letCopyLines.Count > 0 ? FillTokens(letCopyLines[letCopyLines.Count - 1].message) : "Is that really right?";

        ShowChoice(truthChoiceText, () =>
        {
            PlayerDatabase.RecordChoice(AnswerSureChoiceId, "Elijah: " + question, truthChoiceText, truthPoints, "truth");
            StartLines(truthLines, ResumeQuiz);
        },
        insistChoiceText, () =>
        {
            PlayerDatabase.RecordChoice(AnswerSureChoiceId, "Elijah: " + question, insistChoiceText, insistPoints, "insisted");
            StartLines(insistLines, ResumeQuiz);
        });
    }

    // Ma'am ends the quiz, the friends come back to see the score and react (pass or fail),
    // then Elijah asks to borrow lunch money.
    private void PlayOutro()
    {
        bool passed = CountCorrectAnswers() >= passingScore;

        StartLines(outroLines, () =>
            StartLines(afterQuizLines, () =>
                StartLines(passed ? passLines : failLines, () =>
                    StartLines(moneyRequestLines, ShowMoneyChoice))));
    }

    // The dilemma: helping Elijah means the player goes without lunch.
    private void ShowMoneyChoice()
    {
        string question = "Elijah asked to borrow money for lunch, but you only had enough for yourself.";

        ShowChoice(lendChoiceText, () =>
        {
            PlayerDatabase.RecordChoice(LendMoneyChoiceId, question, lendChoiceText, lendPoints, "lent");
            StartLines(lendLines, () => StartCoroutine(FadeOutScene()));
        },
        keepMoneyChoiceText, () =>
        {
            PlayerDatabase.RecordChoice(LendMoneyChoiceId, question, keepMoneyChoiceText, keepMoneyPoints, "kept");
            StartLines(keepMoneyLines, () => StartCoroutine(FadeOutScene()));
        });
    }

    // ---------------- Quiz mini-game ----------------
    // Each question shows in the dialogue box, and its two answers use the normal choice buttons.

    private IEnumerator StartQuiz()
    {
        isPlayingAction = true;
        if (nextButton != null) nextButton.SetActive(false);
        StopJitter();
        yield return ShowQuizTitle();
        isPlayingAction = false;

        if (quizQuestions.Count == 0)
        {
            PlayOutro();
            yield break;
        }

        playerAnswers = new int[quizQuestions.Count];
        for (int i = 0; i < playerAnswers.Length; i++)
            playerAnswers[i] = -1;

        currentQuestion = 0;
        ShowQuestion();
    }

    private void ShowQuestion()
    {
        A2QuizQuestion question = quizQuestions[currentQuestion];
        DisplayLine(new A2CroomDialogueLine($"Question {currentQuestion + 1}/{quizQuestions.Count}", question.question));

        string firstAnswer = question.options.Count > 0 ? question.options[0] : "";
        string secondAnswer = question.options.Count > 1 ? question.options[1] : "";
        ShowChoice(firstAnswer, () => AnswerQuestion(0), secondAnswer, () => AnswerQuestion(1));
    }

    private void AnswerQuestion(int optionIndex)
    {
        playerAnswers[currentQuestion] = optionIndex;

        // Midway through, Elijah leans over and asks about the question just answered.
        if (!elijahInterrupted && currentQuestion + 1 == interruptAfterQuestion)
        {
            elijahInterrupted = true;
            StartLines(copyRequestLines, ShowCopyChoice);
            return;
        }

        NextQuestion();
    }

    private void ResumeQuiz()
    {
        NextQuestion();
    }

    private void NextQuestion()
    {
        currentQuestion++;
        if (currentQuestion < quizQuestions.Count)
        {
            ShowQuestion();
            return;
        }

        // All questions answered: save the score and let Ma'am end the quiz.
        int score = CountCorrectAnswers();
        string result = score >= passingScore ? "passed" : "failed";
        PlayerDatabase.RecordAction("act2_quiz_score", $"Took the Act 2 math quiz and {result} ({score}/{quizQuestions.Count})", score, 0);
        PlayOutro();
    }

    // A big score card in the middle of the screen for a moment.
    private IEnumerator ShowScoreCard()
    {
        if (dialogueBox != null) dialogueBox.SetActive(false);

        Transform canvasTransform = dialogueBox != null ? dialogueBox.transform.parent : null;
        if (canvasTransform == null)
            yield break;

        TMP_Text scoreText = CreateCenterText(canvasTransform, "QuizScore", $"Score: {CountCorrectAnswers()} / {quizQuestions.Count}", 56f);
        yield return FadeTextAlpha(scoreText, 0f, 1f, 0.5f);
        yield return new WaitForSeconds(1.5f);
        yield return FadeTextAlpha(scoreText, 1f, 0f, 0.5f);
        Destroy(scoreText.gameObject);
    }

    private void ShowChoice(string firstLabel, System.Action firstChosen, string secondLabel, System.Action secondChosen)
    {
        waitingForChoice = true;
        if (nextButton != null) nextButton.SetActive(false);
        if (choiceContainer != null) choiceContainer.SetActive(true);

        SetupChoiceButton(choiceButton1, choiceButton1Text, firstLabel, firstChosen);
        SetupChoiceButton(choiceButton2, choiceButton2Text, secondLabel, secondChosen);
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
            PlayEffect(clickAudioSource);
            if (choiceContainer != null) choiceContainer.SetActive(false);
            onChosen();
        });
    }

    private IEnumerator PlayLine(int index)
    {
        A2CroomDialogueLine line = currentLines[index];

        if (line.actionBefore != A2CroomAction.None)
        {
            isPlayingAction = true;
            if (nextButton != null) nextButton.SetActive(false);
            StopJitter();
            yield return PerformAction(line.actionBefore);
            isPlayingAction = false;
        }

        DisplayLine(line);
    }

    private IEnumerator PerformAction(A2CroomAction action)
    {
        switch (action)
        {
            case A2CroomAction.ElijahEnters:
                yield return WalkIn(elijah, SideOf(elijah), walkInDuration);
                break;

            case A2CroomAction.ClassmateEnters:
                yield return WalkIn(classmate, SideOf(classmate), walkInDuration);
                break;

            case A2CroomAction.TeacherArrives:
                // The bell rings and the chatter dies down as Ma'am walks in.
                if (dialogueBox != null) dialogueBox.SetActive(false);
                PlayEffect(teacherAudioSource);
                StartCoroutine(FadeAmbient(ambientVolume * quietClassVolume, 1f));
                yield return new WaitForSeconds(1f);
                break;

            case A2CroomAction.SitDown:
                if (dialogueBox != null) dialogueBox.SetActive(false);
                Coroutine elijahSits = StartCoroutine(SitDown(elijah));
                Coroutine classmateSits = StartCoroutine(SitDown(classmate));
                yield return elijahSits;
                yield return classmateSits;
                yield return new WaitForSeconds(0.3f);
                break;

            case A2CroomAction.QuizStarts:
                yield return ShowQuizTitle();
                break;

            case A2CroomAction.ElijahApproaches:
                yield return ElijahLeansIn();
                break;

            case A2CroomAction.ElijahGoesBack:
                if (dialogueBox != null) dialogueBox.SetActive(false);
                yield return SitDown(elijah);
                break;

            case A2CroomAction.ShowScore:
                yield return ShowScoreCard();
                break;

            case A2CroomAction.FriendsReturn:
                // After the quiz, both friends walk back over to the player's desk.
                if (dialogueBox != null) dialogueBox.SetActive(false);
                Coroutine elijahBack = StartCoroutine(WalkIn(elijah, SideOf(elijah), walkInDuration));
                Coroutine classmateBack = StartCoroutine(WalkIn(classmate, SideOf(classmate), walkInDuration));
                yield return elijahBack;
                yield return classmateBack;
                break;
        }
    }

    // side: -1 enters from the left edge, 1 enters from the right edge
    private IEnumerator WalkIn(ClassCharacter character, float side, float duration)
    {
        if (character.image == null)
            yield break;

        RectTransform imageTransform = character.image.rectTransform;
        RectTransform parentTransform = imageTransform.parent as RectTransform;
        if (parentTransform == null)
            yield break;

        Vector2 startPosition = character.spotPosition + new Vector2(side * parentTransform.rect.width, 0f);
        imageTransform.anchoredPosition = startPosition;
        imageTransform.localScale = character.baseScale;
        character.image.gameObject.SetActive(true);

        yield return MoveImage(imageTransform, startPosition, character.spotPosition, duration);
    }

    // The character drops down out of view, like sitting at their desk.
    private IEnumerator SitDown(ClassCharacter character)
    {
        if (character.image == null || !character.image.gameObject.activeSelf)
            yield break;

        RectTransform imageTransform = character.image.rectTransform;
        RectTransform parentTransform = imageTransform.parent as RectTransform;
        if (parentTransform == null)
            yield break;

        Vector2 startPosition = imageTransform.anchoredPosition;
        Vector2 seatPosition = startPosition - new Vector2(0f, parentTransform.rect.height);

        yield return MoveImage(imageTransform, startPosition, seatPosition, sitDownDuration);
        character.image.gameObject.SetActive(false);
        imageTransform.localScale = character.baseScale;
    }

    // Elijah pops up from below, close to the player (a bit bigger, like he is leaning in to whisper).
    private IEnumerator ElijahLeansIn()
    {
        if (elijah.image == null)
            yield break;

        if (dialogueBox != null) dialogueBox.SetActive(false);

        RectTransform imageTransform = elijah.image.rectTransform;
        RectTransform parentTransform = imageTransform.parent as RectTransform;
        if (parentTransform == null)
            yield break;

        Vector2 startPosition = elijah.spotPosition - new Vector2(0f, parentTransform.rect.height);
        imageTransform.anchoredPosition = startPosition;
        imageTransform.localScale = elijah.baseScale * leanInScale;
        elijah.image.gameObject.SetActive(true);

        yield return MoveImage(imageTransform, startPosition, elijah.spotPosition, 0.6f);
    }

    // The quiet classroom dims and "QUIZ TIME" shows in the middle for a moment.
    private IEnumerator ShowQuizTitle()
    {
        if (dialogueBox != null) dialogueBox.SetActive(false);

        Transform canvasTransform = fadeOutScreen != null ? fadeOutScreen.transform.parent : null;
        if (canvasTransform == null && dialogueBox != null)
            canvasTransform = dialogueBox.transform.parent;
        if (canvasTransform == null)
            yield break;

        Image dimScreen = PrepareFadeOutImage();
        TMP_Text title = CreateCenterText(canvasTransform, "QuizTitle", quizTitle, 64f);

        if (dimScreen != null) StartCoroutine(FadeImageAlpha(dimScreen, 0f, 0.6f, 0.5f));
        yield return FadeTextAlpha(title, 0f, 1f, 0.5f);
        yield return new WaitForSeconds(quizTitleHoldDuration);
        if (dimScreen != null) StartCoroutine(FadeImageAlpha(dimScreen, 0.6f, 0f, 0.5f));
        yield return FadeTextAlpha(title, 1f, 0f, 0.5f);

        Destroy(title.gameObject);
        if (fadeOutScreen != null) fadeOutScreen.SetActive(false);
    }

    private TMP_Text CreateCenterText(Transform canvasTransform, string objectName, string message, float fontSize)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textTransform = textObject.GetComponent<RectTransform>();
        textTransform.SetParent(canvasTransform, false);
        textTransform.anchorMin = new Vector2(0.1f, 0.3f);
        textTransform.anchorMax = new Vector2(0.9f, 0.7f);
        textTransform.offsetMin = Vector2.zero;
        textTransform.offsetMax = Vector2.zero;
        textTransform.SetAsLastSibling();

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (messageInputText != null)
            text.font = messageInputText.font;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.alpha = 0f;
        text.raycastTarget = false;
        text.text = message;
        return text;
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

    private void DisplayLine(A2CroomDialogueLine line)
    {
        if (dialogueBox != null) dialogueBox.SetActive(true);
        if (nextButton != null) nextButton.SetActive(true);
        if (choiceContainer != null) choiceContainer.SetActive(false);

        if (charNameText != null)
            charNameText.text = line.speakerName;

        UpdateSpeakerEffects(line.speakerName);

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        typingCoroutine = StartCoroutine(TypeText(FillTokens(line.message)));
    }

    // The speaker stays bright, comes to the front and jitters; everyone else darkens.
    private void UpdateSpeakerEffects(string speakerName)
    {
        StopJitter();

        foreach (ClassCharacter character in characters)
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

        foreach (ClassCharacter other in characters)
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

    // The classroom fades to black (and its sounds die down) before the next scene.
    private IEnumerator FadeOutScene()
    {
        isEnding = true;
        StopJitter();
        if (nextButton != null) nextButton.SetActive(false);
        if (choiceContainer != null) choiceContainer.SetActive(false);
        if (dialogueBox != null) dialogueBox.SetActive(false);
        StartCoroutine(FadeAmbient(0f, 1.2f));

        Image blackScreen = PrepareFadeOutImage();
        if (blackScreen != null)
            yield return FadeImageAlpha(blackScreen, 0f, 1f, 1.2f);

        yield return new WaitForSeconds(0.5f);

        if (string.IsNullOrEmpty(uwianSceneName))
            uwianSceneName = "A2_School_uwian";

        SceneManager.LoadScene(uwianSceneName);
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
}
