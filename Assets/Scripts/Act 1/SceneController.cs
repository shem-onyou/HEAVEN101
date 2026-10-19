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
    [SerializeField] private AudioSource teleportationAudioSource;

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
            dialogueList.Add(new DialogueLine("You", "My head... Where am I? The last thing I remember is...")); // 0
            dialogueList.Add(new DialogueLine("You", "You... Who are you? No, what are you?")); // 1
            dialogueList.Add(new DialogueLine("God", "I am the One who spoke the stars into being and breathed life into every soul. You know me as God.")); // 2
            dialogueList.Add(new DialogueLine("You", "God? Then... is this Heaven?")); // 3
            dialogueList.Add(new DialogueLine("You", "Wait... I was somewhere else, wasn't I? Why can't I remember how I got here? What happened to me?")); // 4
            dialogueList.Add(new DialogueLine("God", "Take a breath, child. Your body still lives, but you have been brought here for a reason.")); // 5
            dialogueList.Add(new DialogueLine("God", "Your body sleeps. Your spirit has crossed beyond it, and now you stand outside the gates.")); // 6
            dialogueList.Add(new DialogueLine("God", "This is a vision of the Day of Judgment. One day, every soul will answer for the life it lived.")); // 7
            dialogueList.Add(new DialogueLine("You", "Every soul...? Including me?")); // 8
            dialogueList.Add(new DialogueLine("God", "Especially you.")); // 9

            // Choice Question (Index 10)
            List<ChoiceOption> questionChoices = new List<ChoiceOption>()
            {
                new ChoiceOption { choiceText = "I don't know...", nextDialogueIndex = 11 },
                new ChoiceOption { choiceText = "Cause I'm good.", nextDialogueIndex = 17 }
            };
            dialogueList.Add(new DialogueLine("God", "Tell me, child. Why should I open the gates to you?", questionChoices)); // 10

            // Path A: If Player chooses "I don't know..."
            dialogueList.Add(new DialogueLine("You", "I... I don't know.")); // 11
            dialogueList.Add(new DialogueLine("God", "You don't know? Then how have you lived your life?")); // 12
            dialogueList.Add(new DialogueLine("You", "I just take each day as it comes. I try to help people and treat them well.")); // 13
            dialogueList.Add(new DialogueLine("God", "I see, but That is not yet the answer I seek. You skipped something important.")); // 14
            dialogueList.Add(new DialogueLine("God", "Judgment is near. Find the answer before that day. When it comes, no more time will be given.")); // 15
            dialogueList.Add(new DialogueLine("God", "WAKE UP! And live your remaining life wisely.")); // 16

            // Path B: If Player chooses "Cause I'm good."
            dialogueList.Add(new DialogueLine("You", "I tried to be good. I listened to my parents, helped my friends... I never tried to hurt anyone.")); // 17
            dialogueList.Add(new DialogueLine("God", "You believe goodness is a list of things you have done?")); // 18
            dialogueList.Add(new DialogueLine("God", "You skipped the most important part.")); // 19
            dialogueList.Add(new DialogueLine("God", "I will give you chance to find the answer.")); // 20
            dialogueList.Add(new DialogueLine("God", "What you call a dream is a warning. The Day of Judgment is coming, and every choice you make will matter.")); // 21
            dialogueList.Add(new DialogueLine("God", "WAKE UP! Seek the answer.")); // 22
        }

        if (teleportationAudioSource == null)
        {
            GameObject teleportationObject = GameObject.Find("Teleportation");
            if (teleportationObject != null)
                teleportationAudioSource = teleportationObject.GetComponent<AudioSource>();
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

        if (currentLineIndex == 17 || currentLineIndex >= dialogueList.Count)
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
        if (teleportationAudioSource != null)
        {
            teleportationAudioSource.Play();
            while (teleportationAudioSource.isPlaying)
                yield return null;
        }

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