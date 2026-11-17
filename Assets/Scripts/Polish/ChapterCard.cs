using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The black-screen moments at the start of an act, shared by all three acts:
//  - Title():      "ACT 2" draws together, a thin gold line grows under it, then the act's name appears.
//  - Previously(): "Previously..." and what the player really chose in the act before, one line at a time,
//                  so the new act starts from their own story (only shown if there is something to recall).
// Both are coroutines run by the act's first scene while its screen is still black. They use game time,
// so they pause with the pause menu, and they clean up after themselves. A tap (or click, Space, Enter)
// skips the rest of a card: it fades out quickly and the scene goes on.
public static class ChapterCard
{
    private static readonly Color TitleColor = Color.white;
    private static readonly Color SubtitleColor = new Color(0.87f, 0.87f, 0.93f, 1f);
    private static readonly Color LineColor = new Color(1f, 0.82f, 0.45f, 0.9f);
    private static readonly Color HeaderColor = new Color(0.68f, 0.68f, 0.76f, 1f);
    private static readonly Color RecapColor = new Color(0.95f, 0.93f, 0.88f, 1f);

    private const int MaxRecapLines = 5;
    private const float SkippedFadeDuration = 0.35f;

    // Whether the player tapped to skip the card being shown.
    private class Skip
    {
        public bool pressed;

        public bool Check()
        {
            if (!pressed && TapPressed())
                pressed = true;
            return pressed;
        }
    }

    // ------------------------------------------------------------------ act title

    public static IEnumerator Title(Transform canvas, string title, string subtitle, float titleSize = 72f,
        float subtitleSize = 36f, float holdDuration = 2.2f)
    {
        if (canvas == null)
            yield break;

        Skip skip = new Skip();

        RectTransform root = PolishUI.CreateRect("ActTitleCard", canvas);
        PolishUI.Stretch(root);
        root.SetAsLastSibling();

        TextMeshProUGUI titleText = PolishUI.CreateText("Title", root, title, titleSize, TitleColor);
        Place(titleText.rectTransform, 0.52f, 0.7f);
        titleText.alpha = 0f;

        Image line = PolishUI.CreateImage("Line", root, null, LineColor);
        RectTransform lineRect = line.rectTransform;
        lineRect.anchorMin = new Vector2(0.5f, 0.5f);
        lineRect.anchorMax = new Vector2(0.5f, 0.5f);
        lineRect.sizeDelta = new Vector2(0f, 3f);
        lineRect.anchoredPosition = new Vector2(0f, 4f);

        TextMeshProUGUI subtitleText = PolishUI.CreateText("Subtitle", root, subtitle, subtitleSize, SubtitleColor);
        Place(subtitleText.rectTransform, 0.33f, 0.49f);
        subtitleText.alpha = 0f;

        yield return Wait(0.3f, skip);
        if (!skip.pressed)
            UISounds.TitleSwell();

        // The letters draw together while the title fades in; the line grows under it.
        float elapsed = 0f;
        const float titleDuration = 1.2f;
        while (elapsed < titleDuration && !skip.Check())
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / titleDuration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            titleText.alpha = Mathf.Clamp01(progress * 1.5f);
            titleText.characterSpacing = Mathf.Lerp(34f, 8f, eased);
            lineRect.sizeDelta = new Vector2(Mathf.Lerp(0f, 280f, Mathf.Clamp01((progress - 0.25f) / 0.75f)), 3f);
            yield return null;
        }

        yield return FadeText(subtitleText, 0f, 1f, 0.9f, skip);
        yield return Wait(holdDuration, skip);

        // Everything fades out together (quickly if skipped), from wherever it got to.
        float titleStart = titleText.alpha;
        float subtitleStart = subtitleText.alpha;
        float lineStart = line.color.a;
        float fadeDuration = skip.pressed ? SkippedFadeDuration : 1f;
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float remaining = 1f - Mathf.Clamp01(elapsed / fadeDuration);
            titleText.alpha = titleStart * remaining;
            subtitleText.alpha = subtitleStart * remaining;
            line.color = new Color(LineColor.r, LineColor.g, LineColor.b, lineStart * remaining);
            yield return null;
        }

        Object.Destroy(root.gameObject);
        yield return Wait(0.3f, null);
    }

    // ------------------------------------------------------------------ "Previously..."

    // previousAct = the act to recall (1 at the start of Act 2, 2 at the start of Act 3).
    public static IEnumerator Previously(Transform canvas, int previousAct)
    {
        List<string> lines = RecapLines(previousAct);
        if (canvas == null || lines.Count == 0)
            yield break;

        Skip skip = new Skip();

        RectTransform root = PolishUI.CreateRect("PreviouslyCard", canvas);
        PolishUI.Stretch(root);
        root.SetAsLastSibling();

        TextMeshProUGUI header = PolishUI.CreateText("Header", root, "Previously...", 28f, HeaderColor);
        Place(header.rectTransform, 0.76f, 0.86f);
        header.alpha = 0f;

        List<TextMeshProUGUI> texts = new List<TextMeshProUGUI>();
        float top = 0.68f;
        foreach (string line in lines)
        {
            TextMeshProUGUI text = PolishUI.CreateText("Recap", root, line, 26f, RecapColor);
            Place(text.rectTransform, top - 0.08f, top);
            text.enableAutoSizing = true;
            text.fontSizeMin = 14f;
            text.fontSizeMax = 26f;
            text.alpha = 0f;
            texts.Add(text);
            top -= 0.085f;
        }

        yield return Wait(0.6f, skip);
        yield return FadeText(header, 0f, 1f, 0.8f, skip);
        yield return Wait(0.3f, skip);

        foreach (TextMeshProUGUI text in texts)
        {
            yield return FadeText(text, 0f, 1f, 0.6f, skip);
            yield return Wait(0.45f, skip);
        }

        yield return Wait(2.4f, skip);

        // Fades out together (quickly if skipped), from wherever each line got to.
        float headerStart = header.alpha;
        float[] textStarts = new float[texts.Count];
        for (int i = 0; i < texts.Count; i++)
            textStarts[i] = texts[i].alpha;

        float fadeDuration = skip.pressed ? SkippedFadeDuration : 0.9f;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float remaining = 1f - Mathf.Clamp01(elapsed / fadeDuration);
            header.alpha = headerStart * remaining;
            for (int i = 0; i < texts.Count; i++)
                texts[i].alpha = textStarts[i] * remaining;
            yield return null;
        }

        Object.Destroy(root.gameObject);
        yield return Wait(0.5f, null);
    }

    // The player's own choices from that act, oldest first, written as a short reminder.
    public static List<string> RecapLines(int act)
    {
        List<(int priority, string time, string line)> found = new List<(int, string, string)>();
        foreach (PlayerChoiceRecord choice in PlayerDatabase.Data.choices)
        {
            if (!PlayerDatabase.IsSceneFromAct(choice.sceneName, act))
                continue;

            string line = RecapFor(choice, out int priority);
            if (!string.IsNullOrEmpty(line))
                found.Add((priority, choice.recordedAt, line));
        }

        // The most important ones (lowest number) if there are too many, then shown in the order they happened.
        found.Sort((a, b) => a.priority.CompareTo(b.priority));
        if (found.Count > MaxRecapLines)
            found.RemoveRange(MaxRecapLines, found.Count - MaxRecapLines);
        found.Sort((a, b) => string.CompareOrdinal(a.time, b.time));

        List<string> lines = new List<string>();
        foreach (var entry in found)
            lines.Add(entry.line);
        return lines;
    }

    private static string RecapFor(PlayerChoiceRecord choice, out int priority)
    {
        string id = choice.choiceId ?? "";
        string answer = choice.answerId ?? "";
        string lowerAnswer = answer.ToLowerInvariant();
        priority = 9;

        if (id.StartsWith("Act One_line"))
        {
            priority = 0;
            if (lowerAnswer.Contains("good")) return "At the gates, you told God you had been good.";
            if (lowerAnswer.Contains("know")) return "At the gates, you had no answer for God.";
            return null;
        }

        if (id.StartsWith("A1_LivingRoom_line"))
        {
            priority = 2;
            return choice.points > 0 ? "You helped Nanay before school, no questions asked." : "You complained, but you still helped Nanay.";
        }

        switch (id)
        {
            case "act1_cut_class":
                priority = 3;
                return answer == "cut" ? "You were ready to cut class with Elijah." : "You refused to cut class.";
            case "act1_pulubi":
                priority = 1;
                return answer == "give" ? "You gave your pamasahe to a hungry pulubi." : "A hungry pulubi asked for help. You said you had nothing to spare.";

            case "act2_promise_nanay":
                priority = 0;
                return answer == "went" ? "You broke your promise to Nanay and went to Elijah." : "You kept your promise to Nanay. Elijah spent the night alone.";
            case "act2_pulubi_medicine":
                priority = 1;
                if (answer == "gave_fare") return "You gave your pamasahe for a sick child's medicine.";
                if (answer == "kept_fare") return "A father begged for his child's medicine. You kept your pamasahe.";
                return "A father begged for his child's medicine. You froze.";
            case "act2_honest_nanay":
                priority = 2;
                return answer == "truth" ? "You told Nanay the truth." : "You lied to Nanay.";
            case "act2_why_good":
                priority = 3;
                return answer == "heaven" ? "You told God you do good so you can enter Heaven." : "You told God you do good because you love them.";
            case "act2_let_copy":
                priority = 4;
                return answer == "let_copy" ? "You let Elijah copy your answers." : "You wouldn't let Elijah copy.";
            case "act2_did_you_review":
                priority = 5;
                return answer == "lied" ? "You told your friends you had reviewed. You hadn't." : "You admitted you hadn't reviewed.";
            case "act2_lend_money":
                priority = 6;
                return answer == "lent" ? "You gave Elijah your lunch money." : "You kept your lunch money.";
            case "act2_answer_sure":
                priority = 7;
                return answer == "insisted" ? "You said you were sure of an answer. You weren't." : "You admitted you weren't sure.";
        }

        return null;
    }

    // ------------------------------------------------------------------ helpers

    private static void Place(RectTransform rect, float bottom, float top)
    {
        rect.anchorMin = new Vector2(0.08f, bottom);
        rect.anchorMax = new Vector2(0.92f, top);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // Waits in game time; stops early once the player skips (skip may be null = can't be skipped).
    private static IEnumerator Wait(float seconds, Skip skip)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (skip != null && skip.Check())
                yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    // Fades a text; when skipped, it stops where it is (the card's fade-out takes it from there).
    private static IEnumerator FadeText(TMP_Text text, float from, float to, float duration, Skip skip)
    {
        float elapsed = 0f;
        float length = Mathf.Max(duration, 0.01f);
        while (elapsed < length)
        {
            if (skip != null && skip.Check())
                yield break;

            elapsed += Time.deltaTime;
            text.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / length));
            yield return null;
        }

        text.alpha = to;
    }

    // A tap or click (finger/button let go this frame), Space or Enter. Ignored while the pause menu is open.
    // Releases are used, not presses: tapping the pause button opens the pause menu (during Update, before
    // this runs) on release, so that tap is ignored here and never skips the card behind the menu.
    private static bool TapPressed()
    {
        if (PauseMenu.IsPaused || PauseMenu.LastResumeFrame == Time.frameCount)
            return false;

#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Touchscreen.current != null && UnityEngine.InputSystem.Touchscreen.current.primaryTouch.press.wasReleasedThisFrame)
            return true;
        if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasReleasedThisFrame)
            return true;
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            (UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame || UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame))
            return true;
#endif
        return false;
    }
}
