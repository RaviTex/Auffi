using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class Animal : MonoBehaviour
{
    [SerializeField] private AnimalDefinition definition;

    [Header("Outline")]
    [Tooltip("Color of the outline shown while this animal can be photographed.")]
    [SerializeField] private Color outlineColor = Color.white;
    [Tooltip("Hull thickness in world units, independent of the animal's scale.")]
    [Range(0.001f, 0.2f)]
    [SerializeField] private float outlineWidth = 0.03f;

    [Header("Audio")] 
    [SerializeField] private AK.Wwise.Event playCowMooing;

    public AnimalDefinition Definition => definition;
    public Color OutlineColor => outlineColor;
    public float OutlineWidth => outlineWidth;
    public bool HasBeenPhotographed { get; private set; }

    private float _mooingTimer;
    private float _timeToMoo;

    public void MarkPhotographed()
    {
        HasBeenPhotographed = true;
    }

    private void Start()
    {
        _timeToMoo = Random.Range(5, 30);
    }

    private void Update()
    {
        _mooingTimer += Time.deltaTime;
        if (_mooingTimer >= _timeToMoo)
        {
            playCowMooing?.Post(gameObject);
            _mooingTimer = 0f;
            _timeToMoo = Random.Range(15, 60);
        }
    }
}
