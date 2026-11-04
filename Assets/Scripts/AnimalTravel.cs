using UnityEngine;

public class AnimalTravel : MonoBehaviour
{
    [Tooltip("Destination used by TravelToDestination(). Set this per animal in the editor.")] 
    [SerializeField] private Transform destination;

    [SerializeField] private float moveSpeed = 2f;

    [Tooltip("How close to the destination the animal stops (right next to it).")] 
    [SerializeField] private float stopDistance = 1.5f;

    [Header("Ground Lock")]
    [Tooltip("Layers considered ground. Defaults to the Ground layer.")]
    [SerializeField] private LayerMask groundMask = 1 << 7;
    [Tooltip("How far above the animal the ground ray starts.")]
    [SerializeField] private float groundCheckHeight = 5f;
    [Tooltip("How far below the animal the ground ray reaches.")]
    [SerializeField] private float groundCheckDistance = 10f;
    [Tooltip("Vertical offset from the ground hit point.")]
    [SerializeField] private float groundOffset = 0f;
    [Tooltip("How quickly the animal aligns its up axis to the ground normal.")]
    [SerializeField] private float groundRotationLerpSpeed = 8f;

    [Header("Audio")] 
    [SerializeField] private AK.Wwise.Event playCowBells;
    [SerializeField] private AK.Wwise.Event stopCowBells;

    private Transform _target;
    private Vector3 _desiredForward = Vector3.forward;
    private bool _cowBellsPlaying;

    public bool IsTraveling => _target != null;

    public void TravelToDestination()
    {
        TravelTo(destination);
    }

    public void TravelTo(Transform target)
    {
        if (target == null)
        {
            Stop();
            return;
        }

        if (!IsTraveling)
            StartCowBells();

        _target = target;
    }

    public void Stop()
    {
        _target = null;
        StopCowBells();
    }

    private void Update()
    {
        if (_target == null)
        {
            _desiredForward = transform.forward;
            return;
        }

        Vector3 toTarget = _target.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        float travel = moveSpeed * Time.deltaTime;

        if (distance <= stopDistance)
        {
            _desiredForward = transform.forward;
            Stop();
            return;
        }

        Vector3 direction = toTarget / distance;
        _desiredForward = direction;

        if (distance - travel <= stopDistance)
        {
            Vector3 finalPosition = _target.position - direction * stopDistance;
            finalPosition.y = transform.position.y;
            transform.position = finalPosition;
            Stop();
            return;
        }

        transform.position += direction * travel;
    }

    private void LateUpdate()
    {
        LockToGround();
    }

    private void LockToGround()
    {
        Vector3 origin = transform.position + Vector3.up * groundCheckHeight;
        float maxDistance = groundCheckHeight + groundCheckDistance;

        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxDistance, groundMask,
                QueryTriggerInteraction.Ignore))
            return;

        Vector3 position = transform.position;
        position.y = hit.point.y + groundOffset;
        transform.position = position;

        Vector3 forward = Vector3.ProjectOnPlane(_desiredForward, hit.normal);
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.ProjectOnPlane(transform.forward, hit.normal);
        if (forward.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(forward.normalized, hit.normal);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation,
            groundRotationLerpSpeed * Time.deltaTime);
    }

    private void OnDisable()
    {
        _target = null;
        StopCowBells();
    }

    private void StartCowBells()
    {
        if (_cowBellsPlaying || playCowBells == null)
            return;

        _cowBellsPlaying = true; 
        playCowBells.Post(gameObject);
    }

    private void StopCowBells()
    {
        if (!_cowBellsPlaying || stopCowBells == null)
            return;
        _cowBellsPlaying = false;
        stopCowBells.Post(gameObject);
    }
}