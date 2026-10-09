using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class ItemPutDownEvent : UnityEvent<ItemType>
{
}

public class PutDownPivot : Interactable
{
    [Tooltip("Items this pivot accepts. Only placeable items (apples) can be put down.")]
    [SerializeField] private List<ItemType> acceptedItems = new List<ItemType>();
    [Tooltip("Activated while an item sits on the pivot.")]
    [SerializeField] private GameObject placedVisual;
    [Tooltip("Fired when a matching item is put down on this pivot.")]
    [SerializeField] private ItemPutDownEvent onItemPutDown = new ItemPutDownEvent();

    private ItemType _placedItem = ItemType.None;

    public override void Interact(PlayerController player)
    {
        if (_placedItem != ItemType.None)
            TryPickUp(player);
        else
            TryPutDown(player);
    }

    private void TryPutDown(PlayerController player)
    {
        ItemType item = player.CurrentItem;
        if (!item.CanPutDown() || !acceptedItems.Contains(item))
            return;

        player.SetItem(ItemType.None);
        SetPlacedItem(item);

        onItemPutDown.Invoke(item);
    }

    private void TryPickUp(PlayerController player)
    {
        if (player.CurrentItem != ItemType.None || !_placedItem.CanPickUp())
            return;

        player.SetItem(_placedItem);
        SetPlacedItem(ItemType.None);
    }

    private void SetPlacedItem(ItemType item)
    {
        _placedItem = item;

        if (placedVisual != null)
            placedVisual.SetActive(item != ItemType.None);
    }
}
