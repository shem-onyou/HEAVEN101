using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

[System.Serializable]
public class A1BedroomChoiceOption
{
    public string choiceText;
    public int nextDialogueIndex;
}

[System.Serializable]
public class A1BedroomDialogueLine
{
    public string speakerName;
    [TextArea(2, 5)] public string message;

    [Header("Player Choice Options (Optional)")]
    public List<A1BedroomChoiceOption> choices;

    public A1BedroomDialogueLine(string name, string msg, List<A1BedroomChoiceOption> choiceOptions = null)
    {
        speakerName = name;
        message = msg;
        choices = choiceOptions;
    }
}

public class A1_bedroom_control : MonoBehaviour
{
    [Header("Cutscene Objects")]
    public GameObject fadeScreenIn;
    public GameObject fadeOutScreen;
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
    public List<A1BedroomDialogueLine> dialogueList = new List<A1BedroomDialogueLine>();

    private int currentLineIndex = 0;
    private Coroutine typingCoroutine;
    private bool isTyping = false;

    void Start()
    {
        if (dialogueList.Count == 0)
        {
            dialogueList.Add(new A1BedroomDialogueLine("You", "*sigh* Where am I?"));
            dialogueList.Add(new A1BedroomDialogueLine("You", "*shocked* Who... no, What are you?"));
            dialogueList.Add(new A1BedroomDialogueLine("You", "I am the Creator of all things, I am the only one TRUE GOD."));
            dialogueList.Add(new A1BedroomDialogueLine("You", "God!?..."));
            dialogueList.Add(new A1BedroomDialogueLine("You", "Am I Dead? Am I in Heaven? Ho... How did I die? This is just a dream right?"));
            dialogueList.Add(new A1BedroomDialogueLine("You", "Easy now, child. Let me explain."));
            dialogueList.Add(new A1BedroomDialogueLine("You", "Yes, you are mortally dead or sleep. you are now a spiritual being outside the gates of Heaven."));
            dialogueList.Add(new A1BedroomDialogueLine("You", "This is the the day that all of you will face me, This is the Judgement Day. But now, this is just a vision in your dreams."));
            dialogueList.Add(new A1BedroomDialogueLine("You", "*shocked*"));
            dialogueList.Add(new A1BedroomDialogueLine("You", "Tell me child."));

            List<A1BedroomChoiceOption> questionChoices = new List<A1BedroomChoiceOption>()
            {
                new A1BedroomChoiceOption { choiceText = "...", nextDialogueIndex = 11 },
                new A1BedroomChoiceOption { choiceText = "I'm not bad.", nextDialogueIndex = 16 }
            };
            dialogueList.Add(new A1BedroomDialogueLine("You", "Why should I let you enter my kingdom?", questionChoices));

            dialogueList.Add(new A1BedroomDialogueLine("You", "I.... I don't know God."));
            dialogueList.Add(new A1BedroomDialogueLine("You", "You don't know."));
            dialogueList.Add(new A1BedroomDialogueLine("You", "Very well, I will give the chance and let you find the answer to my question."));
            dialogueList.Add(new A1BedroomDialogueLine("You", "I must warn you, the day of Judgement is near, which I, then only God will judge you fair on what you have done."));
            dialogueList.Add(new A1BedroomDialogueLine("You", "GO, WAKE UP AND SEEK THE ANSWER."));

            dialogueList.Add(new A1BedroomDialogueLine("You", "Because, I am good with my parents, friends, I help them and I follow them. I did not do anything bad."));
            dialogueList.Add(new A1BedroomDialogueLine("You", "I see."));
            dialogueList.Add(new A1BedroomDialogueLine("You", "But that is not the answer."));
            dialogueList.Add(new A1BedroomDialogueLine("You", "I am giving you a chance."));
            dialogueList.Add(new A1BedroomDialogueLine("You", "This vision is a warning to you, the day of Judgement is near, which I, the only God will judge you on what you have done."));
        }

        if (choiceContainer != null)
            choiceContainer.SetActive(false);

        if (fadeOutScreen != null)
            fadeOutScreen.SetActive(false);

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

        A1BedroomDialogueLine line = dialogueList[index];

        if (charNameText != null)
            charNameText.text = "You";

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

        A1BedroomDialogueLine line = dialogueList[currentLineIndex];
        if (line.choices != null && line.choices.Count > 0)
        {
            ShowChoices(line.choices);
        }
    }

    private void ShowChoices(List<A1BedroomChoiceOption> choices)
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
        if (nextButton != null) nextButton.SetActive(false);
        if (choiceContainer != null) choiceContainer.SetActive(false);
        if (dialogueBox != null) dialogueBox.SetActive(false);

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

        yield return new WaitForSeconds(0.5f);
        SceneManager.LoadScene("A1_Bedroom");
    }
}
