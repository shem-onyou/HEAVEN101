using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using TMPro;

[System.Serializable]
public class A1LivingRoomDialogueLine
{
    public string speakerName;
    [TextArea(2, 5)] public string message;

    public A1LivingRoomDialogueLine(string name, string msg)
    {
        speakerName = name;
        message = msg;
    }
}

public class A1_livingroom : MonoBehaviour
{
    [Header("Cutscene Objects")]
    [FormerlySerializedAs("livingRoomFadeScreenIn")]
    public GameObject fadeScreenIn;
    [FormerlySerializedAs("livingRoomFadeOutScreen")]
    public GameObject fadeOutScreen;
    public GameObject nanayCharacter;
    [FormerlySerializedAs("livingRoomDialogueBox")]
    public GameObject dialogueBox;
    [FormerlySerializedAs("livingRoomNextButton")]
    public GameObject nextButton;

    [Header("Choice Objects")]
    [FormerlySerializedAs("livingRoomChoiceContainer")]
    public GameObject choiceContainer;
    [FormerlySerializedAs("livingRoomChoiceButton")]
    public Button choiceButton;
    [FormerlySerializedAs("livingRoomChoiceButtonText")]
    public TMP_Text choiceButtonText;

    [Header("Text UI References")]
    [FormerlySerializedAs("livingRoomCharacterNameText")]
    public TMP_Text charNameText;
    [FormerlySerializedAs("livingRoomMessageText")]
    public TMP_Text messageInputText;

    [Header("Typewriter Speed Settings")]
    [FormerlySerializedAs("livingRoomTypingSpeed")]
    [SerializeField] private float typingSpeed = 0.03f;

    [Header("Dialogue Sequence")]
    [FormerlySerializedAs("livingRoomDialogueList")]
    public List<A1LivingRoomDialogueLine> dialogueList = new List<A1LivingRoomDialogueLine>();
    [FormerlySerializedAs("livingRoomNextSceneName")]
    [SerializeField] private string nextSceneName;

    private int currentLineIndex = 0;
    private Coroutine typingCoroutine;
    private bool isTyping = false;
    private Vector3 nanayOriginalPosition;
    private Vector3 nanayOffscreenPosition;

    void Start()
    {
        if (dialogueList.Count == 0)
        {
            dialogueList.Add(new A1LivingRoomDialogueLine("You", "Huh... what time is it?"));
            dialogueList.Add(new A1LivingRoomDialogueLine("Nanay", "Anak, it's already late! You're going to miss school again."));
            dialogueList.Add(new A1LivingRoomDialogueLine("You", "Oh no! I overslept."));
            dialogueList.Add(new A1LivingRoomDialogueLine("Nanay", "Yes, and before you leave, help me first with the chores."));
            dialogueList.Add(new A1LivingRoomDialogueLine("You", "What chores, Nanay?"));
            dialogueList.Add(new A1LivingRoomDialogueLine("Nanay", "Please wash the dishes and sweep the living room."));
            dialogueList.Add(new A1LivingRoomDialogueLine("You", "But I’m already late for school!"));
            dialogueList.Add(new A1LivingRoomDialogueLine("Nanay", "I know, anak. Just do it fast, then you can go."));
            dialogueList.Add(new A1LivingRoomDialogueLine("You", "Okay, Nanay. I'll help you first."));
            dialogueList.Add(new A1LivingRoomDialogueLine("Nanay", "Thank you, anak. Hurry up, okay?"));
        }

        if (choiceContainer != null)
            choiceContainer.SetActive(false);

        if (dialogueBox != null)
            dialogueBox.SetActive(false);

        if (nanayCharacter != null)
        {
            nanayOriginalPosition = nanayCharacter.transform.position;
            nanayOffscreenPosition = new Vector3(nanayOriginalPosition.x + 8f, nanayOriginalPosition.y, nanayOriginalPosition.z);
            nanayCharacter.transform.position = nanayOffscreenPosition;
            nanayCharacter.SetActive(false);
        }

        if (nextButton != null)
            nextButton.SetActive(false);

        if (fadeOutScreen != null)
            fadeOutScreen.SetActive(false);

        if (nextButton != null)
        {
            Button nextDialogueButton = nextButton.GetComponent<Button>();
            if (nextDialogueButton != null)
            {
                nextDialogueButton.onClick.RemoveAllListeners();
                nextDialogueButton.onClick.AddListener(NextLivingRoomButton);
            }
        }

        if (choiceButton != null)
        {
            choiceButton.onClick.RemoveAllListeners();
            choiceButton.onClick.AddListener(EndLivingRoomDialogue);
        }

        StartCoroutine(StartLivingRoomCutscene());
    }

    private IEnumerator StartLivingRoomCutscene()
    {
        yield return new WaitForSeconds(3f);
        if (fadeScreenIn != null) fadeScreenIn.SetActive(false);

        yield return new WaitForSeconds(2f);
        if (dialogueBox != null) dialogueBox.SetActive(true);

        if (dialogueList.Count > 0)
            DisplayLivingRoomLine(currentLineIndex);

        yield return new WaitForSeconds(1f);

        if (nanayCharacter != null)
        {
            nanayCharacter.SetActive(true);
            StartCoroutine(EnterNanayFromRight());
        }
    }

    private IEnumerator EnterNanayFromRight()
    {
        if (nanayCharacter == null)
            yield break;

        Vector3 startPosition = nanayOffscreenPosition;
        Vector3 targetPosition = nanayOriginalPosition;
        float duration = 1.2f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            nanayCharacter.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        nanayCharacter.transform.position = targetPosition;
    }

    public void NextLivingRoomButton()
    {
        if (isTyping)
        {
            CompleteLivingRoomTypingInstantly();
            return;
        }

        if (currentLineIndex + 1 >= dialogueList.Count)
        {
            EndLivingRoomDialogue();
            return;
        }

        currentLineIndex++;
        DisplayLivingRoomLine(currentLineIndex);
    }

    private void DisplayLivingRoomLine(int index)
    {
        if (nextButton != null) nextButton.SetActive(true);
        if (choiceContainer != null) choiceContainer.SetActive(false);

        A1LivingRoomDialogueLine line = dialogueList[index];

        if (charNameText != null)
            charNameText.text = line.speakerName;

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        typingCoroutine = StartCoroutine(TypeLivingRoomText(line.message));
    }

    private IEnumerator TypeLivingRoomText(string targetMessage)
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

        if (currentLineIndex == dialogueList.Count - 1)
            ShowLivingRoomChoice();
    }

    private void CompleteLivingRoomTypingInstantly()
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        messageInputText.maxVisibleCharacters = messageInputText.textInfo.characterCount;
        isTyping = false;

        if (currentLineIndex == dialogueList.Count - 1)
            ShowLivingRoomChoice();
    }

    private void ShowLivingRoomChoice()
    {
        if (nextButton != null) nextButton.SetActive(false);
        if (choiceContainer != null) choiceContainer.SetActive(true);
        if (choiceButton != null) choiceButton.gameObject.SetActive(true);
        if (choiceButtonText != null) choiceButtonText.text = "Continue";
    }

    private void EndLivingRoomDialogue()
    {
        StartCoroutine(FadeOutLivingRoomSequence());
    }

    private IEnumerator FadeOutLivingRoomSequence()
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
                canvasGroup = fadeOutScreen.AddComponent<CanvasGroup>();

            if (fadeImage != null)
                fadeImage.color = Color.black;

            if (canvasGroup != null)
                canvasGroup.alpha = 1f;
            else if (fadeImage != null)
            {
                Color color = fadeImage.color;
                color.a = 1f;
                fadeImage.color = color;
            }
        }

        yield return new WaitForSeconds(0.5f);

        if (!string.IsNullOrEmpty(nextSceneName))
            SceneManager.LoadScene(nextSceneName);
    }
}
