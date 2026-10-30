using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Shows the player's record (choices, actions, good points) on top of the black fade-out screen,
// with a "Main Menu" and a "Next Act" button. Call Show() at the end of an act.
public static class ActResultsScreen
{
    public static void Show(Transform canvasTransform, GameObject choiceContainer, TMP_FontAsset font,
        string title, string mainMenuSceneName, string nextActSceneName)
    {
        RectTransform canvasRect = canvasTransform as RectTransform;
        if (canvasRect == null)
            return;

        CreateSummaryText(canvasRect, font, title);

        Button mainMenuButton;
        Button nextActButton;
        if (!TryUseChoiceButtons(canvasRect, choiceContainer, out mainMenuButton, out nextActButton))
            CreateButtons(canvasRect, font, out mainMenuButton, out nextActButton);

        SetupButton(mainMenuButton, "Main Menu", () => SceneManager.LoadScene(mainMenuSceneName));

        // Until the next act exists, "Next Act" goes back to the Acts selection screen.
        string nextActTarget = string.IsNullOrEmpty(nextActSceneName) ? "Acts" : nextActSceneName;
        SetupButton(nextActButton, "Next Act", () => SceneManager.LoadScene(nextActTarget));
    }

    private static void CreateSummaryText(RectTransform canvasRect, TMP_FontAsset font, string title)
    {
        GameObject textObject = new GameObject("ActResultsText", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textTransform = textObject.GetComponent<RectTransform>();
        textTransform.SetParent(canvasRect, false);
        textTransform.anchorMin = new Vector2(0.1f, 0.25f);
        textTransform.anchorMax = new Vector2(0.9f, 0.95f);
        textTransform.offsetMin = Vector2.zero;
        textTransform.offsetMax = Vector2.zero;
        textTransform.SetAsLastSibling();

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = font;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 28f;
        text.alignment = TextAlignmentOptions.Top;
        text.color = Color.white;
        text.raycastTarget = false;
        text.text = $"<size=130%><b>{title}</b></size>\n\n{PlayerDatabase.BuildSummary()}";
    }

    // Reuses the scene's styled choice_Container buttons, moved below the summary.
    private static bool TryUseChoiceButtons(RectTransform canvasRect, GameObject choiceContainer,
        out Button firstButton, out Button secondButton)
    {
        firstButton = null;
        secondButton = null;
        if (choiceContainer == null)
            return false;

        Transform first = choiceContainer.transform.Find("choice_Button1");
        Transform second = choiceContainer.transform.Find("choice_Button2");
        if (first == null || second == null)
            return false;

        firstButton = first.GetComponent<Button>();
        secondButton = second.GetComponent<Button>();
        if (firstButton == null || secondButton == null)
            return false;

        RectTransform containerTransform = choiceContainer.transform as RectTransform;
        if (containerTransform != null)
            containerTransform.anchoredPosition = new Vector2(0f, -canvasRect.rect.height * 0.35f);

        choiceContainer.transform.SetAsLastSibling();
        choiceContainer.SetActive(true);
        return true;
    }

    // Fallback for scenes without a choice_Container: plain buttons at the bottom of the screen.
    private static void CreateButtons(RectTransform canvasRect, TMP_FontAsset font, out Button firstButton, out Button secondButton)
    {
        float buttonY = -canvasRect.rect.height * 0.35f;
        firstButton = CreateButton(canvasRect, font, "MainMenuButton", new Vector2(-130f, buttonY));
        secondButton = CreateButton(canvasRect, font, "NextActButton", new Vector2(130f, buttonY));
    }

    private static Button CreateButton(RectTransform canvasRect, TMP_FontAsset font, string objectName, Vector2 position)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform buttonTransform = buttonObject.GetComponent<RectTransform>();
        buttonTransform.SetParent(canvasRect, false);
        buttonTransform.sizeDelta = new Vector2(162f, 50f);
        buttonTransform.anchoredPosition = position;
        buttonTransform.SetAsLastSibling();
        buttonObject.GetComponent<Image>().color = Color.white;

        GameObject labelObject = new GameObject("Text (TMP)", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform labelTransform = labelObject.GetComponent<RectTransform>();
        labelTransform.SetParent(buttonTransform, false);
        labelTransform.anchorMin = Vector2.zero;
        labelTransform.anchorMax = Vector2.one;
        labelTransform.offsetMin = Vector2.zero;
        labelTransform.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        if (font != null)
            label.font = font;
        label.fontSize = 20f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.black;

        return buttonObject.GetComponent<Button>();
    }

    private static void SetupButton(Button button, string label, UnityAction onClick)
    {
        button.gameObject.SetActive(true);
        button.interactable = true;

        TMP_Text buttonText = button.GetComponentInChildren<TMP_Text>(true);
        if (buttonText != null)
            buttonText.text = label;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(onClick);
    }
}
