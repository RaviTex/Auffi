using UnityEngine;

// What the player carries and the inventory slot visuals that show it.
public class PlayerInventory : MonoBehaviour
{
    [Header("Slot Visuals")]
    [Tooltip("Shown when the player holds nothing and no camera is out.")]
    [SerializeField] private GameObject emptySlotVisual;
    [Tooltip("Legacy camera indicator. Kept hidden because the player model animates its own camera.")]
    [SerializeField] private GameObject cameraSlotVisual;
    [Tooltip("Shown while the player carries a bucket filled with apples.")]
    [SerializeField] private GameObject applesSlotVisual;
    [Tooltip("Shown while the player carries the gate key.")]
    [SerializeField] private GameObject keySlotVisual;
    [Tooltip("Parent of the item during holding animation")]
    [SerializeField] private Transform itemHoldingPivot;

    public ItemType CurrentItem { get; private set; } = ItemType.None;
    public bool HasApples => CurrentItem == ItemType.Apples;
    public bool IsHolding => CurrentItem != ItemType.None;
    public bool IsHoldingItem => CurrentItem == ItemType.Apples || CurrentItem == ItemType.Key;

    private Vector3 _cameraRestPosition;
    private Vector3 _applesRestPosition;
    private Vector3 _keyRestPosition;
    private float _pivotRestHeight;
    private bool _hasHoldingPivot;

    private void Awake()
    {
        _cameraRestPosition = GetRestPosition(cameraSlotVisual);
        _applesRestPosition = GetRestPosition(applesSlotVisual);
        _keyRestPosition = GetRestPosition(keySlotVisual);
        _hasHoldingPivot = itemHoldingPivot != null;

        if (_hasHoldingPivot)
            _pivotRestHeight = GetPivotHeight();

        RefreshSlotVisuals();
    }

    private void LateUpdate()
    {
        if (!_hasHoldingPivot)
            return;

        float verticalOffset = GetPivotHeight() - _pivotRestHeight;

        SetHoldingOffset(cameraSlotVisual, _cameraRestPosition, verticalOffset);
        SetHoldingOffset(applesSlotVisual, _applesRestPosition, verticalOffset);
        SetHoldingOffset(keySlotVisual, _keyRestPosition, verticalOffset);
    }

    public void SetItem(ItemType item)
    {
        CurrentItem = item;
        RefreshSlotVisuals();
    }

    private void RefreshSlotVisuals()
    {
        SetSlotVisual(emptySlotVisual, CurrentItem == ItemType.None);
        SetSlotVisual(cameraSlotVisual, false);
        SetSlotVisual(applesSlotVisual, CurrentItem == ItemType.Apples);
        SetSlotVisual(keySlotVisual, CurrentItem == ItemType.Key);
    }

    private static void SetSlotVisual(GameObject visual, bool active)
    {
        if (visual != null && visual.activeSelf != active)
            visual.SetActive(active);
    }

    private void SetHoldingOffset(GameObject visual, Vector3 restPosition, float verticalOffset)
    {
        if (visual == null || visual.transform.parent == null)
            return;

        Vector3 position = visual.transform.parent.TransformPoint(restPosition);
        position.y += verticalOffset;
        visual.transform.position = position;
    }

    private float GetPivotHeight()
    {
        return itemHoldingPivot.position.y - transform.position.y;
    }

    private static Vector3 GetRestPosition(GameObject visual)
    {
        return visual != null ? visual.transform.localPosition : Vector3.zero;
    }
}
