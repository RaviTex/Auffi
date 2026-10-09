using UnityEngine;

public class Gate : Interactable
{
    [Tooltip("The part of the gate that swings. Its pivot should be on the hinge.")]
    [SerializeField] private Transform door;
    [Tooltip("Swing angle in degrees, around the parent's Y axis.")]
    [SerializeField] private float openAngle = -90f;
    [Tooltip("Swing speed in degrees per second.")]
    [SerializeField] private float openSpeed = 90f;
    [Tooltip("Swing Direction")]
    [SerializeField] private OpenDirection openDirection;
    
    enum OpenDirection
    {
        X,
        Y,
        Z
    }

    private Quaternion _closedRotation;
    private Quaternion _openRotation;
    private bool _isOpen;

    private void Awake()
    {
        if (door == null)
            door = transform;

        _closedRotation = door.localRotation;
        if(openDirection == OpenDirection.X)
            _openRotation = Quaternion.Euler(openAngle, 0f, 0f) * _closedRotation;
        if(openDirection == OpenDirection.Y)
            _openRotation = Quaternion.Euler(0f, openAngle, 0f) * _closedRotation;
        if(openDirection == OpenDirection.Z)
            _openRotation = Quaternion.Euler(0f, 0f, openAngle) * _closedRotation;
    }

    public override void Interact(PlayerController player)
    {
        if (_isOpen || player.CurrentItem != ItemType.Key)
            return;

        _isOpen = true;
    }

    private void Update()
    {
        if (!_isOpen)
            return;

        door.localRotation = Quaternion.RotateTowards(door.localRotation, _openRotation, openSpeed * Time.deltaTime);
    }
}
