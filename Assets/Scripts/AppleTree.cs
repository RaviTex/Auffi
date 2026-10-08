using UnityEngine;

public class AppleTree : Interactable
{
    public override void OnFocus(PlayerController player)
    {
        // Empty handed: preview the bucket the tree hands out before it gets filled.
        if (player.CurrentItem == ItemType.None)
            player.SetItemPreview(ItemType.Bucket);
    }

    public override void OnUnfocus(PlayerController player)
    {
        player.SetItemPreview(ItemType.None);
    }

    public override void Interact(PlayerController player)
    {
        if (player.CurrentItem != ItemType.None)
            return;

        player.SetItemPreview(ItemType.None);
        player.SetItem(ItemType.Apples);
    }
}
