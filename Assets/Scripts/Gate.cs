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
    [Tooltip("Optional lock that drops to the ground as soon as the gate is interacted with.")]
    [SerializeField] private Transform lockObject;

    [SerializeField] private AK.Wwise.Event gateOpened;

    enum OpenDirection
    {
        X,
        Y,
        Z
    }

    private Quaternion _closedRotation;
    private Quaternion _openRotation;
    private bool _isOpen;
    private bool _lockDropped;

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
        player.SetItem(ItemType.None);
        gateOpened?.Post(gameObject);

        if (lockObject != null)
            DropLock();
    }

    private void DropLock()
    {
        if (_lockDropped)
            return;

        _lockDropped = true;

        if (lockObject == null)
            return;

        Transform fallingLock = lockObject;
        Collider[] gateColliders = GetComponentsInChildren<Collider>(true);

        fallingLock.SetParent(null, true);

        Rigidbody body = fallingLock.GetComponent<Rigidbody>();
        if (body == null)
            body = fallingLock.gameObject.AddComponent<Rigidbody>();

        body.isKinematic = false;
        body.useGravity = true;

        foreach (MeshFilter meshFilter in fallingLock.GetComponentsInChildren<MeshFilter>())
        {
            Mesh mesh = meshFilter.sharedMesh;
            if (mesh == null || meshFilter.GetComponent<Collider>() != null)
                continue;

            BoxCollider box = meshFilter.gameObject.AddComponent<BoxCollider>();
            box.center = mesh.bounds.center;
            box.size = mesh.bounds.size;
        }

        foreach (Collider lockCollider in fallingLock.GetComponentsInChildren<Collider>())
        {
            foreach (Collider gateCollider in gateColliders)
            {
                if (gateCollider != null)
                    Physics.IgnoreCollision(lockCollider, gateCollider, true);
            }
        }

        body.AddTorque(fallingLock.right * 0.4f, ForceMode.VelocityChange);
    }

    private void Update()
    {
        if (!_isOpen)
            return;

        door.localRotation = Quaternion.RotateTowards(door.localRotation, _openRotation, openSpeed * Time.deltaTime);
    }
}
