using UnityEngine;

[CreateAssetMenu(fileName = "AnimalDefinition", menuName = "Auffi/Animal Definition")]
public class AnimalDefinition : ScriptableObject
{
    [Tooltip("Unique key the book uses to match an animal to its entry.")]
    [SerializeField] private string animalId;
    [SerializeField] private string displayName;
    [Tooltip("Image shown in the book once this animal has been photographed.")]
    [SerializeField] private Sprite photo;

    public string AnimalId => animalId;
    public string DisplayName => displayName;
    public Sprite Photo => photo;
}
