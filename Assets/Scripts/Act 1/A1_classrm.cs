using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public enum A1ClassroomAction
{
    None,
    Classmate1Enters,
    Classmate2Enters,
    TeacherArrives,
    SitDown,
    TimeSkip,
    Playful,
    LeaveTogether
}

[System.Serializable]
public class A1ClassroomDialogueLine
{
    public A1ClassroomAction actionBefore;
    public string speakerName;
    [TextArea(2, 5)] public string message;

    public A1ClassroomDialogueLine(string name, string msg, A1ClassroomAction action = A1ClassroomAction.None)
    {
        speakerName = name;
        message = msg;
        actionBefore = action;
    }
}

public class A1_classrm : MonoBehaviour
{
    [Header("Cutscene Objects (auto-found by name if empty)")]
    public GameObject fadeScreenIn;
    public GameObject fadeOutScreen;
    public GameObject dialogueBox;
    public GameObject nextButton;
    public GameObject choiceContainer;

    [Header("Characters (auto-found by name if empty)")]
    public Image classmate1Image;
    public Image classmate2Image;
    [SerializeField] private string classmate1Name = "Elijah";
    [SerializeField] private string classmate2Name = "Classmate";
    [SerializeField] private string teacherName = "Ma'am";
    [SerializeField] private float walkInDuration = 1f;
    [SerializeField] private float sitDownDuration = 0.8f;

    [Header("Speaking Effects")]
    [SerializeField] private Color darkenedTint = new Color(0.45f, 0.45f, 0.45f, 1f);
    [SerializeField] private float tintDuration = 0.15f;
    [SerializeField] private float jitterDuration = 0.3f;
    [SerializeField] private float jitterStrength = 6f;

    [Header("Time Skip")]
    public TMP_Text timeSkipText;
    [SerializeField] private string timeSkipMessage = "A few hours later...";
    [SerializeField] private float timeSkipHoldDuration = 2f;
    [SerializeField] private float timeSkipFontSize = 48f;

    [Header("Teacher Arrival Sound (optional)")]
    public AudioSource sfxSource;
    public AudioClip teacherArrivesClip;

    [Header("Text UI References (auto-found by name if empty)")]
    public TMP_Text charNameText;
    public TMP_Text messageInputText;

    [Header("Typewriter Speed Settings")]
    [SerializeField] private float typingSpeed = 0.03f;

    [Header("Scene Transition")]
    [SerializeField] private string nextSceneName = "A1_School_Uwian";

    [Header("Dialogue Sequence")]
    public List<A1ClassroomDialogueLine> dialogueList = new List<A1ClassroomDialogueLine>();

    private int currentLineIndex = 0;
    private Coroutine typingCoroutine;
    private bool isTyping;
    private bool isPlayingAction;
    private bool dialogueFinished;
    private Vector2 classmate1SpotPosition;
    private Vector2 classmate2SpotPosition;
    private Color classmate1BaseColor = Color.white;
    private Color classmate2BaseColor = Color.white;
    private Coroutine classmate1TintCoroutine;
    private Coroutine classmate2TintCoroutine;
    private Coroutine jitterCoroutine;
    private RectTransform jitterTarget;
    private Vector2 jitterRestPosition;

    void Start()
    {
        if (dialogueList.Count == 0)
        {
            dialogueList.Add(new A1ClassroomDialogueLine(classmate1Name, "Haah... haah... We made it!", A1ClassroomAction.Classmate1Enters));
            dialogueList.Add(new A1ClassroomDialogueLine("You", "Wait... is Ma'am here already?"));
            dialogueList.Add(new A1ClassroomDialogueLine(classmate2Name, "HOY! Why are you two only getting here now?!", A1ClassroomAction.Classmate2Enters));
            dialogueList.Add(new A1ClassroomDialogueLine(classmate1Name, "Hehe... traffic?"));
            dialogueList.Add(new A1ClassroomDialogueLine(classmate2Name, "Traffic?! Your house is literally a five-minute walk away!"));
            dialogueList.Add(new A1ClassroomDialogueLine("You", "I overslept... sorry."));
            dialogueList.Add(new A1ClassroomDialogueLine(classmate2Name, "Hay nako. You're lucky Ma'am isn't here yet. She hasn't checked attendance."));
            dialogueList.Add(new A1ClassroomDialogueLine(classmate1Name, "See? I told you, no need to rush."));
            dialogueList.Add(new A1ClassroomDialogueLine(classmate2Name, "Uy, don't be late next time, okay? I was getting nervous for you two."));
            dialogueList.Add(new A1ClassroomDialogueLine(teacherName, "Good morning, class! Everyone, take your seats.", A1ClassroomAction.TeacherArrives));
            dialogueList.Add(new A1ClassroomDialogueLine(classmate2Name, "Ayan na! Ma'am's here! Sit down, quick!"));
            dialogueList.Add(new A1ClassroomDialogueLine(classmate1Name, "Okay, okay! Tara!"));
            dialogueList.Add(new A1ClassroomDialogueLine("You", "(Phew... that was close.)", A1ClassroomAction.SitDown));

            dialogueList.Add(new A1ClassroomDialogueLine(classmate2Name, "Hay nako, finally! I thought Ma'am's discussion would never end.", A1ClassroomAction.TimeSkip));
            dialogueList.Add(new A1ClassroomDialogueLine("You", "Same... my brain is fried."));
            dialogueList.Add(new A1ClassroomDialogueLine(classmate1Name, "Wait, what did she even teach today?"));
            dialogueList.Add(new A1ClassroomDialogueLine(classmate2Name, "HOY! You were sleeping the whole time, weren't you?!"));
            dialogueList.Add(new A1ClassroomDialogueLine(classmate1Name, "I was just resting my eyes. Iba 'yon!"));
            dialogueList.Add(new A1ClassroomDialogueLine("You", "She said there's a quiz tomorrow on everything she discussed."));
            dialogueList.Add(new A1ClassroomDialogueLine(classmate1Name, "Ha?! Seryoso? Uy... can I copy your notes later?"));
            dialogueList.Add(new A1ClassroomDialogueLine(classmate2Name, "Ayoko nga! Write your own notes, lazy!"));
            dialogueList.Add(new A1ClassroomDialogueLine(classmate1Name, "Sige na, please? I'll treat you to fishball on the way home!"));
            dialogueList.Add(new A1ClassroomDialogueLine(classmate2Name, "...Fine. But add kwek-kwek too."));
            dialogueList.Add(new A1ClassroomDialogueLine("You", "Hoy, what about me? I want some too!"));
            dialogueList.Add(new A1ClassroomDialogueLine(classmate1Name, "You pay for your own! HAHAHA!"));
            dialogueList.Add(new A1ClassroomDialogueLine(classmate2Name, "Hay nako, ang kuripot mo talaga!", A1ClassroomAction.Playful));
            dialogueList.Add(new A1ClassroomDialogueLine(classmate1Name, "Aray! Okay, okay, my treat! Tara, let's go home!"));
            dialogueList.Add(new A1ClassroomDialogueLine("You", "Hoy, wait for me!", A1ClassroomAction.LeaveTogether));
        }

        FindSceneReferences();

        if (choiceContainer != null) choiceContainer.SetActive(false);
        if (fadeOutScreen != null) fadeOutScreen.SetActive(false);
        if (dialogueBox != null) dialogueBox.SetActive(false);
        if (fadeScreenIn != null) fadeScreenIn.SetActive(true);

        if (classmate1Image != null)
        {
            classmate1SpotPosition = classmate1Image.rectTransform.anchoredPosition;
            classmate1BaseColor = classmate1Image.color;
            classmate1Image.gameObject.SetActive(false);
        }

        if (classmate2Image != null)
        {
            classmate2SpotPosition = classmate2Image.rectTransform.anchoredPosition;
            classmate2BaseColor = classmate2Image.color;
            classmate2Image.gameObject.SetActive(false);
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

        if (canvasTransform == null)
            return;

        if (dialogueBox == null) dialogueBox = FindDeepObject(canvasTransform, "DialogueBox");
        if (fadeScreenIn == null) fadeScreenIn = FindDeepObject(canvasTransform, "FadeIn");
        if (fadeOutScreen == null) fadeOutScreen = FindDeepObject(canvasTransform, "FadeOut");
        if (choiceContainer == null) choiceContainer = FindDeepObject(canvasTransform, "choice_Container");

        if (classmate1Image == null)
        {
            GameObject classmateObject = FindDeepObject(canvasTransform, "classmate1");
            if (classmateObject != null) classmate1Image = classmateObject.GetComponent<Image>();
        }

        if (classmate2Image == null)
        {
            GameObject classmateObject = FindDeepObject(canvasTransform, "classmate2");
            if (classmateObject != null) classmate2Image = classmateObject.GetComponent<Image>();
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

    IEnumerator StartCutscene()
    {
        yield return new WaitForSeconds(3f);
        if (fadeScreenIn != null) fadeScreenIn.SetActive(false);

        yield return new WaitForSeconds(1f);

        if (dialogueList.Count > 0)
            StartCoroutine(PlayLine(currentLineIndex));
    }

    public void NextButton()
    {
        if (isPlayingAction || dialogueFinished)
            return;

        if (isTyping)
        {
            CompleteTypingInstantly();
            return;
        }

        if (currentLineIndex + 1 >= dialogueList.Count)
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
        A1ClassroomDialogueLine line = dialogueList[index];

        if (line.actionBefore != A1ClassroomAction.None)
        {
            isPlayingAction = true;
            if (nextButton != null) nextButton.SetActive(false);
            StopJitter();
            yield return PerformAction(line.actionBefore);
            isPlayingAction = false;
        }

        DisplayLine(line);
    }

    private IEnumerator PerformAction(A1ClassroomAction action)
    {
        switch (action)
        {
            case A1ClassroomAction.Classmate1Enters:
                yield return WalkIn(classmate1Image, classmate1SpotPosition, -1f);
                break;

            case A1ClassroomAction.Classmate2Enters:
                yield return WalkIn(classmate2Image, classmate2SpotPosition, 1f);
                break;

            case A1ClassroomAction.TeacherArrives:
                if (dialogueBox != null) dialogueBox.SetActive(false);
                if (sfxSource != null && teacherArrivesClip != null)
                    sfxSource.PlayOneShot(teacherArrivesClip);
                yield return new WaitForSeconds(1f);
                break;

            case A1ClassroomAction.SitDown:
                if (dialogueBox != null) dialogueBox.SetActive(false);
                Coroutine classmate1Sit = StartCoroutine(SitDown(classmate1Image));
                Coroutine classmate2Sit = StartCoroutine(SitDown(classmate2Image));
                yield return classmate1Sit;
                yield return classmate2Sit;
                yield return new WaitForSeconds(0.5f);
                break;

            case A1ClassroomAction.TimeSkip:
                yield return PlayTimeSkip();
                break;

            case A1ClassroomAction.Playful:
                yield return PlayfulHit();
                break;

            case A1ClassroomAction.LeaveTogether:
                yield return LeaveTogether();
                break;
        }
    }

    private IEnumerator PlayTimeSkip()
    {
        if (dialogueBox != null) dialogueBox.SetActive(false);

        // Cut to black instantly (no fade) before the time skip text shows up.
        if (fadeOutScreen != null)
        {
            PrepareFadeScreen(out CanvasGroup canvasGroup, out Image fadeImage);
            SetFadeAlpha(canvasGroup, fadeImage, 1f);
        }

        yield return new WaitForSeconds(0.5f);

        // Classmates are back on their spots behind the black screen, ready to talk after class.
        ResetClassmateAtSpot(classmate1Image, classmate1SpotPosition, classmate1BaseColor);
        ResetClassmateAtSpot(classmate2Image, classmate2SpotPosition, classmate2BaseColor);

        TMP_Text skipText = GetTimeSkipText();
        if (skipText != null)
        {
            skipText.text = timeSkipMessage;
            skipText.transform.SetAsLastSibling();
            skipText.gameObject.SetActive(true);
            yield return FadeTextAlpha(skipText, 0f, 1f, 0.5f);
            yield return new WaitForSeconds(timeSkipHoldDuration);
            yield return FadeTextAlpha(skipText, 1f, 0f, 0.5f);
            skipText.gameObject.SetActive(false);
        }

        if (fadeOutScreen != null)
        {
            yield return FadeBlackScreen(1f, 0f);
            fadeOutScreen.SetActive(false);
        }

        yield return new WaitForSeconds(0.5f);
    }

    private void ResetClassmateAtSpot(Image classmateImage, Vector2 spotPosition, Color baseColor)
    {
        if (classmateImage == null)
            return;

        classmateImage.rectTransform.anchoredPosition = spotPosition;
        classmateImage.color = baseColor;
        classmateImage.gameObject.SetActive(true);
    }

    private TMP_Text GetTimeSkipText()
    {
        if (timeSkipText != null)
            return timeSkipText;

        Transform canvasTransform = fadeOutScreen != null ? fadeOutScreen.transform.parent : null;
        if (canvasTransform == null && dialogueBox != null)
            canvasTransform = dialogueBox.transform.parent;
        if (canvasTransform == null)
            return null;

        GameObject textObject = new GameObject("TimeSkipText", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textTransform = textObject.GetComponent<RectTransform>();
        textTransform.SetParent(canvasTransform, false);
        textTransform.anchorMin = Vector2.zero;
        textTransform.anchorMax = Vector2.one;
        textTransform.offsetMin = Vector2.zero;
        textTransform.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (messageInputText != null)
            text.font = messageInputText.font;
        text.fontSize = timeSkipFontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;

        textObject.SetActive(false);
        timeSkipText = text;
        return timeSkipText;
    }

    private IEnumerator FadeTextAlpha(TMP_Text text, float fromAlpha, float toAlpha, float fadeDuration)
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

    // Classmate2 lunges at classmate1 for a playful hit, classmate1 gets knocked back a little.
    private IEnumerator PlayfulHit()
    {
        if (classmate1Image == null || classmate2Image == null ||
            !classmate1Image.gameObject.activeSelf || !classmate2Image.gameObject.activeSelf)
            yield break;

        RectTransform hitterTransform = classmate2Image.rectTransform;
        RectTransform targetTransform = classmate1Image.rectTransform;

        float towardTarget = classmate1SpotPosition.x - classmate2SpotPosition.x;
        Vector2 lungePosition = classmate2SpotPosition + new Vector2(towardTarget * 0.4f, 0f);
        Vector2 knockedPosition = classmate1SpotPosition + new Vector2(Mathf.Sign(towardTarget) * 30f, 0f);

        yield return MoveImage(hitterTransform, classmate2SpotPosition, lungePosition, 0.12f);

        Coroutine hitterBack = StartCoroutine(MoveImage(hitterTransform, lungePosition, classmate2SpotPosition, 0.25f));
        yield return MoveImage(targetTransform, classmate1SpotPosition, knockedPosition, 0.08f);
        yield return MoveImage(targetTransform, knockedPosition, classmate1SpotPosition, 0.2f);
        yield return hitterBack;
    }

    // Classmate2 runs off first, classmate1 chases after her.
    private IEnumerator LeaveTogether()
    {
        if (dialogueBox != null) dialogueBox.SetActive(false);

        Coroutine classmate2Run = StartCoroutine(RunOffScreen(classmate2Image));
        yield return new WaitForSeconds(0.3f);
        Coroutine classmate1Run = StartCoroutine(RunOffScreen(classmate1Image));

        yield return classmate2Run;
        yield return classmate1Run;
        yield return new WaitForSeconds(0.3f);
    }

    private IEnumerator RunOffScreen(Image classmateImage)
    {
        if (classmateImage == null || !classmateImage.gameObject.activeSelf)
            yield break;

        RectTransform imageTransform = classmateImage.rectTransform;
        RectTransform parentTransform = imageTransform.parent as RectTransform;
        if (parentTransform == null)
            yield break;

        Vector2 startPosition = imageTransform.anchoredPosition;
        Vector2 exitPosition = startPosition + new Vector2(parentTransform.rect.width, 0f);

        yield return MoveImage(imageTransform, startPosition, exitPosition, walkInDuration);
        classmateImage.gameObject.SetActive(false);
    }

    // side: -1 enters from the left edge, 1 enters from the right edge
    private IEnumerator WalkIn(Image classmateImage, Vector2 spotPosition, float side)
    {
        if (classmateImage == null)
            yield break;

        RectTransform imageTransform = classmateImage.rectTransform;
        RectTransform parentTransform = imageTransform.parent as RectTransform;
        if (parentTransform == null)
            yield break;

        Vector2 startPosition = spotPosition + new Vector2(side * parentTransform.rect.width, 0f);
        imageTransform.anchoredPosition = startPosition;
        classmateImage.gameObject.SetActive(true);

        yield return MoveImage(imageTransform, startPosition, spotPosition, walkInDuration);
    }

    private IEnumerator SitDown(Image classmateImage)
    {
        if (classmateImage == null || !classmateImage.gameObject.activeSelf)
            yield break;

        RectTransform imageTransform = classmateImage.rectTransform;
        RectTransform parentTransform = imageTransform.parent as RectTransform;
        if (parentTransform == null)
            yield break;

        Vector2 startPosition = imageTransform.anchoredPosition;
        Vector2 seatPosition = startPosition - new Vector2(0f, parentTransform.rect.height);

        yield return MoveImage(imageTransform, startPosition, seatPosition, sitDownDuration);
        classmateImage.gameObject.SetActive(false);
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

    private void DisplayLine(A1ClassroomDialogueLine line)
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

    // The speaking classmate stays bright and jitters; the other one darkens.
    // When neither classmate is speaking (You / Ma'am), both darken.
    private void UpdateSpeakerEffects(string speakerName)
    {
        bool classmate1Speaking = speakerName == classmate1Name;
        bool classmate2Speaking = speakerName == classmate2Name;

        classmate1TintCoroutine = SetClassmateTint(classmate1Image, classmate1TintCoroutine,
            classmate1Speaking ? classmate1BaseColor : Darken(classmate1BaseColor));
        classmate2TintCoroutine = SetClassmateTint(classmate2Image, classmate2TintCoroutine,
            classmate2Speaking ? classmate2BaseColor : Darken(classmate2BaseColor));

        StopJitter();
        if (classmate1Speaking)
            StartJitter(classmate1Image);
        else if (classmate2Speaking)
            StartJitter(classmate2Image);
    }

    private Color Darken(Color baseColor)
    {
        return new Color(baseColor.r * darkenedTint.r, baseColor.g * darkenedTint.g, baseColor.b * darkenedTint.b, baseColor.a);
    }

    private Coroutine SetClassmateTint(Image classmateImage, Coroutine runningTint, Color targetColor)
    {
        if (classmateImage == null)
            return null;

        if (runningTint != null)
            StopCoroutine(runningTint);

        if (!classmateImage.gameObject.activeInHierarchy)
        {
            classmateImage.color = targetColor;
            return null;
        }

        return StartCoroutine(TintImage(classmateImage, targetColor));
    }

    private IEnumerator TintImage(Image classmateImage, Color targetColor)
    {
        Color startColor = classmateImage.color;
        float duration = Mathf.Max(tintDuration, 0.01f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            classmateImage.color = Color.Lerp(startColor, targetColor, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        classmateImage.color = targetColor;
    }

    private void StartJitter(Image classmateImage)
    {
        if (classmateImage == null || !classmateImage.gameObject.activeInHierarchy)
            return;

        jitterTarget = classmateImage.rectTransform;
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

    private IEnumerator FadeOutScene()
    {
        if (nextButton != null) nextButton.SetActive(false);
        if (dialogueBox != null) dialogueBox.SetActive(false);

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
        if (string.IsNullOrEmpty(nextSceneName))
            nextSceneName = "A1_School_Uwian";

        SceneManager.LoadScene(nextSceneName);
    }
}
