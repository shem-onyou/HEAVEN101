using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Act 3, morning in the bedroom. God's last question still echoes. The player decides to do more good
// than ever and to count every deed, and the Good Deeds counter appears for the first time.
public class A3_Broom_control : A3SceneBase
{
    [Header("Act Title")]
    [SerializeField] private string actTitle = "ACT 3";
    [SerializeField] private string actSubtitle = "Enough?";
    [SerializeField] private string echoLine = "\"When you fall short...\n...what then?\"";

    [Header("Scene Transition")]
    [SerializeField] private string nextSceneName = "A3_Lroom";

    [Header("Dialogue")]
    public List<A3Line> morningLines = new List<A3Line>();

    private AudioSource fanSound;
    private AudioSource birdsSound;
    private AudioSource heartbeatSound;
    private AudioSource clothesSound;
    private AudioSource doorSound;

    protected override bool CounterVisibleAtStart => false;

    protected override void SetupScene()
    {
        // A new Act 3 starts from zero.
        A3GoodDeeds.Reset();

        GameObject lord = FindSceneObject("Lord");
        if (lord != null) lord.SetActive(false);

        fanSound = FindSound("fan");
        birdsSound = FindSound("bird");
        heartbeatSound = FindSound("beat");
        clothesSound = FindSound("cloth");
        doorSound = FindSound("door");

        if (morningLines.Count > 0)
            return;

        morningLines.Add(new A3Line(playerName, "(...)"));
        morningLines.Add(new A3Line(playerName, "(I barely slept. That question kept coming back.)"));
        morningLines.Add(new A3Line(playerName, "(\"When you fall short... what then?\")"));
        morningLines.Add(new A3Line(playerName, "(...Then I just won't fall short.)"));
        morningLines.Add(new A3Line(playerName, "(Today, I'll do more good than ever. And I'll count every single one.)", "counter"));
        morningLines.Add(new A3Line(playerName, "(First... fix my bed. That's one.)", "deed"));
        morningLines.Add(new A3Line("Nanay", "*cough* *cough*... Anak? Are you awake?", "sfx:cough"));
        morningLines.Add(new A3Line(playerName, "Opo, Nay! Coming!"));
        morningLines.Add(new A3Line(playerName, "(Her cough sounds worse today...)"));
        morningLines.Add(new A3Line(playerName, "(Uniform. Fold my clothes. Two... three.)", "dress;deed;deed"));
    }

    protected override IEnumerator RunScene()
    {
        // The question from Act 2 echoes in the dark with a heartbeat.
        StartLoop(heartbeatSound, 0.7f, 1f);
        yield return new WaitForSeconds(1f);
        yield return ShowCenterText(echoLine, 40f, 2f);
        if (heartbeatSound != null) StartCoroutine(FadeAudio(heartbeatSound, 0f, 1.5f));
        yield return new WaitForSeconds(0.8f);

        yield return ShowTitleCard(actTitle, actSubtitle);

        StartLoop(fanSound, 1f, 1.5f);
        StartLoop(birdsSound, 0.7f, 1.5f);
        StartLoop(ambientAudioSource, 1f, 1.5f);
        yield return FadeFromBlack(1.5f);
        yield return new WaitForSeconds(0.4f);

        yield return PlayLines(morningLines);
        yield return FadeOutAndLoad(nextSceneName, "A3_Lroom", 1.2f, doorSound);
    }

    protected override IEnumerator PerformCustomAction(string action, string argument)
    {
        if (action == "dress")
        {
            // A quick dip to black while the clothes rustle.
            HideDialogueBox();
            yield return FadeToBlack(0.5f);
            PlayEffect(clothesSound);
            yield return new WaitForSeconds(EffectLength(clothesSound, 1f, 2f));
            yield return FadeOutCover(0.5f);
        }
    }
}
