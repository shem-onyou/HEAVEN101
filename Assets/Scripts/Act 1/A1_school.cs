using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class A1SchoolDialogueLine
{
    public string speakerName;
    [TextArea(2, 5)] public string message;

    public A1SchoolDialogueLine(string name, string msg)
    {
        speakerName = name;
        message = msg;
    }
}

public class A1_school : MonoBehaviour
{
    [Header("Cutscene Objects")]
    public GameObject fadeScreenIn;
    public GameObject fadeOutScreen;
    public GameObject dialogueBox;
    public GameObject nextButton;

    [Header("Character Entrance")]
    public Sprite pulubiSprite;
    [SerializeField] private float pulubiMoveDuration = 2f;
    [SerializeField] private float classmateMoveDuration = 1f;

    [Header("Choice System Objects")]
    public GameObject choiceContainer;
    public Button choiceButton1;
    public TMP_Text choiceButton1Text;
    public Button choiceButton2;
    public TMP_Text choiceButton2Text;

    [Header("Text UI References")]
    public TMP_Text charNameText;
    public TMP_Text messageInputText;

    [Header("Typewriter Speed Settings")]
    [SerializeField] private float typingSpeed = 0.03f;

    [Header("Dialogue Sequence")]
    public List<A1SchoolDialogueLine> dialogueList = new List<A1SchoolDialogueLine>();

    private int currentLineIndex = 0;
    private int choiceDialogueIndex = 0;
    private Coroutine typingCoroutine;
    private bool isTyping = false;
    private Image classmate1Image;
    private Image pulubiImage;
    private Vector2 classmateRightPosition;
    private bool isPlayingChoiceDialogue;
    private bool waitingForPulubiEntrance;
    private bool pulubiDialoguePlaying;
    private bool waitingForFadeOut;
    private readonly List<A1SchoolDialogueLine> choiceDialogue = new List<A1SchoolDialogueLine>();

    void Start()
    {
        if (dialogueList.Count == 0)
        {
            dialogueList.Add(new A1SchoolDialogueLine("You", "I'm so late! Ma'am will be mad!"));
            dialogueList.Add(new A1SchoolDialogueLine("Elijah", "..."));
            dialogueList.Add(new A1SchoolDialogueLine("You", "HUY! You are late too!"));
            dialogueList.Add(new A1SchoolDialogueLine("Elijah", "Yeah, but it's fine. We don't have to rush."));
            dialogueList.Add(new A1SchoolDialogueLine("You", "But Ma'am will be mad if we're late!"));
            dialogueList.Add(new A1SchoolDialogueLine("Elijah", "Let's cut the first subject!"));
        }

        FindChoiceButtons();

        if (choiceContainer != null)
            choiceContainer.SetActive(false);

        if (fadeOutScreen != null)
            fadeOutScreen.SetActive(false);

        FindClassmate1Image();
        if (classmate1Image != null)
            classmate1Image.gameObject.SetActive(false);

        CreatePulubiImage();
        if (pulubiImage != null)
            pulubiImage.gameObject.SetActive(false);

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

    private void FindChoiceButtons()
    {
        if (choiceContainer == null)
            return;

        if (choiceButton1 == null)
        {
            Transform buttonTransform = choiceContainer.transform.Find("choice_Button1");
            if (buttonTransform != null)
                choiceButton1 = buttonTransform.GetComponent<Button>();
        }

        if (choiceButton2 == null)
        {
            Transform buttonTransform = choiceContainer.transform.Find("choice_Button2");
            if (buttonTransform != null)
                choiceButton2 = buttonTransform.GetComponent<Button>();
        }

        if (choiceButton1Text == null && choiceButton1 != null)
            choiceButton1Text = choiceButton1.GetComponentInChildren<TMP_Text>();

        if (choiceButton2Text == null && choiceButton2 != null)
            choiceButton2Text = choiceButton2.GetComponentInChildren<TMP_Text>();
    }

    private void FindClassmate1Image()
    {
        if (dialogueBox == null || dialogueBox.transform.parent == null)
            return;

        Transform classmateTransform = dialogueBox.transform.parent.Find("classmate1");
        if (classmateTransform != null)
            classmate1Image = classmateTransform.GetComponent<Image>();
    }

    private void CreatePulubiImage()
    {
        if (pulubiSprite == null || dialogueBox == null)
            return;

        RectTransform canvasTransform = dialogueBox.transform.parent as RectTransform;
        if (canvasTransform == null)
            return;

        GameObject pulubiObject = new GameObject("Pulubi", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform pulubiTransform = pulubiObject.GetComponent<RectTransform>();
        pulubiTransform.SetParent(canvasTransform, false);

        pulubiImage = pulubiObject.GetComponent<Image>();
        pulubiImage.sprite = pulubiSprite;
        pulubiImage.preserveAspect = true;

        RectTransform classmateTransform = canvasTransform.Find("classmate1") as RectTransform;
        if (classmateTransform != null)
        {
            pulubiTransform.anchorMin = classmateTransform.anchorMin;
            pulubiTransform.anchorMax = classmateTransform.anchorMax;
            pulubiTransform.pivot = classmateTransform.pivot;
            pulubiTransform.sizeDelta = classmateTransform.sizeDelta;
            pulubiTransform.localScale = classmateTransform.localScale;
            pulubiTransform.anchoredPosition = classmateTransform.anchoredPosition;
        }
        else
        {
            pulubiImage.SetNativeSize();
        }

        pulubiTransform.SetSiblingIndex(dialogueBox.transform.GetSiblingIndex());
    }

    IEnumerator StartCutscene()
    {
        yield return new WaitForSeconds(3f);
        if (fadeScreenIn != null) fadeScreenIn.SetActive(false);

        yield return new WaitForSeconds(2f);
        if (dialogueBox != null) dialogueBox.SetActive(true);

        yield return new WaitForSeconds(1f);

        if (dialogueList.Count > 0)
        {
            DisplayLine(currentLineIndex);
        }
    }

    private IEnumerator BringClassmate1In()
    {
        if (nextButton != null)
            nextButton.SetActive(false);

        if (classmate1Image == null)
        {
            DisplayLine(currentLineIndex);
            yield break;
        }

        classmate1Image.gameObject.SetActive(true);

        RectTransform imageTransform = classmate1Image.rectTransform;
        RectTransform parentTransform = imageTransform.parent as RectTransform;
        if (parentTransform == null)
        {
            DisplayLine(currentLineIndex);
            yield break;
        }

        classmateRightPosition = imageTransform.anchoredPosition;
        float startX = -parentTransform.rect.width * 0.5f -
            imageTransform.rect.width * Mathf.Abs(imageTransform.localScale.x);
        float leftPositionX = -parentTransform.rect.width * 0.25f;
        Vector2 startPosition = new Vector2(startX, classmateRightPosition.y);
        Vector2 leftPosition = new Vector2(leftPositionX, classmateRightPosition.y);
        imageTransform.anchoredPosition = startPosition;

        yield return MoveClassmate1(imageTransform, startPosition, leftPosition);
        DisplayLine(currentLineIndex);
    }

    private IEnumerator MoveClassmate1(RectTransform imageTransform, Vector2 startPosition, Vector2 targetPosition)
    {
        float duration = Mathf.Max(classmateMoveDuration, 0.01f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            imageTransform.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        imageTransform.anchoredPosition = targetPosition;
    }

    public void NextButton()
    {
        if (isTyping)
        {
            CompleteTypingInstantly();
            return;
        }

        if (waitingForPulubiEntrance)
        {
            waitingForPulubiEntrance = false;
            StartCoroutine(PlayPulubiEntrance());
            return;
        }

        if (waitingForFadeOut)
        {
            waitingForFadeOut = false;
            StartCoroutine(FadeOutScene());
            return;
        }

        if (isPlayingChoiceDialogue)
        {
            if (choiceDialogueIndex + 1 >= choiceDialogue.Count)
                return;

            choiceDialogueIndex++;
            DisplayChoiceLine();
            return;
        }

        if (currentLineIndex + 1 >= dialogueList.Count)
        {
            EndDialogue();
            return;
        }

        currentLineIndex++;
        if (currentLineIndex == 1)
            StartCoroutine(BringClassmate1In());
        else
            DisplayLine(currentLineIndex);
    }

    private void DisplayLine(int index)
    {
        if (nextButton != null) nextButton.SetActive(true);
        if (choiceContainer != null) choiceContainer.SetActive(false);

        A1SchoolDialogueLine line = dialogueList[index];

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

        if (pulubiDialoguePlaying)
        {
            pulubiDialoguePlaying = false;
            waitingForFadeOut = true;
            if (nextButton != null) nextButton.SetActive(true);
            return;
        }

        if (waitingForPulubiEntrance)
            return;

        if (isPlayingChoiceDialogue)
        {
            if (choiceDialogueIndex == choiceDialogue.Count - 1)
            {
                isPlayingChoiceDialogue = false;
                StartCoroutine(SendClassmateToClass());
            }
            return;
        }

        if (currentLineIndex == dialogueList.Count - 1)
        {
            StartCoroutine(FinishConversation());
        }
    }

    private IEnumerator FinishConversation()
    {
        if (nextButton != null) nextButton.SetActive(false);

        if (classmate1Image != null && classmate1Image.gameObject.activeSelf)
        {
            RectTransform imageTransform = classmate1Image.rectTransform;
            yield return MoveClassmate1(imageTransform, imageTransform.anchoredPosition, classmateRightPosition);
        }

        ShowSchoolChoice();
    }

    private IEnumerator PlayPulubiEntrance()
    {
        if (nextButton != null) nextButton.SetActive(false);
        if (dialogueBox != null) dialogueBox.SetActive(false);

        if (pulubiImage == null)
        {
            DisplayPulubiDialogue();
            yield break;
        }

        pulubiImage.gameObject.SetActive(true);

        RectTransform imageTransform = pulubiImage.rectTransform;
        RectTransform parentTransform = imageTransform.parent as RectTransform;
        if (parentTransform == null)
        {
            DisplayPulubiDialogue();
            yield break;
        }

        float originalY = parentTransform.InverseTransformPoint(imageTransform.position).y;
        imageTransform.anchorMin = new Vector2(0.5f, 0.5f);
        imageTransform.anchorMax = new Vector2(0.5f, 0.5f);
        imageTransform.pivot = new Vector2(0.5f, 0.5f);

        float halfParentWidth = parentTransform.rect.width * 0.5f;
        float halfImageWidth = imageTransform.rect.width * Mathf.Abs(imageTransform.localScale.x) * 0.5f;
        float startX = -halfParentWidth - halfImageWidth;
        float endX = halfParentWidth - halfImageWidth;
        float duration = Mathf.Max(pulubiMoveDuration, 0.01f);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            imageTransform.anchoredPosition = new Vector2(Mathf.Lerp(startX, endX, progress), originalY);
            yield return null;
        }

        imageTransform.anchoredPosition = new Vector2(endX, originalY);
        DisplayPulubiDialogue();
    }

    private void DisplaySceneLine(string speaker, string message, bool showNextButton)
    {
        if (dialogueBox != null) dialogueBox.SetActive(true);
        if (nextButton != null) nextButton.SetActive(showNextButton);
        if (choiceContainer != null) choiceContainer.SetActive(false);

        if (charNameText != null)
            charNameText.text = speaker;

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        typingCoroutine = StartCoroutine(TypeText(message));
    }

    private void DisplayPulubiDialogue()
    {
        pulubiDialoguePlaying = true;
        DisplaySceneLine("???", "So... Hungry..", false);
    }

    private IEnumerator FadeOutScene()
    {
        if (nextButton != null) nextButton.SetActive(false);
        if (choiceContainer != null) choiceContainer.SetActive(false);

        if (fadeOutScreen == null)
            yield break;

        fadeOutScreen.SetActive(true);
        CanvasGroup canvasGroup = fadeOutScreen.GetComponent<CanvasGroup>();
        Image fadeImage = fadeOutScreen.GetComponent<Image>();

        if (fadeImage != null)
            fadeImage.color = Color.black;

        if (canvasGroup == null && fadeImage == null)
            canvasGroup = fadeOutScreen.AddComponent<CanvasGroup>();

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        else
        {
            Color color = fadeImage.color;
            color.a = 0f;
            fadeImage.color = color;
        }

        const float duration = 1f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / duration);
            if (canvasGroup != null)
                canvasGroup.alpha = alpha;
            else
            {
                Color color = fadeImage.color;
                color.a = alpha;
                fadeImage.color = color;
            }
            yield return null;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
    }

    private void ShowSchoolChoice()
    {
        if (nextButton != null) nextButton.SetActive(false);
        if (choiceContainer != null) choiceContainer.SetActive(true);

        if (choiceButton1 != null)
        {
            choiceButton1.gameObject.SetActive(true);
            if (choiceButton1Text != null) choiceButton1Text.text = "Cut the first subject";
            choiceButton1.onClick.RemoveAllListeners();
            choiceButton1.onClick.AddListener(() => StartChoiceDialogue(
                new A1SchoolDialogueLine("You", "Let's go!"),
                new A1SchoolDialogueLine("Elijah", "You really want to, huh?"),
                new A1SchoolDialogueLine("Elijah", "I'm just joking! HAHAHA tara na!")));
        }

        if (choiceButton2 != null)
        {
            choiceButton2.gameObject.SetActive(true);
            TMP_Text secondChoiceText = choiceButton2Text != null
                ? choiceButton2Text
                : choiceButton2.GetComponentInChildren<TMP_Text>();
            if (secondChoiceText != null) secondChoiceText.text = "No, let's go to class";
            choiceButton2.onClick.RemoveAllListeners();
            choiceButton2.onClick.AddListener(() => StartChoiceDialogue(
                new A1SchoolDialogueLine("You", "No, I don't want to."),
                new A1SchoolDialogueLine("Elijah", "I'm just joking! HAHAHA tara na!")));
        }
    }

    private void StartChoiceDialogue(params A1SchoolDialogueLine[] lines)
    {
        if (choiceContainer != null) choiceContainer.SetActive(false);
        if (dialogueBox != null) dialogueBox.SetActive(true);

        choiceDialogue.Clear();
        choiceDialogue.AddRange(lines);
        choiceDialogueIndex = 0;
        isPlayingChoiceDialogue = true;
        DisplayChoiceLine();
    }

    private void DisplayChoiceLine()
    {
        if (nextButton != null) nextButton.SetActive(true);

        A1SchoolDialogueLine line = choiceDialogue[choiceDialogueIndex];
        if (charNameText != null)
            charNameText.text = line.speakerName;

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        typingCoroutine = StartCoroutine(TypeText(line.message));
    }

    private IEnumerator SendClassmateToClass()
    {
        if (nextButton != null) nextButton.SetActive(false);

        if (classmate1Image != null && classmate1Image.gameObject.activeSelf)
        {
            RectTransform imageTransform = classmate1Image.rectTransform;
            RectTransform parentTransform = imageTransform.parent as RectTransform;
            if (parentTransform != null)
            {
                Vector2 startPosition = imageTransform.anchoredPosition;
                float endX = parentTransform.rect.width * 0.5f +
                    imageTransform.rect.width * Mathf.Abs(imageTransform.localScale.x);
                Vector2 endPosition = new Vector2(endX, startPosition.y);
                yield return MoveClassmate1(imageTransform, startPosition, endPosition);
            }
        }

        EndDialogue();
        waitingForPulubiEntrance = true;
        DisplaySceneLine("You", "...", true);
    }

    private void EndDialogue()
    {
        if (nextButton != null) nextButton.SetActive(false);
        if (choiceContainer != null) choiceContainer.SetActive(false);
    }
}
