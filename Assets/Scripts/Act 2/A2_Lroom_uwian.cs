using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public enum A2LroomUwiAction
{
    None,
    NanayEnters,
    NanayRushesIn,
    NanayHugs,
    NanayLeaves,
    PhoneBuzz,
    Pause,
    LeaveHouse,
    ComeBackHome
}

[System.Serializable]
public class A2LroomUwiDialogueLine
{
    public A2LroomUwiAction actionBefore;
    public string speakerName;
    [TextArea(2, 5)] public string message;

    public A2LroomUwiDialogueLine(string name, string msg, A2LroomUwiAction action = A2LroomUwiAction.None)
    {
        speakerName = name;
        message = msg;
        actionBefore = action;
    }
}

// Act 2 coming home. Two choices:
// 1. Tell Nanay the truth (why you came home late / how the quiz went) or lie.
// 2. The heaviest one: Nanay leaves for her night shift and makes you promise to stay home,
//    then Elijah texts that he needs you. Keep the promise (+1) or go to your friend (-1).
public class A2_Lroom_uwian : MonoBehaviour
{
    public const string HonestChoiceId = "act2_honest_nanay";
    public const string PromiseChoiceId = "act2_promise_nanay";
    public const string StayedAnswer = "stayed";
    public const string WentAnswer = "went";

    public static bool WentToElijah => PlayerDatabase.GetChoice(PromiseChoiceId) == WentAnswer;

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

    [Header("Choice 1: Telling Nanay the truth")]
    [SerializeField] private string truthChoiceText = "Tell her the truth";
    [SerializeField] private string lieLateChoiceText = "\"Traffic lang po.\"";
    [SerializeField] private string lieQuizChoiceText = "\"I passed po!\"";
    [SerializeField] private int truthPoints = 1;
    [SerializeField] private int liePoints = -1;

    [Header("Choice 2: Nanay's promise or Elijah")]
    [SerializeField] private string stayChoiceText = "Stay home (I promised Nanay)";
    [SerializeField] private string goChoiceText = "Go to Elijah";
    [SerializeField] private int stayPoints = 1;
    [SerializeField] private int goPoints = -1;

    [Header("Nanay (auto-found by name if empty)")]
    public Image nanayImage;
    [SerializeField] private string playerName = "You";
    [SerializeField] private string nanayName = "Nanay";
    [SerializeField] private string elijahName = "Elijah";
    [SerializeField] private string elijahTextName = "Elijah (text)";
    [SerializeField] private float nanayWalkDuration = 1.2f;
    [SerializeField] private float nanayRushDuration = 0.5f;

    [Header("Room Lighting")]
    [Tooltip("Found by name. Darkens when the player comes home late and when Nanay leaves.")]
    public SpriteRenderer backgroundRenderer;
    [SerializeField] private string backgroundName = "BG_livingroom";
    [SerializeField] private Color lateEveningTint = new Color(0.75f, 0.72f, 0.8f, 1f);
    [SerializeField] private Color aloneTint = new Color(0.45f, 0.45f, 0.58f, 1f);

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
    [SerializeField] private float slowTypingSpeed = 0.06f;

    [Header("Sounds (auto-found by object or clip name if empty)")]
    [Tooltip("Evening house sounds, looped. Found by \"ambient\".")]
    [SerializeField] private AudioSource ambientAudioSource;
    [SerializeField] private AudioSource clickAudioSource;
    [Tooltip("Found by \"door\".")]
    [SerializeField] private AudioSource doorAudioSource;
    [Tooltip("Found by \"stair\".")]
    [SerializeField] private AudioSource stairsAudioSource;
    [Tooltip("Optional: a phone vibrate/notification. Found by \"phone\", \"notif\" or \"vibrat\".")]
    [SerializeField] private AudioSource phoneAudioSource;
    [Range(0f, 1f)] [SerializeField] private float aloneVolume = 0.3f;

    [Header("Scene Transition")]
    [SerializeField] private string bedroomSceneName = "A2_Broom_uwi";

    [Header("Dialogue: coming home")]
    [Tooltip("The player gave away their pamasahe and walked home in the dark.")]
    public List<A2LroomUwiDialogueLine> lateHomeLines = new List<A2LroomUwiDialogueLine>();
    public List<A2LroomUwiDialogueLine> lateTruthLines = new List<A2LroomUwiDialogueLine>();
    public List<A2LroomUwiDialogueLine> lateLieLines = new List<A2LroomUwiDialogueLine>();
    [Tooltip("The player came home on time.")]
    public List<A2LroomUwiDialogueLine> onTimeHomeLines = new List<A2LroomUwiDialogueLine>();
    public List<A2LroomUwiDialogueLine> passedQuizLines = new List<A2LroomUwiDialogueLine>();
    public List<A2LroomUwiDialogueLine> failedQuizLines = new List<A2LroomUwiDialogueLine>();
    public List<A2LroomUwiDialogueLine> quizTruthLines = new List<A2LroomUwiDialogueLine>();
    public List<A2LroomUwiDialogueLine> quizLieLines = new List<A2LroomUwiDialogueLine>();

    [Header("Dialogue: the promise")]
    public List<A2LroomUwiDialogueLine> nightShiftLines = new List<A2LroomUwiDialogueLine>();
    public List<A2LroomUwiDialogueLine> elijahTextLines = new List<A2LroomUwiDialogueLine>();
    [Tooltip("Added to Elijah's text when the player lent him lunch money.")]
    public List<A2LroomUwiDialogueLine> cantPayBackLines = new List<A2LroomUwiDialogueLine>();
    public List<A2LroomUwiDialogueLine> dilemmaLines = new List<A2LroomUwiDialogueLine>();
    public List<A2LroomUwiDialogueLine> stayLines = new List<A2LroomUwiDialogueLine>();
    public List<A2LroomUwiDialogueLine> goLines = new List<A2LroomUwiDialogueLine>();

    private List<A2LroomUwiDialogueLine> currentLines;
    private int currentLineIndex;
    private System.Action onLinesFinished;
    private Coroutine typingCoroutine;
    private bool isTyping;
    private bool isPlayingAction;
    private bool waitingForChoice;
    private bool isEnding;
    private Vector2 nanaySpotPosition;
    private Vector3 nanayBaseScale = Vector3.one;
    private Color nanayBaseColor = Color.white;
    private Coroutine nanayTintCoroutine;
    private Coroutine jitterCoroutine;
    private RectTransform jitterTarget;
    private Vector2 jitterRestPosition;
    private float ambientVolume = 1f;
    private Color backgroundBaseColor = Color.white;
    private bool onBlackScreen;

    void Start()
    {
        FillDefaultLines();
        FindSceneReferences();
        HideChoreObjects();

        if (choiceContainer != null) choiceContainer.SetActive(false);
        if (fadeOutScreen != null) fadeOutScreen.SetActive(false);
        if (dialogueBox != null) dialogueBox.SetActive(false);
        HoldBlackScreen();

        if (nanayImage != null)
        {
            nanaySpotPosition = nanayImage.rectTransform.anchoredPosition;
            nanayBaseScale = nanayImage.rectTransform.localScale;
            nanayBaseColor = nanayImage.color;
            nanayImage.gameObject.SetActive(false);
        }

        if (backgroundRenderer != null)
        {
            backgroundBaseColor = backgroundRenderer.color;
            if (A2_School_uwian_scr.WalkedHomeAlone)
                backgroundRenderer.color = MultiplyColor(backgroundBaseColor, lateEveningTint);
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

    private void FillDefaultLines()
    {
        // ---------- Came home late (gave the pamasahe away) ----------
        if (lateHomeLines.Count == 0)
        {
            lateHomeLines.Add(new A2LroomUwiDialogueLine(playerName, "N-Nanay... I'm home."));
            lateHomeLines.Add(new A2LroomUwiDialogueLine(nanayName, "ANAK! Saan ka ba galing?!", A2LroomUwiAction.NanayRushesIn));
            lateHomeLines.Add(new A2LroomUwiDialogueLine(nanayName, "It's already dark! I called you so many times! Hindi ka man lang nag-text!"));
            lateHomeLines.Add(new A2LroomUwiDialogueLine(nanayName, "Your classmates got home hours ago! <link=\"shake\">What happened?!</link>"));
            lateHomeLines.Add(new A2LroomUwiDialogueLine(playerName, "(If I tell her I walked home alone... she'll be even more upset.)"));
        }

        if (lateTruthLines.Count == 0)
        {
            lateTruthLines.Add(new A2LroomUwiDialogueLine(playerName, "Nanay... a man asked for money for his sick child. I gave him my pamasahe... so I walked."));
            lateTruthLines.Add(new A2LroomUwiDialogueLine(nanayName, "You... walked? All the way from school? In the dark? Alone?"));
            lateTruthLines.Add(new A2LroomUwiDialogueLine(nanayName, "Anak... I know you have a good heart. But what if something happened to you?"));
            lateTruthLines.Add(new A2LroomUwiDialogueLine(nanayName, "...Halika nga dito.", A2LroomUwiAction.NanayHugs));
            lateTruthLines.Add(new A2LroomUwiDialogueLine(nanayName, "Next time, call me first, ha? Please. You're all I have."));
            lateTruthLines.Add(new A2LroomUwiDialogueLine(playerName, "Opo... sorry po, Nanay."));
        }

        if (lateLieLines.Count == 0)
        {
            lateLieLines.Add(new A2LroomUwiDialogueLine(playerName, "T-traffic lang po, Nanay. The jeep took so long..."));
            lateLieLines.Add(new A2LroomUwiDialogueLine(nanayName, "...Traffic? Until this late?"));
            lateLieLines.Add(new A2LroomUwiDialogueLine(nanayName, "...Sige. Basta next time, text me, ha? I was so scared."));
            lateLieLines.Add(new A2LroomUwiDialogueLine(playerName, "Opo..."));
            lateLieLines.Add(new A2LroomUwiDialogueLine(playerName, "(I did something good today... so why did I have to lie about it?)"));
        }

        // ---------- Came home on time ----------
        if (onTimeHomeLines.Count == 0)
        {
            onTimeHomeLines.Add(new A2LroomUwiDialogueLine(playerName, "Nanay, I'm home."));
            onTimeHomeLines.Add(new A2LroomUwiDialogueLine(nanayName, "Oh, anak! Welcome home.", A2LroomUwiAction.NanayEnters));
            onTimeHomeLines.Add(new A2LroomUwiDialogueLine(nanayName, "So? How was your quiz? You said it was today, right?"));
        }

        if (passedQuizLines.Count == 0)
        {
            passedQuizLines.Add(new A2LroomUwiDialogueLine(playerName, "I passed po, Nanay!"));
            passedQuizLines.Add(new A2LroomUwiDialogueLine(nanayName, "Talaga?! Ang galing naman ng anak ko! I'm so proud of you."));
            passedQuizLines.Add(new A2LroomUwiDialogueLine(playerName, "Hehe... thank you po."));
        }

        if (failedQuizLines.Count == 0)
        {
            failedQuizLines.Add(new A2LroomUwiDialogueLine(playerName, "(I failed... Nanay was so excited about it, too.)"));
        }

        if (quizTruthLines.Count == 0)
        {
            quizTruthLines.Add(new A2LroomUwiDialogueLine(playerName, "...I failed po, Nanay. Sorry."));
            quizTruthLines.Add(new A2LroomUwiDialogueLine(nanayName, "Ay... it's okay, anak. Thank you for telling me the truth."));
            quizTruthLines.Add(new A2LroomUwiDialogueLine(nanayName, "Babawi ka next time, okay? I'll help you review.", A2LroomUwiAction.NanayHugs));
            quizTruthLines.Add(new A2LroomUwiDialogueLine(playerName, "Opo..."));
        }

        if (quizLieLines.Count == 0)
        {
            quizLieLines.Add(new A2LroomUwiDialogueLine(playerName, "I-I passed po!"));
            quizLieLines.Add(new A2LroomUwiDialogueLine(nanayName, "Talaga?! Ang galing naman ng anak ko! I'm so proud of you!", A2LroomUwiAction.NanayHugs));
            quizLieLines.Add(new A2LroomUwiDialogueLine(playerName, "(She's so happy... and it's not even true.)"));
        }

        // ---------- The promise ----------
        if (nightShiftLines.Count == 0)
        {
            nightShiftLines.Add(new A2LroomUwiDialogueLine(nanayName, "Anak, listen. They called me in for the night shift. I have to go now."));
            nightShiftLines.Add(new A2LroomUwiDialogueLine(nanayName, "There's rice and ulam on the table. Lock the door after I leave."));
            nightShiftLines.Add(new A2LroomUwiDialogueLine(nanayName, "And don't go out tonight, ha? It's dangerous outside at night."));
            nightShiftLines.Add(new A2LroomUwiDialogueLine(nanayName, "Promise me."));
            nightShiftLines.Add(new A2LroomUwiDialogueLine(playerName, "...Promise po, Nanay."));
            nightShiftLines.Add(new A2LroomUwiDialogueLine(nanayName, "Good. I love you, anak. Ingat ka dito."));
            nightShiftLines.Add(new A2LroomUwiDialogueLine(playerName, "...", A2LroomUwiAction.NanayLeaves));
        }

        if (elijahTextLines.Count == 0)
        {
            elijahTextLines.Add(new A2LroomUwiDialogueLine(playerName, "(A text from Elijah...?)", A2LroomUwiAction.PhoneBuzz));
            elijahTextLines.Add(new A2LroomUwiDialogueLine(elijahTextName, "pre... gising ka pa?"));
            elijahTextLines.Add(new A2LroomUwiDialogueLine(elijahTextName, "Papa found out about my grades. he's really angry. he's been shouting since he got home", A2LroomUwiAction.PhoneBuzz));
            elijahTextLines.Add(new A2LroomUwiDialogueLine(elijahTextName, "i'm outside our house now. i can't go back in yet", A2LroomUwiAction.PhoneBuzz));
        }

        if (cantPayBackLines.Count == 0)
        {
            cantPayBackLines.Add(new A2LroomUwiDialogueLine(elijahTextName, "and sorry... i can't pay you back tomorrow. he took my allowance", A2LroomUwiAction.PhoneBuzz));
        }

        if (dilemmaLines.Count == 0)
        {
            dilemmaLines.Add(new A2LroomUwiDialogueLine(elijahTextName, "can you come over? please. i don't want to be alone right now", A2LroomUwiAction.PhoneBuzz));
            dilemmaLines.Add(new A2LroomUwiDialogueLine(playerName, "(Nanay told me not to go out tonight... I promised her.)"));
            dilemmaLines.Add(new A2LroomUwiDialogueLine(playerName, "(But Elijah... he has never asked me for anything like this.)"));
        }

        if (stayLines.Count == 0)
        {
            stayLines.Add(new A2LroomUwiDialogueLine(playerName, "(\"Sorry Elijah... I can't leave the house tonight. I promised Nanay.\")"));
            stayLines.Add(new A2LroomUwiDialogueLine(playerName, "(Sent.)"));
            stayLines.Add(new A2LroomUwiDialogueLine(playerName, "...", A2LroomUwiAction.Pause));
            stayLines.Add(new A2LroomUwiDialogueLine(elijahTextName, "ok", A2LroomUwiAction.PhoneBuzz));
            stayLines.Add(new A2LroomUwiDialogueLine(playerName, "(...He's not replying anymore.)", A2LroomUwiAction.Pause));
            stayLines.Add(new A2LroomUwiDialogueLine(playerName, "(I kept my promise.)"));
        }

        if (goLines.Count == 0)
        {
            goLines.Add(new A2LroomUwiDialogueLine(playerName, "(\"Wait for me. I'm coming.\")"));
            goLines.Add(new A2LroomUwiDialogueLine(playerName, "(I'm sorry, Nanay...)", A2LroomUwiAction.LeaveHouse));
            goLines.Add(new A2LroomUwiDialogueLine(elijahName, "...You came."));
            goLines.Add(new A2LroomUwiDialogueLine(elijahName, "I didn't think you would. Salamat, pre. Really."));
            goLines.Add(new A2LroomUwiDialogueLine(playerName, "Of course. I'm here."));
            goLines.Add(new A2LroomUwiDialogueLine(playerName, "...", A2LroomUwiAction.ComeBackHome));
            goLines.Add(new A2LroomUwiDialogueLine(playerName, "(Nanay isn't home yet. She'll never know I left.)"));
            goLines.Add(new A2LroomUwiDialogueLine(playerName, "(...But I know.)"));
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

        if (backgroundRenderer == null)
        {
            GameObject backgroundObject = FindDeepObject(canvasTransform, backgroundName);
            if (backgroundObject == null) backgroundObject = GameObject.Find(backgroundName);
            if (backgroundObject != null) backgroundRenderer = backgroundObject.GetComponent<SpriteRenderer>();
        }

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

    // This scene was copied from the morning living room: the kalat and the chores bar stay hidden.
    private void HideChoreObjects()
    {
        foreach (ClickableUIItem kalat in FindObjectsByType<ClickableUIItem>(FindObjectsInactive.Include))
            kalat.gameObject.SetActive(false);

        Transform canvasTransform = dialogueBox != null ? dialogueBox.transform.parent : null;
        GameObject progress = canvasTransform != null ? FindDeepObject(canvasTransform, "Kalat_progress") : null;
        if (progress == null) progress = GameObject.Find("Kalat_progress");
        if (progress != null) progress.SetActive(false);
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
    // Everything is stopped first (cleaning and victory sounds were copied from the morning scene).
    private void FindSounds()
    {
        foreach (AudioSource audioSource in FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
        {
            if (ambientAudioSource == null && (SoundMatches(audioSource, "ambient") || SoundMatches(audioSource, "ambien")))
                ambientAudioSource = audioSource;
            else if (clickAudioSource == null && SoundMatches(audioSource, "click"))
                clickAudioSource = audioSource;
            else if (doorAudioSource == null && SoundMatches(audioSource, "door"))
                doorAudioSource = audioSource;
            else if (stairsAudioSource == null && SoundMatches(audioSource, "stair"))
                stairsAudioSource = audioSource;
            else if (phoneAudioSource == null && (SoundMatches(audioSource, "phone") || SoundMatches(audioSource, "notif") || SoundMatches(audioSource, "vibrat")))
                phoneAudioSource = audioSource;

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.Stop();
        }

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

    private static void PlayEffect(AudioSource effect)
    {
        if (effect != null && effect.clip != null)
            effect.PlayOneShot(effect.clip);
    }

    private static float EffectLength(AudioSource effect, float fallback, float max)
    {
        return effect != null && effect.clip != null ? Mathf.Min(effect.clip.length, max) : fallback;
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

        PlayEffect(doorAudioSource);
        yield return new WaitForSeconds(EffectLength(doorAudioSource, 0.5f, 1.5f));
        yield return FadeFromBlack(1.5f);
        StartCoroutine(FadeAmbient(ambientVolume, 1.5f));
        yield return new WaitForSeconds(0.5f);

        if (A2_School_uwian_scr.WalkedHomeAlone)
        {
            StartLines(lateHomeLines, () => ShowHonestChoice(
                "Nanay asked why you came home so late after walking home alone.",
                lieLateChoiceText, lateTruthLines, lateLieLines));
            yield break;
        }

        StartLines(onTimeHomeLines, () =>
        {
            if (PassedQuiz())
                StartLines(passedQuizLines, StartPromise);
            else
                StartLines(failedQuizLines, () => ShowHonestChoice(
                    "Nanay asked how your quiz went (you failed).",
                    lieQuizChoiceText, quizTruthLines, quizLieLines));
        });
    }

    private static bool PassedQuiz()
    {
        PlayerActionRecord quiz = PlayerDatabase.Data.actions.Find(action => action.actionId == "act2_quiz_score");
        return quiz == null || quiz.value >= 4f;
    }

    private void ShowHonestChoice(string question, string lieText, List<A2LroomUwiDialogueLine> truthLines, List<A2LroomUwiDialogueLine> lieLines)
    {
        ShowChoice(
            truthChoiceText, () =>
            {
                PlayerDatabase.RecordChoice(HonestChoiceId, question, truthChoiceText, truthPoints, "truth");
                StartLines(truthLines, StartPromise);
            },
            lieText, () =>
            {
                PlayerDatabase.RecordChoice(HonestChoiceId, question, lieText, liePoints, "lied");
                StartLines(lieLines, StartPromise);
            });
    }

    private void StartPromise()
    {
        List<A2LroomUwiDialogueLine> textLines = new List<A2LroomUwiDialogueLine>(elijahTextLines);
        if (PlayerDatabase.GetChoice(A2_Croom_control.LendMoneyChoiceId) == "lent")
            textLines.AddRange(cantPayBackLines);
        textLines.AddRange(dilemmaLines);

        StartLines(nightShiftLines, () => StartLines(textLines, ShowPromiseChoice));
    }

    private void ShowPromiseChoice()
    {
        const string question = "Nanay made you promise not to go out tonight. Then Elijah asked you to come over because he didn't want to be alone.";

        ShowChoice(
            stayChoiceText, () =>
            {
                PlayerDatabase.RecordChoice(PromiseChoiceId, question, stayChoiceText, stayPoints, StayedAnswer);
                StartLines(stayLines, () => StartCoroutine(GoUpstairs()));
            },
            goChoiceText, () =>
            {
                PlayerDatabase.RecordChoice(PromiseChoiceId, question, goChoiceText, goPoints, WentAnswer);
                StartLines(goLines, () => StartCoroutine(GoUpstairs()));
            });
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
    private void StartLines(List<A2LroomUwiDialogueLine> lines, System.Action onFinished)
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

    private void ShowChoice(string firstLabel, System.Action firstChosen, string secondLabel, System.Action secondChosen)
    {
        waitingForChoice = true;
        StopJitter();
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
        A2LroomUwiDialogueLine line = currentLines[index];

        if (line.actionBefore != A2LroomUwiAction.None)
        {
            isPlayingAction = true;
            if (nextButton != null) nextButton.SetActive(false);
            StopJitter();
            yield return PerformAction(line.actionBefore);
            isPlayingAction = false;
        }

        DisplayLine(line);
    }

    private IEnumerator PerformAction(A2LroomUwiAction action)
    {
        switch (action)
        {
            case A2LroomUwiAction.NanayEnters:
                yield return NanayWalkIn(nanayWalkDuration);
                break;

            case A2LroomUwiAction.NanayRushesIn:
                // She was waiting by the door: she rushes in, already on edge.
                if (dialogueBox != null) dialogueBox.SetActive(false);
                yield return NanayWalkIn(nanayRushDuration);
                break;

            case A2LroomUwiAction.NanayHugs:
                yield return NanayHug();
                break;

            case A2LroomUwiAction.NanayLeaves:
                yield return NanayLeaves();
                break;

            case A2LroomUwiAction.PhoneBuzz:
                yield return PhoneBuzz();
                break;

            case A2LroomUwiAction.Pause:
                if (dialogueBox != null) dialogueBox.SetActive(false);
                yield return new WaitForSeconds(1.5f);
                break;

            case A2LroomUwiAction.LeaveHouse:
                yield return LeaveHouse();
                break;

            case A2LroomUwiAction.ComeBackHome:
                yield return ComeBackHome();
                break;
        }
    }

    private IEnumerator NanayWalkIn(float duration)
    {
        if (nanayImage == null || nanayImage.gameObject.activeSelf)
            yield break;

        RectTransform nanayTransform = nanayImage.rectTransform;
        RectTransform parentTransform = nanayTransform.parent as RectTransform;
        if (parentTransform == null)
            yield break;

        float side = nanaySpotPosition.x < 0f ? -1f : 1f;
        Vector2 startPosition = nanaySpotPosition + new Vector2(side * parentTransform.rect.width, 0f);
        nanayTransform.anchoredPosition = startPosition;
        nanayImage.gameObject.SetActive(true);

        yield return MoveImage(nanayTransform, startPosition, nanaySpotPosition, duration);
    }

    // Nanay steps close (grows a bit, like pulling you into a hug), holds, then lets go.
    private IEnumerator NanayHug()
    {
        if (nanayImage == null || !nanayImage.gameObject.activeSelf)
            yield break;

        if (dialogueBox != null) dialogueBox.SetActive(false);

        RectTransform nanayTransform = nanayImage.rectTransform;
        Vector3 closeScale = nanayBaseScale * 1.15f;
        Vector2 closePosition = Vector2.Lerp(nanaySpotPosition, new Vector2(0f, nanaySpotPosition.y), 0.5f);

        Coroutine move = StartCoroutine(MoveImage(nanayTransform, nanaySpotPosition, closePosition, 0.6f));
        yield return ScaleImage(nanayTransform, nanayBaseScale, closeScale, 0.6f);
        yield return move;
        yield return new WaitForSeconds(1.2f);

        move = StartCoroutine(MoveImage(nanayTransform, closePosition, nanaySpotPosition, 0.6f));
        yield return ScaleImage(nanayTransform, closeScale, nanayBaseScale, 0.6f);
        yield return move;
    }

    // Nanay walks out, the door closes, and the room goes dim and quiet.
    private IEnumerator NanayLeaves()
    {
        if (dialogueBox != null) dialogueBox.SetActive(false);

        if (nanayImage != null && nanayImage.gameObject.activeSelf)
        {
            RectTransform nanayTransform = nanayImage.rectTransform;
            RectTransform parentTransform = nanayTransform.parent as RectTransform;
            float side = nanaySpotPosition.x < 0f ? -1f : 1f;
            float distance = parentTransform != null ? parentTransform.rect.width : 1500f;

            Vector2 startPosition = nanayTransform.anchoredPosition;

            // She turns to face the way out first.
            Quaternion restRotation = nanayTransform.localRotation;
            yield return CharacterTurn.FaceExit(nanayTransform, side);
            yield return MoveImage(nanayTransform, startPosition, startPosition + new Vector2(side * distance, 0f), nanayWalkDuration);
            nanayImage.gameObject.SetActive(false);
            nanayTransform.localRotation = restRotation;
        }

        PlayEffect(doorAudioSource);
        StartCoroutine(FadeAmbient(ambientVolume * aloneVolume, 2f));
        yield return TintBackground(aloneTint, 2f);
        yield return new WaitForSeconds(0.8f);
    }

    // The phone buzzes: the vibrate sound and the dialogue box shakes a little.
    // A phone sound placed in the scene is used if there is one; otherwise Assets/Resources/Sounds/phone_vibrate.
    private IEnumerator PhoneBuzz()
    {
        if (phoneAudioSource != null && phoneAudioSource.clip != null)
            PlayEffect(phoneAudioSource);
        else
            SoundEffects.Play("phone_vibrate");

        if (dialogueBox == null)
        {
            yield return new WaitForSeconds(0.4f);
            yield break;
        }

        bool wasActive = dialogueBox.activeSelf;
        dialogueBox.SetActive(true);
        if (charNameText != null) charNameText.text = "";
        if (messageInputText != null) messageInputText.maxVisibleCharacters = 0;

        RectTransform boxTransform = dialogueBox.transform as RectTransform;
        if (boxTransform != null)
        {
            Vector2 restPosition = boxTransform.anchoredPosition;
            float elapsed = 0f;
            const float buzzDuration = 0.45f;
            while (elapsed < buzzDuration)
            {
                elapsed += Time.deltaTime;
                boxTransform.anchoredPosition = restPosition + new Vector2(Mathf.Sin(elapsed * 90f) * 5f, 0f);
                yield return null;
            }

            boxTransform.anchoredPosition = restPosition;
        }

        if (!wasActive)
            dialogueBox.SetActive(false);

        yield return new WaitForSeconds(0.2f);
    }

    // Going out: the door, then the screen goes black and the talk with Elijah plays over it.
    private IEnumerator LeaveHouse()
    {
        if (dialogueBox != null) dialogueBox.SetActive(false);
        PlayEffect(doorAudioSource);
        StartCoroutine(FadeAmbient(0f, 1.5f));

        if (fadeOutScreen != null)
            yield return FadeBlackScreen(0f, 1f, 1.5f);

        onBlackScreen = true;
        yield return new WaitForSeconds(1.5f);

        // Keep the dialogue box above the black screen for the lines with Elijah.
        if (dialogueBox != null) dialogueBox.transform.SetAsLastSibling();
    }

    // Back home late at night: the door, and the dark living room fades back in.
    private IEnumerator ComeBackHome()
    {
        if (dialogueBox != null) dialogueBox.SetActive(false);
        yield return new WaitForSeconds(1f);

        PlayEffect(doorAudioSource);
        yield return new WaitForSeconds(EffectLength(doorAudioSource, 0.6f, 1.5f));

        if (fadeOutScreen != null && onBlackScreen)
            yield return FadeBlackScreen(1f, 0f, 1.5f);

        onBlackScreen = false;
        if (fadeOutScreen != null) fadeOutScreen.SetActive(false);
        yield return new WaitForSeconds(0.5f);
    }

    private IEnumerator TintBackground(Color tint, float duration)
    {
        if (backgroundRenderer == null)
            yield break;

        Color startColor = backgroundRenderer.color;
        Color targetColor = MultiplyColor(backgroundBaseColor, tint);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            backgroundRenderer.color = Color.Lerp(startColor, targetColor, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        backgroundRenderer.color = targetColor;
    }

    private static Color MultiplyColor(Color baseColor, Color tint)
    {
        return new Color(baseColor.r * tint.r, baseColor.g * tint.g, baseColor.b * tint.b, baseColor.a);
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

    private void DisplayLine(A2LroomUwiDialogueLine line)
    {
        if (dialogueBox != null) dialogueBox.SetActive(true);
        if (nextButton != null) nextButton.SetActive(true);
        if (choiceContainer != null) choiceContainer.SetActive(false);

        if (charNameText != null)
            charNameText.text = line.speakerName;

        UpdateSpeakerEffects(line.speakerName);

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        bool isHeavyThought = line.speakerName == playerName && line.message.StartsWith("(");
        typingCoroutine = StartCoroutine(TypeText(line.message, isHeavyThought ? slowTypingSpeed : typingSpeed));
    }

    // Nanay stays bright and jitters while she talks; she darkens while someone else talks.
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

    // Fade to black, footsteps up the stairs, then the bedroom.
    private IEnumerator GoUpstairs()
    {
        isEnding = true;
        StopJitter();
        if (nextButton != null) nextButton.SetActive(false);
        if (dialogueBox != null) dialogueBox.SetActive(false);
        StartCoroutine(FadeAmbient(0f, 1.5f));

        if (fadeOutScreen != null)
            yield return FadeBlackScreen(0f, 1f, 1.5f);

        PlayEffect(stairsAudioSource);
        yield return new WaitForSeconds(EffectLength(stairsAudioSource, 0.5f, 2.5f));

        if (string.IsNullOrEmpty(bedroomSceneName))
            bedroomSceneName = "A2_Broom_uwi";

        SceneManager.LoadScene(bedroomSceneName);
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
            fadeImage.color = new Color(0f, 0f, 0f, fadeImage.color.a);

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
}
