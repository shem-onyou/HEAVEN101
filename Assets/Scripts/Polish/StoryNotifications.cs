using UnityEngine;

// The quiet line in the top-left corner after an important choice, like "Nanay will remember this."
// It never says whether the choice was good or bad: it only tells the player the choice mattered
// (and at the gates in Act 3, God really does remember them).
//
// It runs by itself: PlayerDatabase.RecordChoice raises ChoiceRecorded and this shows the line that
// matches the choice id. A new choice gets a notification by adding a case to MessageFor; a choice
// with no case shows nothing.
public static class StoryNotifications
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        PlayerDatabase.ChoiceRecorded -= OnChoiceRecorded;
        PlayerDatabase.ChoiceRecorded += OnChoiceRecorded;
    }

    private static void OnChoiceRecorded(PlayerChoiceRecord choice)
    {
        string message = MessageFor(choice.choiceId, choice.answerId);
        if (!string.IsNullOrEmpty(message))
            StoryToast.Show(message);
    }

    // What the corner says after each choice (null = nothing).
    public static string MessageFor(string choiceId, string answerId)
    {
        if (string.IsNullOrEmpty(choiceId))
            return null;

        // Act One: God's question at the gates (saved as "Act One_line10").
        if (choiceId.StartsWith("Act One_line"))
            return "God will remember your answer.";

        // Act 1 living room: Nanay asks for help before school.
        if (choiceId.StartsWith("A1_LivingRoom_line"))
            return "Nanay will remember this.";

        switch (choiceId)
        {
            case "act1_cut_class": return "Elijah will remember this.";
            case "act1_pulubi": return "The pulubi will remember this.";

            case "act2_help_nanay": return "Nanay will remember this.";
            case "act2_did_you_review": return "Your friends will remember this.";
            case "act2_let_copy":
            case "act2_answer_sure":
            case "act2_lend_money": return "Elijah will remember this.";
            case "act2_pulubi_medicine": return "The pulubi will remember this.";
            case "act2_honest_nanay": return "Nanay will remember this.";
            // Breaking the promise is what Nanay remembers; keeping it is what Elijah remembers.
            case "act2_promise_nanay": return answerId == "went" ? "Nanay will remember this." : "Elijah will remember this.";
            case "act2_why_good": return "God will remember your answer.";

            case "act3_why_chasing": return "Nanay will remember this.";
            case "act3_help_classmate": return "Perzifanie will remember this.";
            case "act3_pulubi_listen": return "The pulubi will remember this.";
            // The end of the night prayer. Nothing seems to answer... but at the gates God says "I heard you".
            case "act3_prayer": return "He heard you.";
        }

        return null;
    }
}
