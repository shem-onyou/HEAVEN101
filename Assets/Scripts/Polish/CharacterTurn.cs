using System.Collections;
using UnityEngine;

// A character about to leave turns to face the way they are going, then walks off: leaving through the right
// edge, they turn to look right; through the left edge, to look left. The turn is a quick spin around the
// vertical axis (the same 180-degree Y rotation the scenes already use to flip characters), so the picture
// narrows and opens up again, mirrored.
//
// Every character drawing (Elijah, Perzifanie, Nanay, the pulubi) looks to the LEFT, so:
//   rotation Y 0   = looking left,
//   rotation Y 180 = looking right (flipped).
//
// Used by every exit in the scene scripts. Each exit puts the character's rotation back the way it was once
// they are off screen, so if they come back later they walk in facing the same way as before.
public static class CharacterTurn
{
    public const float TurnDuration = 0.18f;

    // side: +1 = leaving through the right edge, -1 = through the left edge. Waits until the turn is done
    // (no wait if they already face that way). Uses game time, so it pauses with the pause menu.
    public static IEnumerator FaceExit(Transform character, float side)
    {
        if (character == null)
            yield break;

        Vector3 euler = character.localEulerAngles;
        float from = LooksRight(euler.y) ? 180f : 0f;
        float to = side > 0f ? 180f : 0f;
        if (Mathf.Approximately(from, to))
            yield break;

        float elapsed = 0f;
        while (elapsed < TurnDuration)
        {
            elapsed += Time.deltaTime;
            euler.y = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / TurnDuration)));
            character.localEulerAngles = euler;
            yield return null;
        }

        euler.y = to;
        character.localEulerAngles = euler;
    }

    // Y angles between 90 and 270 show the picture mirrored (looking right).
    private static bool LooksRight(float yAngle)
    {
        float angle = Mathf.Repeat(yAngle, 360f);
        return angle > 90f && angle < 270f;
    }
}
