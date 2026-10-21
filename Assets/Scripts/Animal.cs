using UnityEngine;

public class Animal : MonoBehaviour
{
    [SerializeField] private AnimalDefinition definition;

    [Header("Outline")]
    [Tooltip("Color of the outline shown while this animal can be photographed.")]
    [SerializeField] private Color outlineColor = Color.white;
    [Tooltip("Hull thickness in world units, independent of the animal's scale.")]
    [Range(0.001f, 0.2f)]
    [SerializeField] private float outlineWidth = 0.03f;

    public AnimalDefinition Definition => definition;
    public Color OutlineColor => outlineColor;
    public float OutlineWidth => outlineWidth;
}
