using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Act 3, school in the morning. The player tries to "pay back" Elijah for what happened in Act 2,
// keeps doing good deeds for everyone, and the friends start to worry.
public class A3_School_control : A3SceneBase
{
    public const string HelpClassmateChoiceId = "act3_help_classmate";

    [Header("Characters")]
    [SerializeField] private string classmate1Name = "Elijah";
    [SerializeField] private string classmate2Name = "Classmate";

    [Header("Choice: Help carry the project?")]
    [SerializeField] private string carryChoiceText = "I'll carry it!";
    [SerializeField] private string restChoiceText = "Sorry... I need to sit down.";
    [SerializeField] private int carryPoints = 1;
    [SerializeField] private int restPoints = 0;

    [Header("Scene Transition")]
    [SerializeField] private string nextSceneName = "A3_Croom";

    [Header("Dialogue")]
    public List<A3Line> arriveLines = new List<A3Line>();
    [Tooltip("Act 2: the player stayed home and Elijah was left alone.")]
    public List<A3Line> elijahStayedLines = new List<A3Line>();
    [Tooltip("Act 2: the player broke the promise and went to Elijah.")]
    public List<A3Line> elijahWentLines = new List<A3Line>();
    [Tooltip("Act 2: the player lent Elijah lunch money.")]
    public List<A3Line> moneyLines = new List<A3Line>();
    public List<A3Line> worryLines = new List<A3Line>();
    public List<A3Line> carryLines = new List<A3Line>();
    public List<A3Line> restLines = new List<A3Line>();
    public List<A3Line> bellLines = new List<A3Line>();

    protected override void SetupScene()
    {
        A3Character elijah = AddCharacter("classmate1", classmate1Name);
        A3Character classmate = AddCharacter("classmate2", classmate2Name);
        SpreadIfStacked(elijah, classmate);

        // The pulubi was copied with the scene; he isn't here this morning.
        GameObject pulubi = FindSceneObject("pulubi");
        if (pulubi != null) pulubi.SetActive(false);

        FillDefaultLines();
    }

    private void FillDefaultLines()
    {
        if (arriveLines.Count == 0)
        {
            arriveLines.Add(new A3Line(playerName, "(Early. Good. I can fix Ma'am's chairs before anyone comes.)", "deed"));
            arriveLines.Add(new A3Line(playerName, "(Sweep the hallway too... Five.)", "deed"));
            arriveLines.Add(new A3Line(classmate2Name, "Uy, good morning! You're so early!", "enter:classmate2"));
            arriveLines.Add(new A3Line(playerName, "Good morning!"));
        }

        if (elijahStayedLines.Count == 0)
        {
            elijahStayedLines.Add(new A3Line(classmate1Name, "...", "enter:classmate1"));
            elijahStayedLines.Add(new A3Line(playerName, "Elijah! About last night... I'm so sorry."));
            elijahStayedLines.Add(new A3Line(playerName, "I'll make it up to you. I'll treat you to lunch. I'll help with your homework. I'll—"));
            elijahStayedLines.Add(new A3Line(classmate1Name, "Pre. Stop."));
            elijahStayedLines.Add(new A3Line(classmate1Name, "Papa and I talked this morning. We're okay now."));
            elijahStayedLines.Add(new A3Line(classmate1Name, "Yeah, I was hurt last night. But you don't have to pay me back for anything."));
            elijahStayedLines.Add(new A3Line(classmate1Name, "We're friends, 'di ba?"));
            elijahStayedLines.Add(new A3Line(playerName, "(...Then why do I still feel like I owe him?)"));
        }

        if (elijahWentLines.Count == 0)
        {
            elijahWentLines.Add(new A3Line(classmate1Name, "Pre! Salamat ulit kagabi, ha.", "enter:classmate1"));
            elijahWentLines.Add(new A3Line(playerName, "Of course! Is your Papa still mad? Do you need anything? I can—"));
            elijahWentLines.Add(new A3Line(classmate1Name, "Hoy, relax! You already did more than enough."));
            elijahWentLines.Add(new A3Line(playerName, "(Enough... I broke my promise to Nanay for that.)"));
            elijahWentLines.Add(new A3Line(playerName, "(It doesn't feel like enough.)"));
        }

        if (moneyLines.Count == 0)
        {
            moneyLines.Add(new A3Line(classmate1Name, "And about your money... I'll pay you back soon, promise."));
            moneyLines.Add(new A3Line(playerName, "No, no! Keep it! It's fine!", "deed"));
            moneyLines.Add(new A3Line(classmate1Name, "...Are you sure? You look like you need it more than me."));
        }

        if (worryLines.Count == 0)
        {
            worryLines.Add(new A3Line(classmate2Name, "Uy... are you okay? You look so tired."));
            worryLines.Add(new A3Line(classmate2Name, "Ma'am's chairs, the hallway... did you do all that this morning?"));
            worryLines.Add(new A3Line(playerName, "Hehe... it's nothing."));
            worryLines.Add(new A3Line(classmate2Name, "Hmm... Oh! Our project is so heavy. Can someone help me carry it to the room?"));
        }

        if (carryLines.Count == 0)
        {
            carryLines.Add(new A3Line(playerName, "Here, give it to me!", "deed"));
            carryLines.Add(new A3Line(classmate2Name, "Wow, salamat! ...Wait. Are your hands shaking?"));
            carryLines.Add(new A3Line(classmate1Name, "Pre, put it down. Sit down. Seriously."));
            carryLines.Add(new A3Line(playerName, "(I'm fine. I'm fine. That's {deeds}...)"));
        }

        if (restLines.Count == 0)
        {
            restLines.Add(new A3Line(playerName, "Sorry... I need to sit down for a bit."));
            restLines.Add(new A3Line(classmate1Name, "Finally. Pahinga ka muna, pre. I'll carry it."));
            restLines.Add(new A3Line(playerName, "(Why do I feel guilty just for resting...?)"));
        }

        if (bellLines.Count == 0)
        {
            bellLines.Add(new A3Line(classmate2Name, "Ay, the bell! Tara na, class is starting!", "sfx:bell"));
            bellLines.Add(new A3Line(playerName, "(Nanay hasn't replied to my text since this morning...)"));
        }
    }

    protected override IEnumerator RunScene()
    {
        StartLoop(ambientAudioSource, 1f, 1.5f);
        yield return FadeFromBlack(1.5f);
        yield return new WaitForSeconds(0.3f);

        yield return PlayLines(arriveLines);

        bool wentToElijah = PlayerDatabase.GetChoice(A2_Lroom_uwian.PromiseChoiceId) == A2_Lroom_uwian.WentAnswer;
        yield return PlayLines(wentToElijah ? elijahWentLines : elijahStayedLines);

        if (PlayerDatabase.GetChoice(A2_Croom_control.LendMoneyChoiceId) == "lent")
            yield return PlayLines(moneyLines);

        yield return PlayLines(worryLines);

        int picked = -1;
        yield return WaitForChoice(carryChoiceText, restChoiceText, choice => picked = choice);
        const string question = "Your classmate asked for help carrying their heavy project.";
        PlayerDatabase.RecordChoice(HelpClassmateChoiceId, question,
            picked == 0 ? carryChoiceText : restChoiceText,
            picked == 0 ? carryPoints : restPoints,
            picked == 0 ? "carried" : "rested");

        yield return PlayLines(picked == 0 ? carryLines : restLines);
        yield return PlayLines(bellLines);
        yield return FadeOutAndLoad(nextSceneName, "A3_Croom", 1.2f);
    }
}
