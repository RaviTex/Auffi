using UnityEngine;

public class Animal : MonoBehaviour
{
    [SerializeField] private AnimalDefinition definition;

    public AnimalDefinition Definition => definition;
}
