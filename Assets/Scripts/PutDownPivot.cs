using System.Collections;
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

    [Header("Accepted Feedback")]
    [Tooltip("Show feedback when an interaction succeeds.")]
    [SerializeField] private bool enableAcceptedFeedback = true;
    [Tooltip("Sprite spawned above the pivot when an interaction succeeds.")]
    [SerializeField] private Sprite acceptedFeedbackSprite;

    [Header("Failed Feedback")]
    [Tooltip("Show feedback when an interaction fails.")]
    [SerializeField] private bool enableFailedFeedback = true;
    [Tooltip("Sprite spawned above the pivot when an interaction fails.")]
    [SerializeField] private Sprite failedFeedbackSprite;

    [Header("Highlight Hint")]
    [Tooltip("Show a hint above the pivot while it is highlighted.")]
    [SerializeField] private bool enableHighlightHint = true;
    [Tooltip("Sprite shown above the pivot while it is highlighted.")]
    [SerializeField] private Sprite highlightHintSprite;

    [Header("Feedback Motion")]
    [Tooltip("World-space height above the pivot where feedback appears.")]
    [SerializeField] private float feedbackSpawnHeight = 2f;
    [SerializeField] private float feedbackRiseDistance = 1f;
    [SerializeField] private float feedbackDuration = 1.2f;
    [SerializeField] private float feedbackScale = 1f;

    private ItemType _placedItem = ItemType.None;
    private bool _interactionUsed;
    private GameObject _hintObject;

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

    private void OnDisable()
    {
        HideHint();
    }

    public override bool IsInteractionAvailable => !allowSingleInteraction || !_interactionUsed;

    public override void Interact(PlayerController player)
    {
        if (!IsInteractionAvailable)
            return;

        bool succeeded = _placedItem != ItemType.None ? TryPickUp(player) : TryPutDown(player);

        if (succeeded)
        {
            if (allowSingleInteraction)
                _interactionUsed = true;

            ShowFeedback(enableAcceptedFeedback, acceptedFeedbackSprite, "Accepted Feedback");
        }
        else
        {
            ShowFeedback(enableFailedFeedback, failedFeedbackSprite, "Failed Feedback");
        }
    }

    public override void OnFocus(PlayerController player)
    {
        ShowHint();
    }

    public override void OnUnfocus(PlayerController player)
    {
        HideHint();
    }

    private void LateUpdate()
    {
        if (_hintObject == null)
            return;

        Camera cam = Camera.main;
        Quaternion rotation = cam != null ? cam.transform.rotation : Quaternion.identity;
        Vector3 anchor = transform.position + Vector3.up * feedbackSpawnHeight;
        Vector3 pivotOffset = highlightHintSprite.bounds.center * feedbackScale;

        _hintObject.transform.SetPositionAndRotation(anchor - rotation * pivotOffset, rotation);
    }

    private void ShowFeedback(bool enabled, Sprite sprite, string objectName)
    {
        if (!enabled || sprite == null)
            return;

        StartCoroutine(PlayRiseAndFade(sprite, objectName));
    }

    private IEnumerator PlayRiseAndFade(Sprite sprite, string objectName)
    {
        Camera cam = Camera.main;
        GameObject feedback = CreateSpriteObject(sprite, objectName);
        SpriteRenderer spriteRenderer = feedback.GetComponent<SpriteRenderer>();

        Vector3 anchor = transform.position + Vector3.up * feedbackSpawnHeight;
        Vector3 pivotOffset = sprite.bounds.center * feedbackScale;
        Quaternion rotation = cam != null ? cam.transform.rotation : Quaternion.identity;

        float elapsed = 0f;
        while (elapsed < feedbackDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / feedbackDuration);

            if (cam != null)
                rotation = cam.transform.rotation;

            feedback.transform.SetPositionAndRotation(
                anchor + Vector3.up * (feedbackRiseDistance * t) - rotation * pivotOffset,
                rotation);

            Color color = spriteRenderer.color;
            color.a = 1f - t;
            spriteRenderer.color = color;

            yield return null;
        }

        Destroy(feedback);
    }

    private void ShowHint()
    {
        if (!enableHighlightHint || highlightHintSprite == null || _hintObject != null)
            return;

        _hintObject = CreateSpriteObject(highlightHintSprite, "Highlight Hint");
    }

    private void HideHint()
    {
        if (_hintObject == null)
            return;

        Destroy(_hintObject);
        _hintObject = null;
    }

    private GameObject CreateSpriteObject(Sprite sprite, string objectName)
    {
        GameObject spriteObject = new GameObject(objectName);
        SpriteRenderer spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        spriteRenderer.sortingOrder = 100;
        spriteObject.transform.localScale = Vector3.one * feedbackScale;
        return spriteObject;
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
        if (player.IsHoldingItem || !_placedItem.CanPickUp())
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
