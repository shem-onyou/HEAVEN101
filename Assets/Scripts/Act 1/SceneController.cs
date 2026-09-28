using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement; // Required for loading scenes
using TMPro;

[System.Serializable]
public class ChoiceOption
{
    public string choiceText;
    public int nextDialogueIndex;
}

[System.Serializable]
public class DialogueLine
{
    public string speakerName;
    [TextArea(2, 5)] public string message;

    [Header("Player Choice Options (Optional)")]
    public List<ChoiceOption> choices;

    public DialogueLine(string name, string msg, List<ChoiceOption> choiceOptions = null)
    {
        speakerName = name;
        message = msg;
        choices = choiceOptions;
    }
}

public class SceneController : MonoBehaviour
{
    [Header("Cutscene Objects")]
    public GameObject fadeScreenIn;
    public GameObject fadeOutScreen; // <--- Drag your FadeOut GameObject here
    public GameObject charLord;
    public GameObject dialogueBox;
    public GameObject nextButton;

    [Header("Choice System Objects")]
    public GameObject choiceContainer;
    public Button choiceButton1;
    public Button choiceButton2;
    public TMP_Text choiceButton1Text;
    public TMP_Text choiceButton2Text;

    [Header("Text UI References")]
    public TMP_Text charNameText;
    public TMP_Text messageInputText;

    [Header("Typewriter Speed Settings")]
    [SerializeField] private float typingSpeed = 0.03f;

    [Header("Dialogue Sequence")]
    public List<DialogueLine> dialogueList = new List<DialogueLine>();

    private int currentLineIndex = 0;
    private Coroutine typingCoroutine;
    private bool isTyping = false;

    void Start()
    {
        if (dialogueList.Count == 0)
        {
            dialogueList.Add(new DialogueLine("You", "*sigh* Where am I?")); // 0
            dialogueList.Add(new DialogueLine("You", "*shocked* Who... no, What are you?")); // 1
            dialogueList.Add(new DialogueLine("God", "I am the Creator of all things, I am the only one TRUE GOD.")); // 2
            dialogueList.Add(new DialogueLine("You", "God!?...")); // 3
            dialogueList.Add(new DialogueLine("You", "Am I Dead? Am I in Heaven? Ho... How did I die? This is just a dream right?")); // 4
            dialogueList.Add(new DialogueLine("God", "Easy now, child. Let me explain.")); // 5
            dialogueList.Add(new DialogueLine("God", "Yes, you are mortally dead or sleep. you are now a spiritual being outside the gates of Heaven.")); // 6
            dialogueList.Add(new DialogueLine("God", "This is the the day that all of you will face me, This is the Judgement Day. But now, this is just a vision in your dreams.")); // 7
            dialogueList.Add(new DialogueLine("You", "*shocked*")); // 8
            dialogueList.Add(new DialogueLine("God", "Tell me child.")); // 9

            // Choice Question (Index 10)
            List<ChoiceOption> questionChoices = new List<ChoiceOption>()
            {
                new ChoiceOption { choiceText = "...", nextDialogueIndex = 11 },
                new ChoiceOption { choiceText = "I'm not bad.", nextDialogueIndex = 16 }
            };
            dialogueList.Add(new DialogueLine("God", "Why should I let you enter my kingdom?", questionChoices)); // 10

            // Path A: If Player chooses "..."
            dialogueList.Add(new DialogueLine("You", "I.... I don't know God.")); // 11
            dialogueList.Add(new DialogueLine("God", "You don't know.")); // 12
            dialogueList.Add(new DialogueLine("God", "Very well, I will give the chance and let you find the answer to my question.")); // 13
            dialogueList.Add(new DialogueLine("God", "I must warn you, the day of Judgement is near, which I, then only God will judge you fair on what you have done.")); // 14
            dialogueList.Add(new DialogueLine("God", "GO, WAKE UP AND SEEK THE ANSWER.")); // 15

            // Path B: If Player chooses "I'm not bad."
            dialogueList.Add(new DialogueLine("You", "Because, I am good with my parents, friends, I help them and I follow them. I did not do anything bad.")); // 16
            dialogueList.Add(new DialogueLine("God", "I see.")); // 17
            dialogueList.Add(new DialogueLine("God", "But that is not the answer.")); // 18
            dialogueList.Add(new DialogueLine("God", "I am giving you a chance.")); // 19
            dialogueList.Add(new DialogueLine("God", "This vision is a warning to you, the day of Judgement is near, which I, the only God will judge you on what you have done.")); // 20
            dialogueList.Add(new DialogueLine("God", "GO, WAKE UP AND SEEK THE ANSWER.")); // 21
        }

        if (choiceContainer != null)
            choiceContainer.SetActive(false);

        if (fadeOutScreen != null)
            fadeOutScreen.SetActive(false); // Ensure fadeout panel starts disabled

        StartCoroutine(StartCutscene());
    }

    IEnumerator StartCutscene()
    {
        yield return new WaitForSeconds(3f);
        if (fadeScreenIn != null) fadeScreenIn.SetActive(false);
        if (charLord != null) charLord.SetActive(true);

        yield return new WaitForSeconds(2f);
        if (dialogueBox != null) dialogueBox.SetActive(true);

        yield return new WaitForSeconds(1f);

        if (dialogueList.Count > 0)
        {
            DisplayLine(currentLineIndex);
        }
    }

    public void NextButton()
    {
        if (isTyping)
        {
            CompleteTypingInstantly();
            return;
        }

        currentLineIndex++;

        if (currentLineIndex == 16 || currentLineIndex >= dialogueList.Count)
        {
            EndDialogue();
            return;
        }

        DisplayLine(currentLineIndex);
    }

    private void DisplayLine(int index)
    {
        if (nextButton != null) nextButton.SetActive(true);
        if (choiceContainer != null) choiceContainer.SetActive(false);

        DialogueLine line = dialogueList[index];

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

        DialogueLine line = dialogueList[currentLineIndex];
        if (line.choices != null && line.choices.Count > 0)
        {
            ShowChoices(line.choices);
        }
    }

    private void ShowChoices(List<ChoiceOption> choices)
    {
        if (nextButton != null) nextButton.SetActive(false);
        if (choiceContainer != null) choiceContainer.SetActive(true);

        if (choices.Count > 0 && choiceButton1 != null)
        {
            if (choiceButton1Text != null) choiceButton1Text.text = choices[0].choiceText;
            choiceButton1.onClick.RemoveAllListeners();
            int index0 = choices[0].nextDialogueIndex;
            choiceButton1.onClick.AddListener(() => MakeChoice(index0));
        }

        if (choices.Count > 1 && choiceButton2 != null)
        {
            if (choiceButton2Text != null) choiceButton2Text.text = choices[1].choiceText;
            choiceButton2.onClick.RemoveAllListeners();
            int index1 = choices[1].nextDialogueIndex;
            choiceButton2.onClick.AddListener(() => MakeChoice(index1));
        }
    }

    public void MakeChoice(int targetIndex)
    {
        if (choiceContainer != null) choiceContainer.SetActive(false);
        currentLineIndex = targetIndex;
        DisplayLine(currentLineIndex);
    }

    private void EndDialogue()
    {
        StartCoroutine(FadeOutSequence());
    }

    private IEnumerator FadeOutSequence()
    {
        // 1. Hide dialogue UI elements
        if (nextButton != null) nextButton.SetActive(false);
        if (choiceContainer != null) choiceContainer.SetActive(false);
        if (dialogueBox != null) dialogueBox.SetActive(false);

        // 2. Show the black screen immediately
        if (fadeOutScreen != null)
        {
            fadeOutScreen.SetActive(true);

            CanvasGroup canvasGroup = fadeOutScreen.GetComponent<CanvasGroup>();
            Image fadeImage = fadeOutScreen.GetComponent<Image>();

            if (canvasGroup == null && fadeImage == null)
            {
                canvasGroup = fadeOutScreen.AddComponent<CanvasGroup>();
            }

            if (fadeImage != null)
            {
                fadeImage.color = Color.black;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
            else if (fadeImage != null)
            {
                Color c = fadeImage.color;
                c.a = 1f;
                fadeImage.color = c;
            }
        }

        yield return new WaitForSeconds(0.5f); // Pause briefly while full black

        // 3. Change to the A1_Bedroom scene
        SceneManager.LoadScene("A1_Bedroom");
    }
}