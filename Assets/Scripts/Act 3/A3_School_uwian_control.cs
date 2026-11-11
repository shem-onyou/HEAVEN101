using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Act 3, running home. The pulubi stops the player, not to ask for anything this time,
// but to offer a prayer. He has nothing to give God, so he simply trusts Him.
// Whether the player stops or runs past, they hear it.
public class A3_School_uwian_control : A3SceneBase
{
    public const string PulubiListenChoiceId = "act3_pulubi_listen";

    [Header("Characters")]
    [SerializeField] private string pulubiName = "Pulubi";

    [Header("Choice")]
    [SerializeField] private string stopChoiceText = "\"Po? What is it?\"";
    [SerializeField] private string goChoiceText = "\"Sorry po, I have to go!\"";

    [Header("Running")]
    [SerializeField] private float runBobStrength = 6f;

    [Header("Scene Transition")]
    [SerializeField] private string nextSceneName = "A3_Lroom_uwi";

    [Header("Dialogue")]
    public List<A3Line> introLines = new List<A3Line>();
    public List<A3Line> listenLines = new List<A3Line>();
    [Tooltip("Played when the player gave their pamasahe to the pulubi in Act 2.")]
    public List<A3Line> childBetterLines = new List<A3Line>();
    public List<A3Line> childStillSickLines = new List<A3Line>();
    public List<A3Line> trustLines = new List<A3Line>();
    public List<A3Line> runLines = new List<A3Line>();

    private Coroutine runningCoroutine;

    protected override void SetupScene()
    {
        AddCharacter("pulubi", pulubiName, trembles: true);

        // The friends stayed in class.
        GameObject classmate1 = FindSceneObject("classmate1");
        if (classmate1 != null) classmate1.SetActive(false);
        GameObject classmate2 = FindSceneObject("classmate2");
        if (classmate2 != null) classmate2.SetActive(false);

        FillDefaultLines();
    }

    private void FillDefaultLines()
    {
        if (introLines.Count == 0)
        {
            introLines.Add(new A3Line(playerName, "(Please... please be okay, Nay...)", "run"));
            introLines.Add(new A3Line(pulubiName, "Anak! Anak, wait!", "stoprun;enter:pulubi"));
            introLines.Add(new A3Line(playerName, "I'm sorry po, I can't stop! My Nanay—"));
        }

        if (listenLines.Count == 0)
        {
            listenLines.Add(new A3Line(playerName, "Po? What is it?"));
            listenLines.Add(new A3Line(pulubiName, "I won't ask for anything, anak. I can see you're scared."));
        }

        if (childBetterLines.Count == 0)
        {
            childBetterLines.Add(new A3Line(pulubiName, "Your pamasahe bought my daughter's medicine. She's better now. Salamat."));
        }

        if (childStillSickLines.Count == 0)
        {
            childStillSickLines.Add(new A3Line(pulubiName, "My daughter is still sick. I couldn't buy all her medicine."));
        }

        if (trustLines.Count == 0)
        {
            trustLines.Add(new A3Line(playerName, "Then... how can you be so calm?"));
            trustLines.Add(new A3Line(pulubiName, "Because I have nothing, anak. No money. Nothing good enough to give Him."));
            trustLines.Add(new A3Line(pulubiName, "Kaya nagtitiwala na lang ako. And He has never let go of me."));
            trustLines.Add(new A3Line(pulubiName, "Go to your Nanay. I'll pray for you both."));
            trustLines.Add(new A3Line(pulubiName, "Hindi ka nag-iisa, anak."));
            trustLines.Add(new A3Line(playerName, "(...Trust?)", "run"));
        }

        if (runLines.Count == 0)
        {
            runLines.Add(new A3Line(playerName, "Sorry po, I have to go!", "run"));
            runLines.Add(new A3Line(pulubiName, "Anak! Hindi ka nag-iisa! Magtiwala ka sa Kanya!", "leaveslow:pulubi"));
            runLines.Add(new A3Line(playerName, "(Trust...? Trust who? I've been doing everything by myself...)"));
        }
    }

    protected override IEnumerator RunScene()
    {
        StartLoop(ambientAudioSource, 0.6f, 1f);
        yield return FadeFromBlack(0.8f);

        yield return PlayLines(introLines);

        int picked = -1;
        yield return WaitForChoice(stopChoiceText, goChoiceText, choice => picked = choice);
        const string question = "While you ran home, the pulubi stopped you and offered to pray for you.";
        PlayerDatabase.RecordChoice(PulubiListenChoiceId, question,
            picked == 0 ? stopChoiceText : goChoiceText, 0,
            picked == 0 ? "listened" : "ran");

        if (picked == 0)
        {
            bool gaveFare = PlayerDatabase.GetChoice(A2_School_uwian_scr.PulubiChoiceId) == A2_School_uwian_scr.GaveFareAnswer;
            yield return PlayLines(listenLines);
            yield return PlayLines(gaveFare ? childBetterLines : childStillSickLines);
            yield return PlayLines(trustLines);
        }
        else
        {
            yield return PlayLines(runLines);
        }

        yield return FadeOutAndLoad(nextSceneName, "A3_Lroom_uwi", 1f);
    }

    protected override IEnumerator PerformCustomAction(string action, string argument)
    {
        switch (action)
        {
            case "run":
                // The street bobs up and down like the player is running.
                if (runningCoroutine == null)
                    runningCoroutine = StartCoroutine(RunningBob());
                break;

            case "stoprun":
                StopRunning();
                break;
        }

        yield break;
    }

    private IEnumerator RunningBob()
    {
        if (backgroundRenderer == null)
            yield break;

        Transform backgroundTransform = backgroundRenderer.transform;
        float elapsed = 0f;
        while (true)
        {
            elapsed += Time.deltaTime;
            float bob = Mathf.Abs(Mathf.Sin(elapsed * 9f)) * runBobStrength;
            backgroundTransform.localPosition = backgroundRestPosition + new Vector3(0f, -bob, 0f);
            yield return null;
        }
    }

    private void StopRunning()
    {
        if (runningCoroutine != null)
        {
            StopCoroutine(runningCoroutine);
            runningCoroutine = null;
        }

        if (backgroundRenderer != null)
            backgroundRenderer.transform.localPosition = backgroundRestPosition;
    }
}
