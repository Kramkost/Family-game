using UnityEngine;
using UnityEngine.EventSystems;

public class DropTargetSquare : MonoBehaviour, IDropHandler
{
    [Tooltip("Reference to the main minigame manager to report success.")]
    [SerializeField] private RepairUIManager uiManager;

    private bool isFilled = false;

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag != null && !isFilled)
        {
            if (eventData.pointerDrag.TryGetComponent(out DraggableSquare draggable))
            {
                draggable.LockInPlace(GetComponent<RectTransform>());
                isFilled = true;
                
                uiManager.ReportSquareMatched();
            }
        }
    }
}