public enum ItemType
{
    None,
    Camera,
    Bucket,
    Apples
}

public static class ItemTypeRules
{
    // Camera and bucket are indicator states only; apples are the only real object right now.
    public static bool CanPutDown(this ItemType item)
    {
        return item == ItemType.Apples;
    }

    public static bool CanPickUp(this ItemType item)
    {
        return item == ItemType.Apples;
    }
}
