using UnityEngine;
using TMPro;

// Put this on a TMP text in the ending scene to show everything the player chose and did.
public class PlayerSummaryDisplay : MonoBehaviour
{
    public TMP_Text summaryText;

    void Start()
    {
        if (summaryText == null)
            summaryText = GetComponent<TMP_Text>();

        if (summaryText != null)
            summaryText.text = PlayerDatabase.BuildSummary();
    }
}
