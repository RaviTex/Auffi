using UnityEngine;

// What the player carries and the inventory slot visuals that show it.
public class PlayerInventory : MonoBehaviour
{
    [Header("Slot Visuals")]
    [Tooltip("Shown when the player holds nothing and no camera is out.")]
    [SerializeField] private GameObject emptySlotVisual;
    [Tooltip("Shown when an animal can be photographed.")]
    [SerializeField] private GameObject cameraSlotVisual;
    [Tooltip("Shown while the player carries an empty bucket.")]
    [SerializeField] private GameObject bucketSlotVisual;
    [Tooltip("Shown while the player carries a bucket filled with apples.")]
    [SerializeField] private GameObject applesSlotVisual;
    [Tooltip("Shown while the player carries the gate key.")]
    [SerializeField] private GameObject keySlotVisual;

    private AnimalVisibilitySampler _visibility;

    public ItemType CurrentItem { get; private set; } = ItemType.None;
    public ItemType PreviewItem { get; private set; } = ItemType.None;
    public bool HasApples => CurrentItem == ItemType.Apples;

    private void Awake()
    {
        _visibility = GetComponent<AnimalVisibilitySampler>();
    }

    private void LateUpdate()
    {
        bool photoAvailable = CurrentItem == ItemType.None && _visibility.TryGetVisibleAnimal(out _, out _);

        ItemType state = CurrentItem != ItemType.None
            ? CurrentItem
            : photoAvailable
                ? ItemType.Camera
                : PreviewItem != ItemType.None
                    ? PreviewItem
                    : ItemType.None;

        SetSlotVisual(emptySlotVisual, state == ItemType.None);
        SetSlotVisual(cameraSlotVisual, state == ItemType.Camera);
        SetSlotVisual(bucketSlotVisual, state == ItemType.Bucket);
        SetSlotVisual(applesSlotVisual, state == ItemType.Apples);
        SetSlotVisual(keySlotVisual, state == ItemType.Key);
    }

    public void SetItem(ItemType item)
    {
        CurrentItem = item;
    }

    public void SetItemPreview(ItemType item)
    {
        PreviewItem = item;
    }

    private static void SetSlotVisual(GameObject visual, bool active)
    {
        if (visual != null && visual.activeSelf != active)
            visual.SetActive(active);
    }
}
