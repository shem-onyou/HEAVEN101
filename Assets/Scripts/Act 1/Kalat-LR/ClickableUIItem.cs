using UnityEngine;
using UnityEngine.EventSystems;

// A piece of kalat (clutter) in the living room clean-up. Tapping it tells the scene's controller.
// The file name must stay ClickableUIItem.cs (the same as the class) so Unity can load it on every scene.
public class ClickableUIItem : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        OnItemClicked();
    }

    private void OnItemClicked()
    {
        // The living room controller (Act 1, 2 or 3) plays the pop animation and hides the item itself.
        A1_livingroom act1Controller = FindAnyObjectByType<A1_livingroom>();
        if (act1Controller != null)
        {
            act1Controller.RegisterKalatCleaned(this);
            return;
        }

        A2_Lroom_control act2Controller = FindAnyObjectByType<A2_Lroom_control>();
        if (act2Controller != null)
        {
            act2Controller.RegisterKalatCleaned(this);
            return;
        }

        A3_Lroom_control act3Controller = FindAnyObjectByType<A3_Lroom_control>();
        if (act3Controller != null)
        {
            act3Controller.RegisterKalatCleaned(this);
            return;
        }

        gameObject.SetActive(false);
    }
}