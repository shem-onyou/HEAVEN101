using TMPro;

// How long the typewriter waits after each letter. Every dialogue script's typewriter loop calls Delay()
// instead of waiting the same time for every character, so lines read like someone talking:
// a short breath after a comma, a real pause at the end of a sentence or after "...", and no pause
// in the middle of "?!" or "..." (only after the last mark). Numbers like "2.5" don't pause.
//
// The pauses scale with the line's own speed, so slow lines (God, the player's thoughts) pause longer.
// Nothing is allocated here: it runs for every letter.
public static class DialoguePacing
{
    // Multiples of the normal per-letter time.
    private const float SentencePause = 9f;   // . ! ? and ... (0.27 s at the usual 0.03 s per letter)
    private const float CommaPause = 4f;      // , ; : and dashes (0.12 s)

    // text: the dialogue text being typed. visibleCharacters: how many characters are showing now
    // (the loop's i). baseDelay: the line's normal time per letter.
    public static float Delay(TMP_Text text, int visibleCharacters, float baseDelay)
    {
        if (text == null || visibleCharacters <= 0)
            return baseDelay;

        TMP_TextInfo info = text.textInfo;
        if (info == null || info.characterInfo == null)
            return baseDelay;

        // The last letter was just shown: the line is done, nothing to pause for.
        if (visibleCharacters >= info.characterCount || visibleCharacters >= info.characterInfo.Length)
            return baseDelay;

        char shown = info.characterInfo[visibleCharacters - 1].character;
        char next = info.characterInfo[visibleCharacters].character;
        char before = visibleCharacters >= 2 ? info.characterInfo[visibleCharacters - 2].character : ' ';

        if (IsSentenceEnd(shown))
        {
            // "?!", "..." and closing quotes/brackets keep going; the pause comes after the last mark.
            if (IsSentenceEnd(next) || IsClosingMark(next))
                return baseDelay;

            if (char.IsWhiteSpace(next))
                return baseDelay * SentencePause;

            // "...Then I won't." pauses after the dots too; "2.5" does not.
            bool ellipsis = shown == '…' || (shown == '.' && before == '.');
            return ellipsis ? baseDelay * SentencePause : baseDelay;
        }

        // A closing quote or bracket right after the end of a sentence: pause after it instead.
        if (IsClosingMark(shown) && IsSentenceEnd(before))
            return char.IsWhiteSpace(next) ? baseDelay * SentencePause : baseDelay;

        if (shown == ',' || shown == ';' || shown == ':' || shown == '—' || shown == '–')
            return char.IsWhiteSpace(next) || IsClosingMark(next) ? baseDelay * CommaPause : baseDelay;

        return baseDelay;
    }

    private static bool IsSentenceEnd(char c)
    {
        return c == '.' || c == '!' || c == '?' || c == '…';
    }

    private static bool IsClosingMark(char c)
    {
        return c == '"' || c == '\'' || c == ')' || c == '”' || c == '’' || c == '*';
    }
}
