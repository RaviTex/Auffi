using System;
using System.Collections.Generic;
using UnityEngine;

public class AnimalBook : MonoBehaviour
{
    [System.Serializable]
    public class BookEntry
    {
        public AnimalDefinition definition;
        [Tooltip("GameObject shown in the book once this animal has been photographed.")]
        public GameObject entryObject;
    }

    public event Action<AnimalDefinition> AnimalUnlocked;

    [Tooltip("Panel shown and hidden with the book toggle. Leave empty to use this GameObject.")]
    [SerializeField] private GameObject panel;
    [SerializeField] private List<BookEntry> entries = new List<BookEntry>();

    private readonly HashSet<string> _unlocked = new HashSet<string>();

    private GameObject Panel => panel != null ? panel : gameObject;

    public bool IsOpen => Panel.activeSelf;

    public bool IsUnlocked(AnimalDefinition definition)
    {
        return definition != null && !string.IsNullOrEmpty(definition.AnimalId) &&
               _unlocked.Contains(definition.AnimalId);
    }

    private void Awake()
    {
        // The panel may start inactive, so unlocks can happen before this runs.
        foreach (BookEntry entry in entries)
        {
            if (entry.entryObject == null)
                continue;

            bool unlocked = entry.definition != null && _unlocked.Contains(entry.definition.AnimalId);
            entry.entryObject.SetActive(unlocked);
        }
    }

    public void Open()
    {
        Panel.SetActive(true);
    }

    public void Close()
    {
        Panel.SetActive(false);
    }

    public void Toggle()
    {
        Panel.SetActive(!Panel.activeSelf);
    }

    public bool Unlock(AnimalDefinition definition)
    {
        if (definition == null || string.IsNullOrEmpty(definition.AnimalId))
            return false;

        foreach (BookEntry entry in entries)
        {
            if (entry.definition == null || entry.definition.AnimalId != definition.AnimalId)
                continue;

            bool firstUnlock = _unlocked.Add(definition.AnimalId);

            if (entry.entryObject != null)
                entry.entryObject.SetActive(true);

            if (firstUnlock)
                AnimalUnlocked?.Invoke(definition);

            return true;
        }

        return false;
    }
}
