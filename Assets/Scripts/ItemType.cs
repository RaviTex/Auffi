public enum ItemType
{
    None,
    Camera,
    Apples,
    Key
}

public static class ItemTypeRules
{
    // Camera and bucket are indicator states only; apples and the key are real objects.
    public static bool CanPutDown(this ItemType item)
    {
        return item == ItemType.Apples || item == ItemType.Key;
    }

    public static bool CanPickUp(this ItemType item)
    {
        return item == ItemType.Apples || item == ItemType.Key;
    }

    // The key is a quest item, so pick-up interactions use the special item sound.
    public static bool IsSpecial(this ItemType item)
    {
        return item == ItemType.Key;
    }
}
