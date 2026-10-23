using UnityEngine;

public class AnimalTravel : MonoBehaviour
{
    [Tooltip("Destination used by TravelToDestination(). Set this per animal in the editor.")] 
    [SerializeField] private Transform destination;

    [SerializeField] private float moveSpeed = 2f;

    [Tooltip("How close to the destination the animal stops (right next to it).")] 
    [SerializeField] private float stopDistance = 1.5f;

    [SerializeField] private float rotationLerpSpeed = 8f;
    
    [Header("Audio")] 
    [SerializeField] private AK.Wwise.Event playCowBells;
    [SerializeField] private AK.Wwise.Event stopCowBells;

    private Transform _target;
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
            return;

        Vector3 toTarget = _target.position - transform.position;
        float distance = toTarget.magnitude;
        float travel = moveSpeed * Time.deltaTime;

        if (distance <= stopDistance)
        {
            Stop();
            return;
        }

        Vector3 direction = toTarget / distance;

        if (distance - travel <= stopDistance)
        {
            transform.position = _target.position - direction * stopDistance;
            Stop();
            return;
        }

        transform.position += direction * travel;

        Vector3 horizontalDirection = new Vector3(direction.x, 0f, direction.z);
        if (horizontalDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(horizontalDirection.normalized, Vector3.up);
            transform.rotation =
                Quaternion.Slerp(transform.rotation, targetRotation, rotationLerpSpeed * Time.deltaTime);
        }
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