using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Act 3, classroom. The player keeps counting deeds during class, until Ma'am gets a phone call:
// Nanay collapsed at home. The friends tell the player to run.
public class A3_Croom_control : A3SceneBase
{
    [Header("Characters")]
    [SerializeField] private string classmate1Name = "Elijah";
    [SerializeField] private string classmate2Name = "Classmate";
    [SerializeField] private string teacherName = "Ma'am";

    [Header("Scene Transition")]
    [SerializeField] private string nextSceneName = "A3_School_uwian";

    [Header("Dialogue")]
    public List<A3Line> classLines = new List<A3Line>();
    public List<A3Line> newsLines = new List<A3Line>();

    private AudioSource phoneSound;
    private AudioSource heartbeatSound;

    protected override void SetupScene()
    {
        A3Character elijah = AddCharacter("classmate1", classmate1Name);
        A3Character classmate = AddCharacter("classmate2", classmate2Name);
        SpreadIfStacked(elijah, classmate);

        phoneSound = FindSound("phone", "ring");
        heartbeatSound = FindSound("beat");

        if (classLines.Count == 0)
        {
            classLines.Add(new A3Line(teacherName, "Okay class, open your books to page forty-two.", "show:classmate1;show:classmate2"));
            classLines.Add(new A3Line(playerName, "(Focus. Take good notes so I can help Elijah later.)", "deed"));
            classLines.Add(new A3Line(classmate1Name, "(Psst... pre, you okay? You keep looking at your phone.)"));
            classLines.Add(new A3Line(playerName, "(Still no reply from Nanay...)"));
            classLines.Add(new A3Line(teacherName, "...Excuse me, class. I need to take this call.", "sfx:phone;pause:1.5"));
            classLines.Add(new A3Line(teacherName, "Hello? ...Yes, this is their teacher."));
            classLines.Add(new A3Line(teacherName, "..."));
            classLines.Add(new A3Line(teacherName, "Anak... can you come here for a moment?"));
        }

        if (newsLines.Count == 0)
        {
            newsLines.Add(new A3Line(teacherName, "That was your neighbor. She called the school."));
            newsLines.Add(new A3Line(teacherName, "Your Nanay collapsed at home.", "heartbeat;shake:14"));
            newsLines.Add(new A3Line(playerName, "...What?"));
            newsLines.Add(new A3Line(playerName, "N-Nanay...?"));
            newsLines.Add(new A3Line(classmate2Name, "Go! Run! We'll tell the other teachers!"));
            newsLines.Add(new A3Line(classmate1Name, "Pre, go! Text us when you get there!"));
        }
    }

    protected override IEnumerator RunScene()
    {
        StartLoop(ambientAudioSource, 1f, 1.5f);
        yield return FadeFromBlack(1.5f);
        yield return new WaitForSeconds(0.3f);

        yield return PlayLines(classLines);
        yield return PlayLines(newsLines);

        // No slow fade: the player just runs.
        yield return FadeOutAndLoad(nextSceneName, "A3_School_uwian", 0.4f);
    }

    protected override IEnumerator PerformCustomAction(string action, string argument)
    {
        if (action == "heartbeat")
        {
            // The classroom noise drops away and the heart pounds.
            if (ambientAudioSource != null) StartCoroutine(FadeAudio(ambientAudioSource, OriginalVolume(ambientAudioSource) * 0.15f, 0.5f));
            StartLoop(heartbeatSound, 1f, 0.3f);
        }

        yield break;
    }
}
