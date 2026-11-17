using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MainMenu : MonoBehaviour
{
    [Header("Buttons (auto-found by name if empty)")]
    public Button startButton;
    public Button exitButton;
    public Button continueButton;
    [Tooltip("The button named \"About\" or \"About?\".")]
    public Button aboutButton;

    [Header("About Panel (auto-found if empty)")]
    [Tooltip("The panel that opens when About is pressed. Found by an object with \"about\" in its name " +
             "(other than the About button itself), e.g. \"AboutPanel\".")]
    public GameObject aboutPanel;
    [Tooltip("The button inside the panel that closes it (e.g. \"Back\", \"Close\", \"X\").")]
    public Button aboutBackButton;
    [Tooltip("The text inside the panel where the About text is written.")]
    public TMP_Text aboutText;
    [Tooltip("Turn off if you'd rather type the About text yourself in the panel's text object.")]
    [SerializeField] private bool writeAboutText = true;
    [SerializeField] private float aboutFadeDuration = 0.25f;

    [TextArea(8, 20)]
    [SerializeField] private string aboutContent =
        "<size=130%><b>HEAVEN 101</b></size>\n" +
        "Created by <b>Shem De Leon</b>\n\n" +
        "One night, you dream that you are standing before the gates of Heaven.\n" +
        "A voice asks you only one question:\n" +
        "<i>\"Why should I open the gates to you?\"</i>\n\n" +
        "You wake up with another chance, and you think you already know the answer:\n" +
        "be good. Help others. Never lie. Count every good deed.\n\n" +
        "But is being good ever enough?\n\n" +
        "Live through three days. Make your choices.\n" +
        "And find out what it truly takes to enter Heaven.";

    [Header("Music (auto-found by object or clip name if empty)")]
    [Tooltip("Plays once when the Main Menu opens. Found by \"opening\" (e.g. OpeningMainmenu).")]
    public AudioSource openingAudioSource;
    [Tooltip("Played one after the other, over and over, while the Main Menu is open (the Acts screen is silent). " +
             "Found by \"angelic\" (e.g. angelicvoice1, angelicvoice2), in name order.")]
    public List<AudioSource> angelicAudioSources = new List<AudioSource>();
    [Tooltip("On: the angelic voices fade in as the opening sound fades out. Off: they start together with it.")]
    [SerializeField] private bool waitForOpeningToFinish = true;
    [Tooltip("At this second of the opening sound it starts fading out, and the first angelic voice starts fading in. " +
             "0 = wait until near the end of the opening.")]
    [SerializeField] private float openingFadeStartTime = 7f;
    [Tooltip("How long the opening sound takes to fade out.")]
    [SerializeField] private float openingFadeOutDuration = 3f;
    [Tooltip("How long the first angelic voice takes to grow from silence to full volume.")]
    [SerializeField] private float firstVoiceFadeInDuration = 4f;
    [Tooltip("Only this many seconds of each angelic voice are played (the files themselves are not changed).")]
    [SerializeField] private float voiceLength = 10f;
    [Tooltip("Seconds where one voice fades out while the next fades in, so there is never a gap or a hard cut.")]
    [SerializeField] private float crossfadeDuration = 2.5f;
    [Tooltip("How long the music takes to fade out when the player leaves the Main Menu (to the Acts screen or an act).")]
    [SerializeField] private float leaveFadeDuration = 1.5f;

    [Header("Scenes")]
    [SerializeField] private string actsSceneName = "Acts";

    private CanvasGroup aboutCanvasGroup;
    private Coroutine aboutFadeCoroutine;
    private Vector3 aboutPanelScale = Vector3.one;

    void Start()
    {
        FindButtons();
        FindAboutPanel();
        FindMusic();
        StartMenuMusic();

        // Every menu button clicks when pressed (a locked Continue button stays silent).
        foreach (Button button in new[] { startButton, exitButton, continueButton, aboutButton, aboutBackButton })
            ButtonClickSound.AddTo(button);

        if (startButton != null)
        {
            startButton.onClick.RemoveListener(StartGame);
            startButton.onClick.AddListener(StartGame);
        }
        else
            Debug.LogWarning("MainMenu: no button named \"Start\" was found.");

        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(ExitGame);
            exitButton.onClick.AddListener(ExitGame);
        }
        else
            Debug.LogWarning("MainMenu: no button named \"Exit\" or \"Exit?\" was found.");

        if (continueButton != null)
        {
            // Only clickable when there is saved progress to go back to.
            continueButton.interactable = CanContinue();
            continueButton.onClick.RemoveListener(ContinueGame);
            continueButton.onClick.AddListener(ContinueGame);
        }

        SetupAbout();
    }

    private void FindButtons()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include);

        foreach (Button button in buttons)
        {
            string buttonName = button.gameObject.name;

            if (startButton == null && buttonName == "Start")
                startButton = button;
            else if (exitButton == null && (buttonName == "Exit" || buttonName == "Exit?"))
                exitButton = button;
            else if (continueButton == null && buttonName == "Continue")
                continueButton = button;
            else if (aboutButton == null && (buttonName == "About" || buttonName == "About?"))
                aboutButton = button;
        }
    }

    // ------------------------------------------------------------------ Music

    // Finds the opening sound and the angelic voices by a word in their object name or clip name.
    private void FindMusic()
    {
        bool findAngelic = angelicAudioSources.Count == 0;

        foreach (AudioSource audioSource in FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
        {
            if (!audioSource.gameObject.scene.IsValid())
                continue;

            if (openingAudioSource == null && SoundMatches(audioSource, "opening"))
                openingAudioSource = audioSource;
            else if (findAngelic && SoundMatches(audioSource, "angelic"))
                angelicAudioSources.Add(audioSource);
        }

        // angelicvoice1 first, then angelicvoice2, and so on.
        if (findAngelic)
            angelicAudioSources.Sort((a, b) => string.CompareOrdinal(SoundName(a), SoundName(b)));

        // Nothing plays by itself; this script decides when each one plays.
        if (openingAudioSource != null)
        {
            openingAudioSource.playOnAwake = false;
            openingAudioSource.loop = false;
            openingAudioSource.Stop();
        }

        foreach (AudioSource voice in angelicAudioSources)
        {
            if (voice == null)
                continue;

            voice.playOnAwake = false;
            voice.loop = false;
            voice.Stop();
        }
    }

    private static bool SoundMatches(AudioSource audioSource, string keyword)
    {
        return SoundName(audioSource).Contains(keyword);
    }

    private static string SoundName(AudioSource audioSource)
    {
        string clipName = audioSource.clip != null ? audioSource.clip.name : "";
        return (audioSource.gameObject.name + " " + clipName).ToLowerInvariant();
    }

    // Hands the music to the MenuMusicPlayer. It plays on the Main Menu only: it fades out when the player goes
    // to the Acts screen (or into an act), and starts again here when they come back (without the opening sound,
    // which plays once per launch).
    private void StartMenuMusic()
    {
        if (MenuMusicPlayer.Instance != null)
        {
            // Still playing (nothing to do), or still fading out from leaving a moment ago: replace it.
            if (!MenuMusicPlayer.Instance.IsLeaving)
                return;
            MenuMusicPlayer.Instance.StopNow();
        }

        List<AudioSource> voices = angelicAudioSources.FindAll(voice => voice != null && voice.clip != null);
        bool hasOpening = openingAudioSource != null && openingAudioSource.clip != null;
        if (!hasOpening && voices.Count == 0)
            return;

        MenuMusicPlayer music = MenuMusicPlayer.Create();
        // Only this scene: the Acts screen is silent.
        music.menuSceneNames = new[] { gameObject.scene.name };
        music.waitForOpeningToFinish = waitForOpeningToFinish;
        music.openingFadeStartTime = openingFadeStartTime;
        music.openingFadeOutDuration = openingFadeOutDuration;
        music.firstVoiceFadeInDuration = firstVoiceFadeInDuration;
        music.voiceLength = voiceLength;
        music.crossfadeDuration = crossfadeDuration;
        music.leaveFadeDuration = leaveFadeDuration;
        music.Play(hasOpening ? openingAudioSource : null, voices);
    }

    // ------------------------------------------------------------------ About

    // Finds the About panel, its close button and its text, unless they were dragged in the Inspector.
    private void FindAboutPanel()
    {
        if (aboutPanel == null)
        {
            // The outermost object with "about" in its name (so "AboutPanel" wins over "AboutText" inside it).
            int bestDepth = int.MaxValue;
            foreach (Transform sceneTransform in FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                GameObject candidate = sceneTransform.gameObject;
                if (!candidate.scene.IsValid() || !candidate.name.ToLowerInvariant().Contains("about"))
                    continue;

                // Skip the About button itself (and anything inside it, like its label).
                if (aboutButton != null && sceneTransform.IsChildOf(aboutButton.transform))
                    continue;
                if (candidate.GetComponent<Button>() != null)
                    continue;

                int depth = 0;
                for (Transform parent = sceneTransform.parent; parent != null; parent = parent.parent)
                    depth++;

                if (depth < bestDepth)
                {
                    bestDepth = depth;
                    aboutPanel = candidate;
                }
            }
        }

        if (aboutPanel == null)
            return;

        if (aboutBackButton == null)
        {
            // Prefer a button with a "back"/"close"/"x" name; otherwise the first button in the panel.
            foreach (Button button in aboutPanel.GetComponentsInChildren<Button>(true))
            {
                string lowerName = button.gameObject.name.ToLowerInvariant();
                if (aboutBackButton == null)
                    aboutBackButton = button;
                if (lowerName.Contains("back") || lowerName.Contains("close") || lowerName == "x")
                {
                    aboutBackButton = button;
                    break;
                }
            }
        }

        if (aboutText == null)
        {
            // The biggest text in the panel that is not a button label (titles are usually smaller).
            float biggestArea = -1f;
            foreach (TMP_Text text in aboutPanel.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.GetComponentInParent<Button>(true) != null)
                    continue;

                Rect rect = text.rectTransform.rect;
                float area = rect.width * rect.height;
                if (area > biggestArea)
                {
                    biggestArea = area;
                    aboutText = text;
                }
            }
        }
    }

    private void SetupAbout()
    {
        if (aboutPanel == null)
        {
            if (aboutButton != null)
                Debug.LogWarning("MainMenu: no About panel was found. Name it with \"About\" (e.g. \"AboutPanel\") or drag it into the About Panel field.");
            return;
        }

        if (aboutPanel.GetComponentInParent<Canvas>(true) == null)
        {
            Debug.LogWarning("MainMenu: \"" + aboutPanel.name + "\" is a sprite, not a UI element inside a Canvas. " +
                             "Its text won't show and it can't be clicked. Make the About panel a UI Image inside the \"Buttons\" Canvas.");
        }
        else
        {
            // Draw the panel above everything else on the menu (logo, clouds and the other buttons).
            Canvas panelCanvas = aboutPanel.GetComponent<Canvas>();
            if (panelCanvas == null)
                panelCanvas = aboutPanel.AddComponent<Canvas>();
            Canvas parentCanvas = aboutPanel.transform.parent != null ? aboutPanel.transform.parent.GetComponentInParent<Canvas>(true) : null;
            panelCanvas.overrideSorting = true;
            if (parentCanvas != null)
                panelCanvas.sortingLayerID = parentCanvas.rootCanvas.sortingLayerID;
            panelCanvas.sortingOrder = 100;
            if (aboutPanel.GetComponent<GraphicRaycaster>() == null)
                aboutPanel.AddComponent<GraphicRaycaster>();
        }

        aboutCanvasGroup = aboutPanel.GetComponent<CanvasGroup>();
        if (aboutCanvasGroup == null)
            aboutCanvasGroup = aboutPanel.AddComponent<CanvasGroup>();
        aboutPanelScale = aboutPanel.transform.localScale;

        if (writeAboutText && aboutText != null)
        {
            // Shrinks to fit if the panel is small (e.g. on a phone), never bigger than the size you set.
            aboutText.enableAutoSizing = true;
            aboutText.fontSizeMax = Mathf.Max(aboutText.fontSize, 18f);
            aboutText.fontSizeMin = 10f;
            aboutText.text = aboutContent;
        }

        aboutPanel.SetActive(false);

        if (aboutButton != null)
        {
            aboutButton.onClick.RemoveListener(OpenAbout);
            aboutButton.onClick.AddListener(OpenAbout);
        }

        if (aboutBackButton != null)
        {
            aboutBackButton.onClick.RemoveListener(CloseAbout);
            aboutBackButton.onClick.AddListener(CloseAbout);
        }
        else
            Debug.LogWarning("MainMenu: the About panel has no button to close it.");
    }

    public void OpenAbout()
    {
        if (aboutPanel == null)
            return;

        aboutPanel.SetActive(true);
        aboutPanel.transform.SetAsLastSibling();
        FadeAbout(1f, null);
    }

    public void CloseAbout()
    {
        if (aboutPanel == null)
            return;

        FadeAbout(0f, () => aboutPanel.SetActive(false));
    }

    private void FadeAbout(float targetAlpha, System.Action onDone)
    {
        if (aboutFadeCoroutine != null)
            StopCoroutine(aboutFadeCoroutine);

        aboutFadeCoroutine = StartCoroutine(FadeAboutRoutine(targetAlpha, onDone));
    }

    // The panel fades in with a tiny "pop", and fades out when closed.
    private IEnumerator FadeAboutRoutine(float targetAlpha, System.Action onDone)
    {
        Transform panelTransform = aboutPanel.transform;
        float startAlpha = targetAlpha > 0f ? 0f : aboutCanvasGroup.alpha;
        Vector3 fullScale = aboutPanelScale;
        Vector3 smallScale = aboutPanelScale * 0.95f;

        aboutCanvasGroup.interactable = targetAlpha > 0f;
        aboutCanvasGroup.blocksRaycasts = targetAlpha > 0f;

        float duration = Mathf.Max(aboutFadeDuration, 0.01f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            aboutCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
            panelTransform.localScale = targetAlpha > 0f
                ? Vector3.Lerp(smallScale, fullScale, progress)
                : Vector3.Lerp(fullScale, smallScale, progress);
            yield return null;
        }

        aboutCanvasGroup.alpha = targetAlpha;
        panelTransform.localScale = fullScale;
        aboutFadeCoroutine = null;
        onDone?.Invoke();
    }

    // ------------------------------------------------------------------ scenes

    // Goes to the Acts screen. There, the act the player is in the middle of continues where they left off,
    // and a finished act asks before starting over (see Acts.cs).
    public void StartGame()
    {
        SceneManager.LoadScene(actsSceneName);
    }

    public void ContinueGame()
    {
        if (CanContinue())
            SceneManager.LoadScene(PlayerDatabase.Data.currentScene);
    }

    // There is a saved place, and its scene is still in the build (an old save could name a removed scene).
    private static bool CanContinue()
    {
        return PlayerDatabase.HasSavedProgress && Application.CanStreamedLevelBeLoaded(PlayerDatabase.Data.currentScene);
    }

    // Closes the game. Application.Quit does nothing inside the Editor, so Play mode is stopped there instead.
    public void ExitGame()
    {
        StartCoroutine(QuitAfterClick());
    }

    // Waits a moment so the click sound is heard before the game closes.
    private IEnumerator QuitAfterClick()
    {
        yield return new WaitForSecondsRealtime(Mathf.Min(ButtonClickSound.Length, 0.4f));

        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
