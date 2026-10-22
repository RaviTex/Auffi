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
    [Tooltip("Item already sitting on this pivot when the scene starts. Use None for an empty pivot.")]
    [SerializeField] private ItemType startingItem = ItemType.None;
    [Tooltip("Activated while an item sits on the pivot.")]
    [SerializeField] private GameObject placedVisual;
    [Tooltip("Fired when a matching item is put down on this pivot.")]
    [SerializeField] private ItemPutDownEvent onItemPutDown = new ItemPutDownEvent();
    [Tooltip("When enabled, only the first successful pick-up or put-down uses this pivot. " +
             "Failed attempts (empty-handed, wrong item, full hands) do not count.")]
    [SerializeField] private bool allowSingleInteraction;

    [Header("Sounds")] 
    [SerializeField] private AK.Wwise.Event pickUpSound;
    
    private ItemType _placedItem = ItemType.None;
    private bool _interactionUsed;

    private void Awake()
    {
        if (startingItem != ItemType.None && !startingItem.CanPickUp())
        {
            Debug.LogWarning($"[PutDownPivot] Starting item {startingItem} cannot be picked up. " +
                             "The pivot starts empty.", this);
            startingItem = ItemType.None;
        }

        SetPlacedItem(startingItem);
    }

    public override bool IsInteractionAvailable => !allowSingleInteraction || !_interactionUsed;

    public override void Interact(PlayerController player)
    {
        if (!IsInteractionAvailable)
            return;

        bool succeeded = _placedItem != ItemType.None ? TryPickUp(player) : TryPutDown(player);

        if (succeeded && allowSingleInteraction)
            _interactionUsed = true;
    }

    private bool TryPutDown(PlayerController player)
    {
        ItemType item = player.CurrentItem;
        if (!item.CanPutDown() || !acceptedItems.Contains(item))
            return false;

        player.SetItem(ItemType.None);
        SetPlacedItem(item);

        onItemPutDown.Invoke(item);
        return true;
    }

    private bool TryPickUp(PlayerController player)
    {
        if (player.CurrentItem != ItemType.None || !_placedItem.CanPickUp())
            return false;

        player.SetItem(_placedItem);
        SetPlacedItem(ItemType.None);
        pickUpSound?.Post(gameObject);
        return true;
    }

    private void SetPlacedItem(ItemType item)
    {
        _placedItem = item;

        if (placedVisual != null)
            placedVisual.SetActive(item != ItemType.None);
    }
}
