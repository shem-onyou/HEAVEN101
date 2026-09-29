using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

[System.Serializable]
public class A1BedroomDialogueLine
{
    public string speakerName;
    [TextArea(2, 5)] public string message;

    public A1BedroomDialogueLine(string name, string msg)
    {
        speakerName = name;
        message = msg;
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
    public TMP_Text choiceButton1Text;
    [SerializeField, HideInInspector] private Button choiceButton2;

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
            dialogueList.Add(new A1BedroomDialogueLine("You", "Huhhh?"));
            dialogueList.Add(new A1BedroomDialogueLine("You", "I'm alive."));
            dialogueList.Add(new A1BedroomDialogueLine("You", "Did that really happen?"));
            dialogueList.Add(new A1BedroomDialogueLine("You", "God..."));
            dialogueList.Add(new A1BedroomDialogueLine("You", "How will I enter the gates of Heaven?"));
            dialogueList.Add(new A1BedroomDialogueLine("You", "Hmmmmm..."));
            dialogueList.Add(new A1BedroomDialogueLine("You", "Huh? Oh right!"));
            dialogueList.Add(new A1BedroomDialogueLine("You", "I gotta go to school, it's already late!"));
            dialogueList.Add(new A1BedroomDialogueLine("You", "*wearing clothes*"));
            dialogueList.Add(new A1BedroomDialogueLine("You", "All right, I'm set."));

            dialogueList.Add(new A1BedroomDialogueLine("You", "..."));
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

        if (currentLineIndex + 1 >= dialogueList.Count)
        {
            EndDialogue();
            return;
        }

        currentLineIndex++;
        DisplayLine(currentLineIndex);
    }

    private void DisplayLine(int index)
    {
        if (nextButton != null) nextButton.SetActive(true);
        if (choiceContainer != null) choiceContainer.SetActive(false);

        A1BedroomDialogueLine line = dialogueList[index];

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

        if (currentLineIndex == dialogueList.Count - 1)
        {
            ShowLivingRoomChoice();
        }
    }

    private void ShowLivingRoomChoice()
    {
        if (nextButton != null) nextButton.SetActive(false);
        if (choiceContainer != null) choiceContainer.SetActive(true);

        if (choiceButton2 != null)
        {
            choiceButton2.onClick.RemoveAllListeners();
            choiceButton2.gameObject.SetActive(false);
        }

        if (choiceButton1 != null)
        {
            choiceButton1.gameObject.SetActive(true);
            if (choiceButton1Text != null) choiceButton1Text.text = "Go to the living room.";
            choiceButton1.onClick.RemoveAllListeners();
            choiceButton1.onClick.AddListener(EndDialogue);
        }
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
        SceneManager.LoadScene("A1_LivingRoom");
    }
}
//ANG NEXT NA GAGAWIN AY SA LIVING ROOM, KUNG SAAN MAY MGA CHOICE NA PWEDE PILIIN NG PLAYER. AT MAKIKIPAG USAP SA NANAY 9/29/2026