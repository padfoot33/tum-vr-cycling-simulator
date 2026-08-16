using UnityEngine;

public class HookCarController : MonoBehaviour
{
    [Header("Assigned by spawner")]
    public Transform[] pathPoints;

    [Header("Tuning")]
    public float speedMps = 12f;     // slow at intersection
    public float reachDist = 1.2f;  // how close to a point before switching

    private int _idx;
    private bool _active;

    public void Begin()
    {
        if (pathPoints == null || pathPoints.Length < 2)
        {
            Debug.LogError("[HookCarController] Need at least 2 path points.");
            return;
        }

        transform.position = pathPoints[0].position;
        _idx = 1;
        _active = true;
        FaceNext();
    }

    private void Update()
    {
        if (!_active) return;

        Transform target = pathPoints[_idx];
        Vector3 to = target.position - transform.position;
        to.y = 0f;

        float dist = to.magnitude;
        if (dist <= reachDist)
        {
            _idx++;
            if (_idx >= pathPoints.Length)
            {
                Destroy(gameObject);
                _active = false;
                return;
            }
            FaceNext();
            return;
        }

        Vector3 moveDir = to.normalized;
        transform.position += moveDir * speedMps * Time.deltaTime;

        // Smooth rotate toward movement
        if (moveDir.sqrMagnitude > 0.001f)
        {
            Quaternion rot = Quaternion.LookRotation(moveDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, 10f * Time.deltaTime);
        }
    }

    private void FaceNext()
    {
        if (_idx >= pathPoints.Length) return;

        Vector3 dir = (pathPoints[_idx].position - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }
}