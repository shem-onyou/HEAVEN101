using UnityEngine;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// The end-of-act recap appears one line at a time (ActResultsScreen adds this to the recap text).
// Each line with points ticks: higher for good points, lower for bad ones. Empty lines don't wait.
// When everything is shown, the Main Menu / Next Act buttons appear. A tap or click shows everything at once,
// and the buttons always appear after a few seconds at most, so the player can never be stuck here.
// Uses real time, so it can't freeze if the game is paused.
//
// Lines are revealed with maxVisibleCharacters (like the dialogue typewriters), so the text's layout and
// font size never change while it appears.
public class ResultsReveal : MonoBehaviour
{
    private const float FirstDelay = 0.6f;
    private const float LineDelay = 0.3f;
    private const float PanelFadeDuration = 0.4f;
    private const float MaxDuration = 9f;
    private const int ShowAll = 99999;

    private TMP_Text text;
    private CanvasGroup panel;
    private GameObject[] buttons;

    // Worked out once from the full layout: where each line ends, whether it is empty, and its points sign.
    private int[] lineEnds = new int[0];
    private bool[] lineEmpty = new bool[0];
    private int[] linePoints = new int[0];

    private int shownLines;
    private float timer;
    private float age;
    private bool done;

    public void Begin(TMP_Text summary, CanvasGroup panelGroup, GameObject[] buttonObjects)
    {
        text = summary;
        panel = panelGroup;
        buttons = buttonObjects;

        text.maxVisibleCharacters = ShowAll;
        text.ForceMeshUpdate();
        ReadLines(text.textInfo);

        shownLines = 0;
        timer = -FirstDelay;

        if (lineEnds.Length == 0)
        {
            Finish();
            return;
        }

        text.maxVisibleCharacters = 0;
    }

    private void ReadLines(TMP_TextInfo info)
    {
        int count = info != null ? Mathf.Min(info.lineCount, info.lineInfo.Length) : 0;
        lineEnds = new int[count];
        lineEmpty = new bool[count];
        linePoints = new int[count];

        for (int i = 0; i < count; i++)
        {
            TMP_LineInfo line = info.lineInfo[i];
            lineEnds[i] = line.lastCharacterIndex + 1;
            lineEmpty[i] = line.visibleCharacterCount == 0;

            // The first letters of a "+1" line are green, of a "-1" line red (see PlayerDatabase.FormatPoints).
            int first = line.firstVisibleCharacterIndex;
            if (!lineEmpty[i] && first >= 0 && first < info.characterInfo.Length)
            {
                Color32 color = info.characterInfo[first].color;
                if (color.g > color.r + 40) linePoints[i] = 1;
                else if (color.r > color.g + 60) linePoints[i] = -1;
            }
        }
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;
        age += deltaTime;

        if (panel != null && panel.alpha < 1f)
            panel.alpha = Mathf.Clamp01(age / PanelFadeDuration);

        if (done)
            return;

        if (text == null || age > MaxDuration || (age > 0.2f && SkipPressed()))
        {
            Finish();
            return;
        }

        timer += deltaTime;
        while (!done && timer >= LineDelay)
        {
            timer -= LineDelay;
            ShowNextLine();

            // Empty lines come right away with the next line.
            while (!done && shownLines < lineEnds.Length && lineEmpty[shownLines])
                ShowNextLine();
        }
    }

    private void ShowNextLine()
    {
        int line = shownLines;
        shownLines++;
        text.maxVisibleCharacters = lineEnds[line];

        // The title (line 0) comes with the act's own completion sound.
        if (line > 0 && !lineEmpty[line])
            UISounds.Tick(linePoints[line]);

        if (shownLines >= lineEnds.Length)
            Finish();
    }

    private void Finish()
    {
        if (done)
            return;

        done = true;
        if (text != null)
            text.maxVisibleCharacters = ShowAll;
        if (panel != null)
            panel.alpha = 1f;

        if (buttons != null)
        {
            foreach (GameObject button in buttons)
            {
                if (button != null)
                    button.SetActive(true);
            }
        }
    }

    private static bool SkipPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            return true;
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return true;
#endif
        return false;
    }
}
