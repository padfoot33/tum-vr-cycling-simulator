using UnityEngine;

public class ClosePassSpawner : MonoBehaviour
{
    [Header("Assign in Scene (CP_Spawner object)")]
    public GameObject closePassCarPrefab;
    public Transform spawnPoint;
    public Transform despawnPoint;

    [Header("Logger")]
    public RunLogger runLogger;

    [Header("Run control")]
    public bool allowOnlyOncePerRun = true;

    private bool _spawnedOnce = false;
    private GameObject _activeCar;

    public void TriggerClosePass()
    {
        if (allowOnlyOncePerRun && _spawnedOnce)
        {
            Debug.Log("[ClosePassSpawner] Ignored (already spawned once this run).");
            return;
        }

        if (closePassCarPrefab == null || spawnPoint == null || despawnPoint == null)
        {
            Debug.LogError("[ClosePassSpawner] Assign prefab + spawnPoint + despawnPoint in Inspector!");
            return;
        }

        if (_activeCar != null)
        {
            Destroy(_activeCar);
            _activeCar = null;
        }

        _activeCar = Instantiate(closePassCarPrefab);

        var ctrl = _activeCar.GetComponent<ClosePassCarController>();
        if (ctrl == null)
        {
            Debug.LogError("[ClosePassSpawner] Prefab missing ClosePassCarController component!");
            Destroy(_activeCar);
            _activeCar = null;
            return;
        }

        ctrl.spawnPoint = spawnPoint;
        ctrl.despawnPoint = despawnPoint;
        ctrl.runLogger = runLogger;
        ctrl.eventName = "EVENT01_CLOSEPASS";
        ctrl.Begin();

        _spawnedOnce = true;
        Debug.Log("[ClosePassSpawner] Close pass spawned.");
    }
}