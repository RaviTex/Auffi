using System.Collections.Generic;
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
    [Tooltip("Vertical offset from the ground hit point. Only used when no foot contacts can be resolved.")]
    [SerializeField] private float groundOffset = 0f;
    [Tooltip("How quickly the animal aligns its up axis to the ground normal.")]
    [SerializeField] private float groundRotationLerpSpeed = 8f;

    [Header("Foot Solver")]
    [Tooltip("Feet are detected from the lowest renderers under this animal. Increase if a foot is missed, decrease if non-foot geometry is included.")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float footHeightFraction = 0.1f;
    [Tooltip("Foot pieces closer than this fraction of the animal's height are merged into one contact point.")]
    [Range(0.001f, 0.25f)]
    [SerializeField] private float footMergeFraction = 0.05f;

    [Header("Audio")] 
    [SerializeField] private AK.Wwise.Event playCowBells;
    [SerializeField] private AK.Wwise.Event stopCowBells;

    private struct FootContact
    {
        public Vector3 LocalPoint;
        public RaycastHit Hit;
    }

    private Transform _target;
    private Vector3 _desiredForward = Vector3.forward;
    private bool _cowBellsPlaying;

    private readonly List<Vector3> _footPoints = new List<Vector3>();
    private readonly List<FootContact> _footContacts = new List<FootContact>();
    private bool _feetInitialized;

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

    private void Start()
    {
        DetectFeet();
    }

    private void LateUpdate()
    {
        LockToGround();
    }

    private void LockToGround()
    {
        Vector3 origin = transform.position + Vector3.up * groundCheckHeight;
        float maxDistance = groundCheckHeight + groundCheckDistance;

        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit centerHit, maxDistance, groundMask,
                QueryTriggerInteraction.Ignore))
            return;

        if (!_feetInitialized)
            DetectFeet();

        if (_footPoints.Count < 2 || !TrySampleFeet(maxDistance))
        {
            ApplyGroundHit(centerHit);
            return;
        }

        Vector3 groundNormal = Vector3.zero;
        foreach (FootContact contact in _footContacts)
            groundNormal += contact.Hit.normal;

        if (groundNormal.sqrMagnitude < 0.0001f)
        {
            ApplyGroundHit(centerHit);
            return;
        }

        groundNormal.Normalize();

        Vector3 forward = Vector3.ProjectOnPlane(_desiredForward, groundNormal);
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.ProjectOnPlane(transform.forward, groundNormal);
        if (forward.sqrMagnitude < 0.0001f)
        {
            ApplyGroundHit(centerHit);
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(forward.normalized, groundNormal);
        Quaternion rotation = Quaternion.Slerp(transform.rotation, targetRotation,
            groundRotationLerpSpeed * Time.deltaTime);

        Vector3 lossyScale = transform.lossyScale;
        float targetY = float.NegativeInfinity;

        foreach (FootContact contact in _footContacts)
        {
            Vector3 footOffset = rotation * Vector3.Scale(contact.LocalPoint, lossyScale);
            targetY = Mathf.Max(targetY, contact.Hit.point.y - footOffset.y);
        }

        if (float.IsNegativeInfinity(targetY))
        {
            ApplyGroundHit(centerHit);
            return;
        }

        Vector3 position = transform.position;
        position.y = targetY;
        transform.position = position;
        transform.rotation = rotation;
    }

    private void ApplyGroundHit(RaycastHit hit)
    {
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

    private bool TrySampleFeet(float maxDistance)
    {
        _footContacts.Clear();
        float rayOriginHeight = transform.position.y + groundCheckHeight;

        foreach (Vector3 footPoint in _footPoints)
        {
            Vector3 worldFoot = transform.TransformPoint(footPoint);
            Vector3 origin = new Vector3(worldFoot.x, rayOriginHeight, worldFoot.z);

            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxDistance, groundMask,
                    QueryTriggerInteraction.Ignore))
                continue;

            _footContacts.Add(new FootContact
            {
                LocalPoint = footPoint,
                Hit = hit
            });
        }

        return _footContacts.Count >= 2;
    }

    private void DetectFeet()
    {
        _feetInitialized = true;
        _footPoints.Clear();

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return;

        List<Bounds> localBounds = new List<Bounds>(renderers.Length);
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;

        foreach (Renderer renderer in renderers)
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            Bounds bounds = GetBoundsInLocalSpace(renderer);
            localBounds.Add(bounds);
            minY = Mathf.Min(minY, bounds.min.y);
            maxY = Mathf.Max(maxY, bounds.max.y);
        }

        if (localBounds.Count == 0)
            return;

        float height = Mathf.Max(maxY - minY, 0.0001f);
        float cutoff = minY + height * Mathf.Clamp01(footHeightFraction);
        float mergeRadius = height * Mathf.Max(footMergeFraction, 0.0001f);
        float mergeRadiusSqr = mergeRadius * mergeRadius;

        foreach (Bounds bounds in localBounds)
        {
            if (bounds.min.y > cutoff)
                continue;

            Vector3 point = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            int existing = -1;

            for (int i = 0; i < _footPoints.Count; i++)
            {
                Vector3 delta = _footPoints[i] - point;
                delta.y = 0f;

                if (delta.sqrMagnitude <= mergeRadiusSqr)
                {
                    existing = i;
                    break;
                }
            }

            if (existing < 0)
            {
                _footPoints.Add(point);
                continue;
            }

            Vector3 merged = _footPoints[existing];
            merged.x = (merged.x + point.x) * 0.5f;
            merged.z = (merged.z + point.z) * 0.5f;
            merged.y = Mathf.Min(merged.y, point.y);
            _footPoints[existing] = merged;
        }
    }

    private Bounds GetBoundsInLocalSpace(Renderer renderer)
    {
        Mesh mesh = null;

        if (renderer.TryGetComponent(out MeshFilter meshFilter))
            mesh = meshFilter.sharedMesh;
        else if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
            mesh = skinnedMeshRenderer.sharedMesh;

        if (mesh == null)
        {
            Bounds worldBounds = renderer.bounds;
            Vector3 center = transform.InverseTransformPoint(worldBounds.center);
            Vector3 extents = transform.InverseTransformVector(worldBounds.extents);
            extents = new Vector3(Mathf.Abs(extents.x), Mathf.Abs(extents.y), Mathf.Abs(extents.z));
            return new Bounds(center, extents * 2f);
        }

        Bounds meshBounds = mesh.bounds;
        Matrix4x4 toLocal = transform.worldToLocalMatrix * renderer.localToWorldMatrix;
        Vector3 min = meshBounds.min;
        Vector3 max = meshBounds.max;

        Bounds localBounds = new Bounds(toLocal.MultiplyPoint3x4(meshBounds.center), Vector3.zero);

        for (int x = 0; x < 2; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                for (int z = 0; z < 2; z++)
                {
                    Vector3 corner = new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y,
                        z == 0 ? min.z : max.z);
                    localBounds.Encapsulate(toLocal.MultiplyPoint3x4(corner));
                }
            }
        }

        return localBounds;
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