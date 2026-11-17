using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public class PlayerChoiceRecord
{
    public string choiceId;
    public string sceneName;
    public string question;
    public string answerId;
    public string answerText;
    public int points;
    public string recordedAt;
}

[Serializable]
public class PlayerActionRecord
{
    public string actionId;
    public string sceneName;
    public string description;
    public float value;
    public int points;
    public string recordedAt;
}

[Serializable]
public class PlayerSaveData
{
    // 1 = finishing an act moves the Continue point to the start of the next act (see PlayerDatabase.UpgradeOldSave).
    public int saveVersion = PlayerDatabase.SaveVersion;
    // The story scene to go back to (Continue, or pressing the act the player is in the middle of).
    public string currentScene = "";
    public List<string> visitedScenes = new List<string>();
    public List<PlayerChoiceRecord> choices = new List<PlayerChoiceRecord>();
    public List<PlayerActionRecord> actions = new List<PlayerActionRecord>();
    public List<int> completedActs = new List<int>();
    public string lastSaved = "";
}

// Local save file for everything the player answers and does.
// Saved as JSON in Application.persistentDataPath/player_save.json.
public static class PlayerDatabase
{
    private const string SaveFileName = "player_save.json";
    public const int SaveVersion = 1;

    // Scenes that are menus, not story, so they are never saved as the Continue point.
    private static readonly string[] MenuScenes = { "Main Menu", "Acts" };

    // The first scene of each act (index = act number). Keep in sync with the Acts screen's scene fields.
    private static readonly string[] ActFirstScenes = { "", "Act One", "A2_Broom", "A3_Broom" };

    private static PlayerSaveData data;

    // Raised right after a choice is saved (StoryNotifications shows "Nanay will remember this." from it).
    // A listener that throws can never break the scene that made the choice.
    public static event Action<PlayerChoiceRecord> ChoiceRecorded;

    public static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    public static PlayerSaveData Data
    {
        get
        {
            if (data == null)
                Load();
            return data;
        }
    }

    public static bool HasSavedProgress => !string.IsNullOrEmpty(Data.currentScene);

    // Total of all points from choices and actions. Positive = good deeds.
    public static int GoodPoints
    {
        get
        {
            int total = 0;
            foreach (PlayerChoiceRecord choice in Data.choices) total += choice.points;
            foreach (PlayerActionRecord action in Data.actions) total += action.points;
            return total;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Initialize()
    {
        data = null;
        ChoiceRecorded = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Array.IndexOf(MenuScenes, scene.name) >= 0)
            return;

        Data.currentScene = scene.name;
        if (!Data.visitedScenes.Contains(scene.name))
            Data.visitedScenes.Add(scene.name);

        Save();
    }

    // The act the player is in the middle of (the act of the saved scene), or 0 if none.
    public static int CurrentAct => ActOfScene(Data.currentScene);

    public static string FirstSceneOfAct(int actNumber)
    {
        return actNumber > 0 && actNumber < ActFirstScenes.Length ? ActFirstScenes[actNumber] : "";
    }

    // "A2_School" -> 2, "Act One" -> 1, a menu or unknown scene -> 0.
    public static int ActOfScene(string sceneName)
    {
        for (int act = 1; act < ActFirstScenes.Length; act++)
        {
            if (IsFromAct(sceneName, act))
                return act;
        }

        return 0;
    }

    // Playing an act from the beginning: that act's answers and actions are erased (the other acts keep theirs).
    // Finished acts stay unlocked.
    public static void RestartAct(int actNumber)
    {
        Data.choices.RemoveAll(choice => IsFromAct(choice.sceneName, actNumber));
        Data.actions.RemoveAll(action => IsFromAct(action.sceneName, actNumber));
        Save();
    }

    // The act is over: Continue now leads to the start of the next act (nothing after the last act).
    public static void MarkActCompleted(int actNumber)
    {
        if (!Data.completedActs.Contains(actNumber))
            Data.completedActs.Add(actNumber);

        if (ActOfScene(Data.currentScene) == actNumber)
            Data.currentScene = FirstSceneOfAct(actNumber + 1);

        Save();
    }

    public static bool IsActCompleted(int actNumber)
    {
        return Data.completedActs.Contains(actNumber);
    }

    // Records the answer to a question. Answering the same choiceId again (e.g. replaying a scene)
    // replaces the old answer. answerId is the stable value used by story logic; it defaults to answerText.
    public static void RecordChoice(string choiceId, string question, string answerText, int points = 0, string answerId = null)
    {
        Data.choices.RemoveAll(choice => choice.choiceId == choiceId);
        PlayerChoiceRecord record = new PlayerChoiceRecord
        {
            choiceId = choiceId,
            sceneName = SceneManager.GetActiveScene().name,
            question = question,
            answerId = string.IsNullOrEmpty(answerId) ? answerText : answerId,
            answerText = answerText,
            points = points,
            recordedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };
        Data.choices.Add(record);

        Save();

        try
        {
            ChoiceRecorded?.Invoke(record);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    // Records something the player did. Doing the same actionId again replaces the old record.
    public static void RecordAction(string actionId, string description, float value = 0f, int points = 0)
    {
        Data.actions.RemoveAll(action => action.actionId == actionId);
        Data.actions.Add(new PlayerActionRecord
        {
            actionId = actionId,
            sceneName = SceneManager.GetActiveScene().name,
            description = description,
            value = value,
            points = points,
            recordedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        });

        Save();
    }

    // Returns the answerId of a recorded choice, or null if the player has not answered it yet.
    public static string GetChoice(string choiceId)
    {
        PlayerChoiceRecord record = Data.choices.Find(choice => choice.choiceId == choiceId);
        return record != null ? record.answerId : null;
    }

    public static bool HasDoneAction(string actionId)
    {
        return Data.actions.Exists(action => action.actionId == actionId);
    }

    public static string BuildSummary()
    {
        StringBuilder summary = new StringBuilder();

        summary.AppendLine("Your Choices");
        if (Data.choices.Count == 0)
            summary.AppendLine("- (none)");
        foreach (PlayerChoiceRecord choice in Data.choices)
            summary.AppendLine($"- {choice.question}\n   > {choice.answerText} ({FormatPoints(choice.points)})");

        summary.AppendLine();
        summary.AppendLine("What You Did");
        if (Data.actions.Count == 0)
            summary.AppendLine("- (none)");
        foreach (PlayerActionRecord action in Data.actions)
            summary.AppendLine($"- {action.description} ({FormatPoints(action.points)})");

        summary.AppendLine();
        summary.Append($"Good Points: {GoodPoints}");
        return summary.ToString();
    }

    // A short, phone-friendly recap of one act: only what changed the good points,
    // one short line each, in the order it happened.
    public static string BuildActSummary(int actNumber)
    {
        List<(string time, int order, int points, string label)> entries = new List<(string, int, int, string)>();
        int actPoints = 0;

        foreach (PlayerChoiceRecord choice in Data.choices)
        {
            if (!IsFromAct(choice.sceneName, actNumber))
                continue;

            actPoints += choice.points;
            if (choice.points != 0)
                entries.Add((choice.recordedAt, entries.Count, choice.points, ShortLabel(choice.choiceId, choice.points, choice.answerText)));
        }

        foreach (PlayerActionRecord action in Data.actions)
        {
            if (!IsFromAct(action.sceneName, actNumber))
                continue;

            actPoints += action.points;
            if (action.points != 0)
                entries.Add((action.recordedAt, entries.Count, action.points, ShortLabel(action.actionId, action.points, action.description)));
        }

        entries.Sort((a, b) =>
        {
            int byTime = string.CompareOrdinal(a.time, b.time);
            return byTime != 0 ? byTime : a.order.CompareTo(b.order);
        });

        StringBuilder summary = new StringBuilder();
        summary.AppendLine($"This Act: {FormatPoints(actPoints)}    Total: {GoodPoints}");
        summary.AppendLine();

        if (entries.Count == 0)
            summary.AppendLine("(nothing recorded)");
        foreach (var entry in entries)
            summary.AppendLine($"{FormatPoints(entry.points)}  {entry.label}");

        return summary.ToString().TrimEnd();
    }

    // Is this scene part of that act? (Used by the "Previously..." recap at the start of an act.)
    public static bool IsSceneFromAct(string sceneName, int actNumber)
    {
        return IsFromAct(sceneName, actNumber);
    }

    // Act 1 scenes are "Act One" and "A1_..."; Act 2 scenes are "A2_...", and so on.
    private static bool IsFromAct(string sceneName, int actNumber)
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;

        if (actNumber == 1 && sceneName.StartsWith("Act One"))
            return true;

        return sceneName.StartsWith($"A{actNumber}_");
    }

    // Short labels for the results screen. Unknown ids fall back to the recorded text, shortened.
    private static string ShortLabel(string id, int points, string fallback)
    {
        bool good = points > 0;

        if (id.StartsWith("A1_LivingRoom_line"))
            return good ? "Helped Nanay" : "Didn't help Nanay";

        switch (id)
        {
            case "act1_morning_chores": return "Cleaned the living room";
            case "act1_cut_class": return good ? "Went to class" : "Cut class";
            case "act1_pulubi": return good ? "Gave to the pulubi" : "Didn't give to the pulubi";

            case "act2_help_nanay": return good ? "Helped Nanay again" : "Didn't help Nanay";
            case "act2_morning_chores": return "Cleaned the living room";
            case "act2_did_you_review": return good ? "Admitted you didn't review" : "Lied about reviewing";
            case "act2_let_copy": return good ? "Didn't let Elijah copy" : "Let Elijah copy";
            case "act2_answer_sure": return good ? "Admitted you weren't sure" : "Insisted you were right";
            case "act2_lend_money": return good ? "Lent Elijah your lunch money" : "Kept your lunch money";
            case "act2_pulubi_medicine": return good ? "Gave your pamasahe for medicine" : "Kept your pamasahe";
            case "act2_honest_nanay": return good ? "Told Nanay the truth" : "Lied to Nanay";
            case "act2_promise_nanay": return good ? "Kept your promise to Nanay" : "Broke your promise to Nanay";

            case "act3_morning_chores": return "Cleaned the whole house";
            case "act3_help_classmate": return good ? "Carried the project alone" : "Carried the project together";
            case "act3_care_nanay": return "Took care of Nanay all night";
        }

        if (string.IsNullOrEmpty(fallback))
            return id;

        return fallback.Length > 36 ? fallback.Substring(0, 33) + "..." : fallback;
    }

    // Every good deed from every act, oldest first, as short labels ("Helped Nanay").
    // Used by the final summary of the game.
    public static List<string> GetGoodDeedLabels()
    {
        List<(string time, string label)> deeds = new List<(string, string)>();
        foreach (PlayerChoiceRecord choice in Data.choices)
            if (choice.points > 0) deeds.Add((choice.recordedAt, ShortLabel(choice.choiceId, choice.points, choice.answerText)));
        foreach (PlayerActionRecord action in Data.actions)
            if (action.points > 0) deeds.Add((action.recordedAt, ShortLabel(action.actionId, action.points, action.description)));

        deeds.Sort((a, b) => string.CompareOrdinal(a.time, b.time));

        List<string> labels = new List<string>();
        foreach (var deed in deeds)
        {
            if (!labels.Contains(deed.label))
                labels.Add(deed.label);
        }

        return labels;
    }

    // The player's own good deeds from every act, newest first, written as something they would say
    // in a prayer: "Lord... I helped Nanay." Used by the Act 3 prayer.
    public static List<string> GetGoodDeedPrayers()
    {
        List<(string time, string label)> deeds = new List<(string, string)>();
        foreach (PlayerChoiceRecord choice in Data.choices)
            if (choice.points > 0) deeds.Add((choice.recordedAt, ShortLabel(choice.choiceId, choice.points, choice.answerText)));
        foreach (PlayerActionRecord action in Data.actions)
            if (action.points > 0) deeds.Add((action.recordedAt, ShortLabel(action.actionId, action.points, action.description)));

        deeds.Sort((a, b) => string.CompareOrdinal(b.time, a.time));

        List<string> prayers = new List<string>();
        foreach (var deed in deeds)
        {
            string prayer = $"Lord... I {ToFirstPerson(deed.label)}.";
            if (!prayers.Contains(prayer))
                prayers.Add(prayer);
        }

        return prayers;
    }

    // "Gave your pamasahe" -> "gave my pamasahe", "Admitted you weren't sure" -> "admitted I wasn't sure".
    private static string ToFirstPerson(string label)
    {
        string text = " " + label + " ";
        text = text.Replace(" your ", " my ").Replace(" you weren't ", " I wasn't ").Replace(" you ", " I ");
        text = text.Trim();
        return text.Length > 0 ? char.ToLowerInvariant(text[0]) + text.Substring(1) : text;
    }

    // Uses TMP rich text: green for good points, red for bad ones.
    private static string FormatPoints(int points)
    {
        if (points > 0) return $"<color=#7CFC7C>+{points}</color>";
        if (points < 0) return $"<color=#FF7070>{points}</color>";
        return points.ToString();
    }

    public static void Save()
    {
        if (data == null)
            return;

        data.lastSaved = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        try
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"PlayerDatabase: could not save to {SavePath}\n{exception.Message}");
        }
    }

    public static void Load()
    {
        data = null;
        bool oldSave = false;

        try
        {
            if (File.Exists(SavePath))
            {
                string json = File.ReadAllText(SavePath);
                data = JsonUtility.FromJson<PlayerSaveData>(json);
                oldSave = !json.Contains("\"saveVersion\"");
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"PlayerDatabase: could not read {SavePath}, starting fresh.\n{exception.Message}");
        }

        if (data == null)
            data = new PlayerSaveData();
        else if (oldSave)
            UpgradeOldSave();
    }

    // Saves made before saveVersion 1 kept the last scene of a finished act as the Continue point.
    // They are moved forward the same way MarkActCompleted does now.
    private static void UpgradeOldSave()
    {
        int act = ActOfScene(data.currentScene);
        if (act > 0 && data.completedActs.Contains(act))
            data.currentScene = FirstSceneOfAct(act + 1);

        data.saveVersion = SaveVersion;
        Save();
    }

    public static void DeleteSave()
    {
        data = new PlayerSaveData();

        try
        {
            if (File.Exists(SavePath))
                File.Delete(SavePath);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"PlayerDatabase: could not delete {SavePath}\n{exception.Message}");
        }
    }
}
