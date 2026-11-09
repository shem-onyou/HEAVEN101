using UnityEngine;
using UnityEngine.EventSystems;

public class ClickableUIItem : MonoBehaviour, IPointerClickHandler
{
    // Implementation of IPointerClickHandler interface
    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("UI Item Clicked: " + gameObject.name);
        
        // Call your custom function here
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