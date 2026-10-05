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
        // Add your custom logic here (e.g., pick up item, open inventory, play audio)
        
        // Example: Disable the item after picking it up
        gameObject.SetActive(false);
    }
}