using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Small touches that make the dialogue box feel alive, in every story scene, without changing the scenes:
//  - the box pops in softly whenever it appears;
//  - the speaker's name gives a small bounce when a different character starts talking;
//  - the Next arrow is dim while a line is typing, and bobs once the line is complete ("tap to continue");
//  - text effects in dialogue lines: <link="shake">WAKE UP!</link> trembles, <link="wave">...</link> floats.
//    The link tags are invisible, and the effect only plays in the dialogue box.
//
// Added by itself to the object named "DialogueBox" when a scene loads. It only changes scale, alpha,
// the arrow's position and color, and the text's vertices, so the scene scripts keep full control
// of showing, hiding and moving the box (shakes, phone buzz).
public class DialogueBoxPolish : MonoBehaviour
{
    private const float PopDuration = 0.18f;
    private const float PopStartScale = 0.94f;
    private const float PopFadeDuration = 0.1f;
    private const float NamePopDuration = 0.22f;
    private const float NamePopAmount = 0.14f;
    private const float ArrowBobHeight = 3f;
    private const float ArrowBobSpeed = 1.6f;
    private const float ArrowTypingAlpha = 0.4f;

    private bool initialized;
    private Vector3 boxScale = Vector3.one;
    private CanvasGroup group;
    private float popAge = 99f;

    private TMP_Text nameText;
    private Vector3 nameScale = Vector3.one;
    private string lastName;
    private float nameAge = 99f;

    private TMP_Text messageText;

    private Transform arrow;
    private SpriteRenderer arrowSprite;
    private Graphic arrowGraphic;
    private Vector3 arrowRest;
    private Color arrowColor = Color.white;
    private float arrowTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        GameObject box = PolishUI.FindInScene(scene, "DialogueBox");
        if (box != null && box.GetComponent<DialogueBoxPolish>() == null)
            box.AddComponent<DialogueBoxPolish>();
    }

    private void Initialize()
    {
        if (initialized)
            return;

        initialized = true;
        boxScale = transform.localScale;

        nameText = FindText("CharacterName");
        messageText = FindText("MessageOutput");
        if (nameText != null)
            nameScale = nameText.transform.localScale;

        // The Next button is the first Button in the box (the scene scripts find it the same way).
        Button next = GetComponentInChildren<Button>(true);
        if (next != null)
        {
            arrow = next.transform;
            arrowRest = arrow.localPosition;
            arrowSprite = next.GetComponent<SpriteRenderer>();
            arrowGraphic = arrowSprite == null ? next.GetComponent<Graphic>() : null;
            if (arrowSprite != null) arrowColor = arrowSprite.color;
            else if (arrowGraphic != null) arrowColor = arrowGraphic.color;
        }

        group = GetComponent<CanvasGroup>();
        if (group == null)
            group = gameObject.AddComponent<CanvasGroup>();
    }

    private TMP_Text FindText(string objectName)
    {
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.gameObject.name == objectName)
                return text;
        }

        return null;
    }

    private void OnEnable()
    {
        Initialize();
        popAge = 0f;
        transform.localScale = boxScale * PopStartScale;
        group.alpha = 0f;
    }

    private void OnDisable()
    {
        if (!initialized)
            return;

        transform.localScale = boxScale;
        if (group != null) group.alpha = 1f;
        if (nameText != null) nameText.transform.localScale = nameScale;
        ResetArrow();
        lastName = null;
    }

    private void LateUpdate()
    {
        float deltaTime = Time.deltaTime;
        UpdatePop(deltaTime);
        UpdateName(deltaTime);
        UpdateArrow(deltaTime);
        UpdateTextEffects();
    }

    // ------------------------------------------------------------------ box pop

    private void UpdatePop(float deltaTime)
    {
        if (popAge > PopDuration)
            return;

        popAge += deltaTime;
        float progress = Mathf.Clamp01(popAge / PopDuration);
        transform.localScale = boxScale * Mathf.LerpUnclamped(PopStartScale, 1f, EaseOutBack(progress));
        group.alpha = Mathf.Clamp01(popAge / PopFadeDuration);

        if (progress >= 1f)
        {
            transform.localScale = boxScale;
            group.alpha = 1f;
        }
    }

    private static float EaseOutBack(float progress)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float p = progress - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }

    // ------------------------------------------------------------------ speaker name

    private void UpdateName(float deltaTime)
    {
        if (nameText == null)
            return;

        string current = nameText.text;
        if (current != lastName)
        {
            lastName = current;
            nameAge = string.IsNullOrEmpty(current) ? NamePopDuration : 0f;
        }

        if (nameAge >= NamePopDuration)
            return;

        nameAge += deltaTime;
        float progress = Mathf.Clamp01(nameAge / NamePopDuration);
        nameText.transform.localScale = nameScale * (1f + NamePopAmount * Mathf.Sin(progress * Mathf.PI));
        if (progress >= 1f)
            nameText.transform.localScale = nameScale;
    }

    // ------------------------------------------------------------------ Next arrow

    private void UpdateArrow(float deltaTime)
    {
        if (arrow == null)
            return;

        bool lineComplete = IsLineComplete();
        if (lineComplete)
        {
            arrowTime += deltaTime;
            float bob = Mathf.Abs(Mathf.Sin(arrowTime * Mathf.PI * ArrowBobSpeed)) * ArrowBobHeight;
            arrow.localPosition = arrowRest + new Vector3(0f, bob, 0f);
            SetArrowAlpha(arrowColor.a);
        }
        else
        {
            arrowTime = 0f;
            arrow.localPosition = arrowRest;
            SetArrowAlpha(arrowColor.a * ArrowTypingAlpha);
        }
    }

    private bool IsLineComplete()
    {
        if (messageText == null)
            return true;

        TMP_TextInfo info = messageText.textInfo;
        int count = info != null ? info.characterCount : 0;
        return count == 0 || messageText.maxVisibleCharacters >= count;
    }

    private void SetArrowAlpha(float alpha)
    {
        Color color = arrowColor;
        color.a = alpha;
        if (arrowSprite != null) arrowSprite.color = color;
        else if (arrowGraphic != null) arrowGraphic.color = color;
    }

    private void ResetArrow()
    {
        if (arrow == null)
            return;

        arrow.localPosition = arrowRest;
        SetArrowAlpha(arrowColor.a);
        arrowTime = 0f;
    }

    // ------------------------------------------------------------------ text effects

    // Moves the letters inside <link="shake"> or <link="wave"> every frame. Lines without links cost nothing.
    private void UpdateTextEffects()
    {
        if (messageText == null || !messageText.isActiveAndEnabled)
            return;

        TMP_TextInfo info = messageText.textInfo;
        if (info == null || info.linkCount == 0)
            return;

        // Rebuild the letters in their normal places, then move them.
        messageText.ForceMeshUpdate();
        info = messageText.textInfo;

        float time = Time.time;
        int visible = messageText.maxVisibleCharacters;
        bool moved = false;

        for (int l = 0; l < info.linkCount; l++)
        {
            TMP_LinkInfo link = info.linkInfo[l];
            string id = link.GetLinkID();
            bool shake = id == "shake";
            bool wave = id == "wave";
            if (!shake && !wave)
                continue;

            int first = link.linkTextfirstCharacterIndex;
            int last = Mathf.Min(first + link.linkTextLength, info.characterCount);
            for (int c = first; c < last; c++)
            {
                if (c >= visible || c < 0 || c >= info.characterInfo.Length)
                    break;

                TMP_CharacterInfo character = info.characterInfo[c];
                if (!character.isVisible)
                    continue;

                Vector3 offset;
                if (shake)
                {
                    // Each letter trembles on its own.
                    offset = new Vector3(
                        (Mathf.PerlinNoise(c * 1.7f, time * 22f) - 0.5f) * 3.2f,
                        (Mathf.PerlinNoise(time * 22f, c * 2.3f) - 0.5f) * 3.2f, 0f);
                }
                else
                {
                    offset = new Vector3(0f, Mathf.Sin(time * 5f + c * 0.55f) * 2.2f, 0f);
                }

                int materialIndex = character.materialReferenceIndex;
                int vertexIndex = character.vertexIndex;
                Vector3[] vertices = info.meshInfo[materialIndex].vertices;
                if (vertices == null || vertexIndex + 3 >= vertices.Length)
                    continue;

                vertices[vertexIndex] += offset;
                vertices[vertexIndex + 1] += offset;
                vertices[vertexIndex + 2] += offset;
                vertices[vertexIndex + 3] += offset;
                moved = true;
            }
        }

        if (moved)
            messageText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    }
}
