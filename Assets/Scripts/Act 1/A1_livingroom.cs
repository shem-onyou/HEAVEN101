using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using TMPro;

[System.Serializable]
public class A1LivingRoomChoiceOption
{
    public string choiceText;
    public int nextDialogueIndex;
}

[System.Serializable]
public class A1LivingRoomDialogueLine
{
    public string speakerName;
    [TextArea(2, 5)] public string message;
    public List<A1LivingRoomChoiceOption> choices = new List<A1LivingRoomChoiceOption>();

    public A1LivingRoomDialogueLine(string name, string msg, List<A1LivingRoomChoiceOption> choiceOptions = null)
    {
        speakerName = name;
        message = msg;
        choices = choiceOptions ?? new List<A1LivingRoomChoiceOption>();
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
    public Button choiceButton2;
    public TMP_Text choiceButton2Text;

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
    public TMP_Text kalatProgressText;

    private int currentLineIndex = 0;
    private Coroutine typingCoroutine;
    private bool isTyping = false;
    private Vector3 nanayOriginalPosition;
    private Vector3 nanayOffscreenPosition;
    private ClickableUIItem[] clickableKalatItems;
    private readonly HashSet<ClickableUIItem> cleanedKalatItems = new HashSet<ClickableUIItem>();
    private bool choresStarted;
    private bool completionDialogueActive;
    private bool isSceneTransitionQueued;

    private void Awake()
    {
        clickableKalatItems = FindObjectsByType<ClickableUIItem>(FindObjectsInactive.Include);
        SetKalatItemsClickable(false);
        EnsureKalatProgressText();

        if (kalatProgressText != null)
        {
            kalatProgressText.gameObject.SetActive(false);
            UpdateKalatProgress();
        }
    }

    private void EnsureKalatProgressText()
    {
        if (kalatProgressText != null)
            return;

        GameObject progressObject = GameObject.Find("Kalat_progress");
        if (progressObject != null)
        {
            kalatProgressText = progressObject.GetComponent<TMP_Text>();
            return;
        }

        TMP_Text[] allTexts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        foreach (TMP_Text text in allTexts)
        {
            if (text != null && text.name == "Kalat_progress")
            {
                kalatProgressText = text;
                return;
            }
        }
    }

    void Start()
    {
        if (string.IsNullOrEmpty(nextSceneName))
            nextSceneName = "A1_School";

        if (dialogueList.Count == 0)
        {
            dialogueList.Add(new A1LivingRoomDialogueLine("You", "Huh... what time is it?"));
            dialogueList.Add(new A1LivingRoomDialogueLine("Nanay", "Anak, it's already late! You're going to miss school again."));
            dialogueList.Add(new A1LivingRoomDialogueLine("You", "Oh no! I overslept."));
            dialogueList.Add(new A1LivingRoomDialogueLine("Nanay", "Yes, and before you leave, help me first with the chores."));
            dialogueList.Add(new A1LivingRoomDialogueLine("You", "What chores, Nanay?"));
            dialogueList.Add(new A1LivingRoomDialogueLine("Nanay", "Please wash the dishes and sweep the living room.", new List<A1LivingRoomChoiceOption>
            {
                new A1LivingRoomChoiceOption { choiceText = "Do it", nextDialogueIndex = 8 },
                new A1LivingRoomChoiceOption { choiceText = "But I'm late", nextDialogueIndex = 6 }
            }));
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
            if (completionDialogueActive && currentLineIndex == dialogueList.Count - 1)
            {
                return;
            }

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

        if (HasLivingRoomChoices(currentLineIndex))
            ShowLivingRoomChoice();
        else if (currentLineIndex == dialogueList.Count - 1)
            EndLivingRoomDialogue();
    }

    private void CompleteLivingRoomTypingInstantly()
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        messageInputText.maxVisibleCharacters = messageInputText.textInfo.characterCount;
        isTyping = false;

        if (HasLivingRoomChoices(currentLineIndex))
            ShowLivingRoomChoice();
        else if (currentLineIndex == dialogueList.Count - 1)
            EndLivingRoomDialogue();
    }

    private bool HasLivingRoomChoices(int index)
    {
        return dialogueList[index].choices != null && dialogueList[index].choices.Count > 0;
    }

    private void ShowLivingRoomChoice()
    {
        if (nextButton != null) nextButton.SetActive(false);
        if (choiceContainer != null) choiceContainer.SetActive(true);

        if (choiceButton != null)
        {
            choiceButton.gameObject.SetActive(true);
            choiceButton.onClick.RemoveAllListeners();
        }

        if (choiceButton2 != null)
        {
            choiceButton2.gameObject.SetActive(false);
            choiceButton2.onClick.RemoveAllListeners();
        }

        List<A1LivingRoomChoiceOption> choices = dialogueList[currentLineIndex].choices;
        if (choices != null && choices.Count > 0)
        {
            if (choiceButton != null)
            {
                if (choiceButtonText != null) choiceButtonText.text = choices[0].choiceText;
                int firstTargetIndex = choices[0].nextDialogueIndex;
                choiceButton.onClick.AddListener(() => MakeLivingRoomChoice(firstTargetIndex));
            }

            if (choices.Count > 1 && choiceButton2 != null)
            {
                choiceButton2.gameObject.SetActive(true);
                if (choiceButton2Text != null) choiceButton2Text.text = choices[1].choiceText;
                int secondTargetIndex = choices[1].nextDialogueIndex;
                choiceButton2.onClick.AddListener(() => MakeLivingRoomChoice(secondTargetIndex));
            }

            return;
        }

    }

    private void MakeLivingRoomChoice(int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= dialogueList.Count)
            return;

        if (choiceContainer != null) choiceContainer.SetActive(false);
        currentLineIndex = targetIndex;
        DisplayLivingRoomLine(currentLineIndex);
    }

    private void EndLivingRoomDialogue()
    {
        if (nextButton != null) nextButton.SetActive(false);
        if (choiceContainer != null) choiceContainer.SetActive(false);
        if (dialogueBox != null) dialogueBox.SetActive(false);
        if (nanayCharacter != null) nanayCharacter.SetActive(false);

        if (!choresStarted)
        {
            choresStarted = true;
            EnsureKalatProgressText();
            UpdateKalatProgress();
            if (kalatProgressText != null)
            {
                kalatProgressText.gameObject.SetActive(true);
                kalatProgressText.enabled = true;
            }
            SetKalatItemsClickable(true);
            return;
        }

        if (completionDialogueActive && !isSceneTransitionQueued)
        {
            isSceneTransitionQueued = true;
            StartCoroutine(LoadNextSceneAfterDelay());
        }
    }

    private IEnumerator LoadNextSceneAfterDelay()
    {
        yield return new WaitForSeconds(1.5f);
        SceneManager.LoadScene(nextSceneName);
    }

    public void RegisterKalatCleaned(ClickableUIItem item)
    {
        if (!choresStarted || item == null || !cleanedKalatItems.Add(item))
            return;

        UpdateKalatProgress();

        if (cleanedKalatItems.Count >= clickableKalatItems.Length)
            ShowCompletionDialogue();
    }

    private void UpdateKalatProgress()
    {
        if (kalatProgressText != null)
            kalatProgressText.text = $"Kalat: {cleanedKalatItems.Count} / {clickableKalatItems.Length}";
    }

    private void ShowCompletionDialogue()
    {
        if (kalatProgressText != null) kalatProgressText.gameObject.SetActive(false);

        completionDialogueActive = true;

        dialogueList.Add(new A1LivingRoomDialogueLine("You", "I'm finished, Nanay. I'm going to school now."));
        dialogueList.Add(new A1LivingRoomDialogueLine("Nanay", "All right, anak. Be careful on your way to school!"));
        currentLineIndex = dialogueList.Count - 2;

        if (dialogueBox != null) dialogueBox.SetActive(true);
        DisplayLivingRoomLine(currentLineIndex);
    }

    private void SetKalatItemsClickable(bool clickable)
    {
        foreach (ClickableUIItem item in clickableKalatItems)
            item.enabled = clickable;
    }
}
