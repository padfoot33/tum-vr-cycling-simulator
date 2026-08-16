using System.Collections;
using UnityEngine;

public class ScenarioSpawnManager : MonoBehaviour
{
    [Header("Bike Reference")]
    public GameObject egoBike;

    [Header("Scenario Spawn Points")]
    public Transform scenario1Spawn;
    public Transform scenario2Spawn;
    public Transform scenario3Spawn;

    [Header("Active Scenario")]
    public int activeScenario = 1;

    private IEnumerator Start()
    {
        Debug.Log("ScenarioSpawnManager Start called.");
        yield return new WaitForSeconds(0.2f);
        MoveBikeToActiveScenarioSpawn();
    }

    public void MoveBikeToActiveScenarioSpawn()
    {
        Transform targetSpawn = null;

        if (activeScenario == 1) targetSpawn = scenario1Spawn;
        else if (activeScenario == 2) targetSpawn = scenario2Spawn;
        else if (activeScenario == 3) targetSpawn = scenario3Spawn;

        if (egoBike == null)
        {
            Debug.LogError("ScenarioSpawnManager: egoBike not assigned.");
            return;
        }

        if (targetSpawn == null)
        {
            Debug.LogError("ScenarioSpawnManager: targetSpawn not assigned.");
            return;
        }

        Debug.Log("Before move bike pos = " + egoBike.transform.position);
        Debug.Log("Target spawn = " + targetSpawn.name + " pos = " + targetSpawn.position);

        Rigidbody rb = egoBike.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.position = targetSpawn.position;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        else
        {
            egoBike.transform.position = targetSpawn.position;
        }

        Debug.Log("Bike moved to Scenario " + activeScenario + " spawn position.");
        Debug.Log("After move bike pos = " + egoBike.transform.position);
    }
}