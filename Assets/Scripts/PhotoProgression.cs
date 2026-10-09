using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class PhotoProgression : MonoBehaviour
{
    [SerializeField] private AnimalBook book;
    [SerializeField] private PlayerController player;
    [Tooltip("Photographing all of these reveals the target.")]
    [SerializeField] private List<AnimalDefinition> requiredAnimals = new List<AnimalDefinition>();
    [Tooltip("Activated once all required animals have been photographed.")]
    [SerializeField] private GameObject revealTarget;
    [Tooltip("Fired right after the target is activated, e.g. to set an AnimalTravel in motion.")]
    [SerializeField] private UnityEvent onReveal = new UnityEvent();
    [Tooltip("Photographing this animal rewards the player.")]
    [SerializeField] private AnimalDefinition rewardAnimal;
    [SerializeField] private ItemType rewardItem = ItemType.Key;

    private bool _revealed;

    private void OnEnable()
    {
        if (book != null)
            book.AnimalUnlocked += OnAnimalUnlocked;
    }

    private void OnDisable()
    {
        if (book != null)
            book.AnimalUnlocked -= OnAnimalUnlocked;
    }

    private void OnAnimalUnlocked(AnimalDefinition definition)
    {
        if (IsRewardAnimal(definition))
        {
            if (player != null)
                player.SetItem(rewardItem);
            return;
        }

        if (!_revealed && AllRequiredPhotographed())
            RevealTarget();
    }

    private void RevealTarget()
    {
        if (_revealed || revealTarget == null)
            return;

        _revealed = true;
        revealTarget.SetActive(true);
        onReveal.Invoke();
    }

    private bool AllRequiredPhotographed()
    {
        if (book == null || requiredAnimals.Count == 0)
            return false;

        foreach (AnimalDefinition animal in requiredAnimals)
        {
            if (animal == null || !book.IsUnlocked(animal))
                return false;
        }

        return true;
    }

    private bool IsRewardAnimal(AnimalDefinition definition)
    {
        return rewardAnimal != null &&
               definition != null &&
               definition.AnimalId == rewardAnimal.AnimalId;
    }
}
