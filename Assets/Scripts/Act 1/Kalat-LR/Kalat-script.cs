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
        // The living room controller plays the pop animation and hides the item itself.
        A1_livingroom controller = FindAnyObjectByType<A1_livingroom>();
        if (controller != null)
            controller.RegisterKalatCleaned(this);
        else
            gameObject.SetActive(false);
    }
}