using UnityEngine;

public class ClosePassCarController : MonoBehaviour
{
    [Header("Assigned by Spawner (Scene)")]
    public Transform spawnPoint;       // DO NOT assign in prefab
    public Transform despawnPoint;     // DO NOT assign in prefab
    public RunLogger runLogger;        // assigned by spawner
    public string eventName = "EVENT01_CLOSEPASS";

    [Header("Tuning")]
    public float speedMps = 10f;         // 10 m/s = 36 km/h
    public float lateralOffsetM = 1.2f;  // how close the pass feels
    public float despawnRadiusM = 2f;    // how close to despawn point before destroying

    private bool _active = false;

    public void Begin()
    {
        if (spawnPoint == null || despawnPoint == null)
        {
            Debug.LogError("[ClosePassCarController] spawnPoint/despawnPoint not assigned (Spawner must assign them).");
            return;
        }

        // Spawn at spawn point + lateral offset
        transform.position = spawnPoint.position + spawnPoint.right * lateralOffsetM;

        // Face toward despawn point
        Vector3 dir = (despawnPoint.position - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

        _active = true;

        if (runLogger != null)
        {
            runLogger.SetEvent(eventName);
            runLogger.UpdateEventVehicleData(transform.position.x, transform.position.z, speedMps * 3.6f);
        }
    }

    private void Update()
    {
        if (!_active) return;

        // Move forward
        transform.position += transform.forward * speedMps * Time.deltaTime;

        // Update logger every frame while active
        if (runLogger != null && runLogger.IsLogging)
        {
            runLogger.SetEvent(eventName);
            runLogger.UpdateEventVehicleData(transform.position.x, transform.position.z, speedMps * 3.6f);
        }

        // Despawn
        if (Vector3.Distance(transform.position, despawnPoint.position) <= despawnRadiusM)
        {
            if (runLogger != null)
            {
                runLogger.MarkEvent("EVENT01_CLOSEPASS_END");
                runLogger.SetEvent("NONE");
                runLogger.ClearEventVehicleData();
            }

            _active = false;
            Destroy(gameObject);
        }
    }
}