using System.Collections.Generic;
using UnityEngine;

public class PutDownPivot : Interactable
{
    [Tooltip("Items this pivot accepts. Anything else is refused.")]
    [SerializeField] private List<ItemType> acceptedItems = new List<ItemType>();
    [Tooltip("Activated when an item is placed on the pivot.")]
    [SerializeField] private GameObject placedVisual;

    public override void Interact(PlayerController player)
    {
        ItemType item = player.CurrentItem;
        if (item == ItemType.None || !acceptedItems.Contains(item))
            return;

        player.SetItem(ItemType.None);

        if (placedVisual != null)
            placedVisual.SetActive(true);
    }
}
