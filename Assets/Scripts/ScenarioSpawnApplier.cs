using UnityEngine;
using System.Collections;
using System.Reflection;
using tumvt.sumounity;

public class ScenarioSpawnApplier : MonoBehaviour
{
    [SerializeField] private ScenarioManager scenarioManager;
    [SerializeField] private Transform r1Start;
    [SerializeField] private Transform r2Start;

    [Header("Ground Snap")]
    [SerializeField] private bool snapToGround = true;
    [SerializeField] private LayerMask groundMask = ~0;      // Everything by default
    [SerializeField] private float rayStartHeight = 50f;     // cast from above
    [SerializeField] private float rayDistance = 200f;       // cast down distance
    [SerializeField] private float groundOffset = 0.05f;     // small lift above ground
    [SerializeField] private float maxSnapUp = 0.5f;         // don't snap upward more than this

    [Header("Stability")]
    [SerializeField] private bool freezeDuringTeleport = true;
    [SerializeField] private float settleSeconds = 0.25f;
    [SerializeField] private int startupDelayFrames = 5; // Wait for all other Start() methods to complete

    [Header("Safe Enable Conditions")]
    [SerializeField] private bool waitForSumoPhysicsArea = true;  // Wait for SumoVehicleDetect to return true
    [SerializeField] private float maxDistanceBeforeEnable = 2.0f; // Max distance between SUMO and Unity position before safe to enable
    [SerializeField] private float enableCheckInterval = 0.05f;    // Check every 50ms if safe to enable
    [SerializeField] private float enableTimeoutSeconds = 10.0f;   // 0 to disable fallback timeout

    [Header("SUMO Sync")]
    [SerializeField] private bool sendTeleportToSumo = true;
    [SerializeField] private int sumoSyncFrames = 8;
    [SerializeField] private float sumoSyncInterval = 0.05f;

    private bool appliedOnce = false;
    private SumoSocketClient sumoSocketClient = null;
    private string bikeId = "";

    private IEnumerator Start()
    {
        Debug.Log($"[ScenarioSpawnApplier] 🚀 START COROUTINE RUNNING - ScenarioSpawnApplier object: '{gameObject.name}' (InstanceID={GetInstanceID()}), path='{GetFullHierarchyPath()}' | Initial bike position: {transform.position}, Y={transform.position.y}");

        // Get bike ID and SUMO socket for later enable-safe checks
        var bikeController = GetComponent<SBPScripts.BicycleController>();
        if (bikeController == null)
        {
            bikeController = GetComponentInParent<SBPScripts.BicycleController>();
        }
        if (bikeController != null)
        {
            bikeId = bikeController.id;
        }

        sumoSocketClient = FindObjectOfType<SumoSocketClient>();
        if (sumoSocketClient != null)
        {
            Debug.Log($"[ScenarioSpawnApplier] ✅ SumoSocketClient found for enable-safe checks");
        }

        // Wait a few frames to ensure all other Start() methods complete (especially BicycleSimulatorController.Start)
        for (int i = 0; i < startupDelayFrames; i++)
        {
            yield return null;
        }
        Debug.Log($"[ScenarioSpawnApplier] ✅ Startup delay complete ({startupDelayFrames} frames)");

        // Auto-find references if not assigned
        if (scenarioManager == null) scenarioManager = FindObjectOfType<ScenarioManager>(true);
        if (r1Start == null) r1Start = GameObject.Find("R1_Start")?.transform;
        if (r2Start == null) r2Start = GameObject.Find("R2_Start")?.transform;

        if (scenarioManager != null)
        {
            Debug.Log($"[ScenarioSpawnApplier] ✅ ScenarioManager found: {scenarioManager.gameObject.name} in scene: {scenarioManager.gameObject.scene.name}");
        }
        else
        {
            Debug.LogError("[ScenarioSpawnApplier] ❌ ScenarioManager not found!");
        }

        Debug.Log($"[ScenarioSpawnApplier] 🎮 Spawn points - R1_Start: {(r1Start != null ? r1Start.position.ToString() : "NOT FOUND")}, R2_Start: {(r2Start != null ? r2Start.position.ToString() : "NOT FOUND")}");

        // Wait until Scenario is loaded
        int waitCounter = 0;
        while (scenarioManager == null || scenarioManager.GetCurrentScenario() == null)
        {
            if (waitCounter % 20 == 0)
            {
                var currentScenario = scenarioManager?.GetCurrentScenario();
                Debug.Log($"[ScenarioSpawnApplier] ⏳ Waiting for scenario (counter={waitCounter}, scenarioManager={scenarioManager != null}, scenario={currentScenario != null})");
            }
            waitCounter++;
            yield return null;
        }

        Debug.Log($"[ScenarioSpawnApplier] ✅✅ Scenario LOADED! RouteOrder='{scenarioManager.GetCurrentScenario().RouteOrder}'. NOW APPLYING SPAWN!");
        ApplyFromScenario();
    }

    public void ApplyFromScenario()
    {
        if (appliedOnce) 
        {
            Debug.LogWarning("[ScenarioSpawnApplier] ApplyFromScenario called but appliedOnce=true, skipping");
            return;
        }

        if (scenarioManager == null)
        {
            Debug.LogError("[ScenarioSpawnApplier] ScenarioManager is null!");
            return;
        }

        var sc = scenarioManager.GetCurrentScenario();
        if (sc == null)
        {
            Debug.LogError("[ScenarioSpawnApplier] GetCurrentScenario() returned NULL!");
            return;
        }

        if (string.IsNullOrEmpty(sc.RouteOrder))
        {
            Debug.LogError($"[ScenarioSpawnApplier] RouteOrder is empty/null! Scenario ID={sc.Id}");
            return;
        }

        Debug.Log($"[ScenarioSpawnApplier] 📊 Route check: RouteOrder='{sc.RouteOrder}' (length={sc.RouteOrder.Length})");
        
        string routeOrderTrimmed = sc.RouteOrder.Trim();
        bool startsWithR2 = routeOrderTrimmed.StartsWith("R2");
        Debug.Log($"[ScenarioSpawnApplier] 🔍 After Trim: '{routeOrderTrimmed}', startsWithR2={startsWithR2}");
        
        Transform target = startsWithR2 ? r2Start : r1Start;
        string targetName = startsWithR2 ? "R2_Start" : "R1_Start";

        Debug.Log($"[ScenarioSpawnApplier] 🎯 Selected target: {targetName}, found={target != null}");

        if (target == null)
        {
            Debug.LogError($"[ScenarioSpawnApplier] ❌ {targetName} spawn point NOT FOUND in scene!");
            Debug.LogError($"[ScenarioSpawnApplier] R1_Start={r1Start}, R2_Start={r2Start}");
            return;
        }

        Debug.Log($"[ScenarioSpawnApplier] ✅ Starting TeleportRoutine to {targetName}");
        StartCoroutine(TeleportRoutine(target, targetName, sc.RouteOrder));
    }

    private IEnumerator TeleportRoutine(Transform target, string targetName, string routeOrder)
    {
        var rb = GetComponent<Rigidbody>();

        Debug.Log($"[ScenarioSpawnApplier] 📋 TeleportRoutine START - This object: '{gameObject.name}' (InstanceID={GetInstanceID()}), path='{GetFullHierarchyPath()}'");

        // Disable SUMO teleport BEFORE applying spawn to prevent overwrite
        DisableSumoTeleportForSpawn("ScenarioSpawnApplier starting spawn");

                // Decide final pose (snap to ground)
        Vector3 finalPos = target.position;
        Quaternion finalRot = target.rotation;

        if (snapToGround)
        {
            Vector3 rayStart = target.position + Vector3.up * rayStartHeight;
            RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, rayDistance, groundMask, QueryTriggerInteraction.Ignore);
            bool found = false;
            RaycastHit bestHit = default;
            float maxAllowedY = target.position.y + maxSnapUp;

            foreach (var hit in hits)
            {
                if (hit.collider != null && hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }
                if (hit.point.y > maxAllowedY)
                {
                    continue;
                }
                if (!found || hit.point.y > bestHit.point.y)
                {
                    bestHit = hit;
                    found = true;
                }
            }

            if (found)
            {
                finalPos = bestHit.point + Vector3.up * groundOffset;
                Debug.Log($"[ScenarioSpawnApplier] Ground raycast HIT: {bestHit.collider.name} at Y={bestHit.point.y} => adjusted finalPos Y={finalPos.y}");
            }
            else
            {
                Debug.LogWarning($"[ScenarioSpawnApplier] Ground raycast MISS or above maxSnapUp. Using target.position Y={target.position.y}");
            }
        }

        Debug.Log($"[ScenarioSpawnApplier] Teleporting to {targetName} (RouteOrder={routeOrder}) => finalPos={finalPos}");

        // Stabilize physics while teleporting
        bool hadRB = rb != null;
        bool oldKinematic = false;
        bool oldGravity = false;

        if (hadRB && freezeDuringTeleport)
        {
            oldKinematic = rb.isKinematic;
            oldGravity = rb.useGravity;

            rb.isKinematic = true;
            rb.useGravity = false;
            Debug.Log($"[ScenarioSpawnApplier] 🔒 Physics frozen: isKinematic=true, useGravity=false");
        }

        // Teleport
        if (hadRB)
        {
            rb.position = finalPos;
            rb.rotation = finalRot;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            Debug.Log($"[ScenarioSpawnApplier] 📌 RB position set to: {rb.position} (Y={rb.position.y})");
        }
        else
        {
            transform.SetPositionAndRotation(finalPos, finalRot);
            Debug.Log($"[ScenarioSpawnApplier] 📌 Transform position set to: {transform.position} (Y={transform.position.y})");
        }

        // Let transforms/physics settle a bit
        yield return null;

        if (hadRB && freezeDuringTeleport)
        {
            yield return new WaitForSeconds(settleSeconds);

            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            rb.isKinematic = oldKinematic;
            rb.useGravity = oldGravity;
            Debug.Log($"[ScenarioSpawnApplier] 🔓 Physics unfrozen: isKinematic={oldKinematic}, useGravity={oldGravity}");
        }

        appliedOnce = true;

        if (sendTeleportToSumo)
        {
            yield return StartCoroutine(SendTeleportToSumo(finalPos, finalRot));
        }
        
        // Now wait until it's safe to enable SUMO teleport
        Debug.Log($"[ScenarioSpawnApplier] ⏳ Checking safety conditions before enabling SUMO teleport...");
        yield return StartCoroutine(WaitUntilSafeToEnableSumoTeleport(rb, finalPos));
        
        Debug.Log($"[ScenarioSpawnApplier] ✅ Safety check passed! Enabling SUMO teleport now. rb.position={rb.position} (Y={rb.position.y})");
        EnableSumoTeleportAfterSpawn("ScenarioSpawnApplier spawn complete and safe");
        
        // Diagnostic: notify and log controller details
        var simulatorController = GetComponent<SBPScripts.Simulator.BicycleSimulatorController>();
        if (simulatorController == null)
            simulatorController = GetComponentInParent<SBPScripts.Simulator.BicycleSimulatorController>();
        if (simulatorController != null)
        {
            Debug.Log($"[ScenarioSpawnApplier] ✅ BicycleSimulatorController found on: '{simulatorController.gameObject.name}' (InstanceID={simulatorController.GetInstanceID()}), path='{GetFullHierarchyPath(simulatorController.gameObject)}'");
        }
        
        var standardController = GetComponent<SBPScripts.BicycleController>();
        if (standardController == null)
            standardController = GetComponentInParent<SBPScripts.BicycleController>();
        if (standardController != null)
        {
            Debug.Log($"[ScenarioSpawnApplier] ✅ BicycleController found on: '{standardController.gameObject.name}' (InstanceID={standardController.GetInstanceID()}), path='{GetFullHierarchyPath(standardController.gameObject)}'");
        }
        
        Debug.Log($"[ScenarioSpawnApplier] ✅ SPAWNED at {targetName} finalPos={finalPos}, Y={finalPos.y}. Bike now at: {transform.position}");
    }

    private IEnumerator SendTeleportToSumo(Vector3 finalPos, Quaternion finalRot)
    {
        if (sumoSocketClient == null)
        {
            Debug.LogWarning("[ScenarioSpawnApplier] SumoSocketClient is null. Skipping SUMO teleport sync.");
            yield break;
        }
        if (string.IsNullOrEmpty(bikeId))
        {
            Debug.LogWarning("[ScenarioSpawnApplier] Bike ID is empty. Skipping SUMO teleport sync.");
            yield break;
        }

        if (!EnsureSumoSendDataEnabled())
        {
            yield break;
        }

        for (int i = 0; i < sumoSyncFrames; i++)
        {
            yield return new WaitForSeconds(sumoSyncInterval);
        }
    }

    private bool EnsureSumoSendDataEnabled()
    {
        FieldInfo field = typeof(SumoSocketClient).GetField("simulatorVehicleInfo", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
        {
            Debug.LogWarning("[ScenarioSpawnApplier] simulatorVehicleInfo field not found on SumoSocketClient.");
            return false;
        }

        SimulatorVehicleInfo simInfo = field.GetValue(sumoSocketClient) as SimulatorVehicleInfo;
        if (simInfo == null)
        {
            Debug.LogWarning("[ScenarioSpawnApplier] simulatorVehicleInfo is null on SumoSocketClient.");
            return false;
        }

        if (simInfo.egoVehicle == null)
        {
            simInfo.egoVehicle = transform;
        }
        if (!string.IsNullOrEmpty(bikeId))
        {
            simInfo.egoVehicleId = bikeId;
        }
        simInfo._sendData = true;
        return true;
    }

    private Vector3 lastLoggedPosition = Vector3.zero;
    private float lastLogTime = 0f;

    /// <summary>
    /// Helper method to build full hierarchy path for diagnostics.
    /// Example: "SimBike(Clone)/Wheels/FrontWheel"
    /// </summary>
    private string GetFullHierarchyPath(GameObject obj = null)
    {
        if (obj == null) obj = gameObject;
        System.Collections.Generic.List<string> path = new System.Collections.Generic.List<string>();
        Transform current = obj.transform;
        while (current != null)
        {
            path.Add(current.name);
            current = current.parent;
        }
        path.Reverse();
        return string.Join("/", path);
    }

    /// <summary>
    /// Wait until it's safe to enable SUMO teleport.
    /// Checks:
    /// - Option A: SumoVehicleDetect returns true (bike is inside physics area)
    /// - Option B: Distance between SUMO ground truth position and Unity rb position is small enough
    /// </summary>
    private IEnumerator WaitUntilSafeToEnableSumoTeleport(Rigidbody rb, Vector3 spawnPos)
    {
        float elapsed = 0f;
        int checkCount = 0;
        
        while (true)
        {
            elapsed += enableCheckInterval;
            
            bool isSafe = false;
            string reason = "";

            // Check condition A: SUMO physics area
            if (waitForSumoPhysicsArea && !sendTeleportToSumo && !string.IsNullOrEmpty(bikeId) && sumoSocketClient != null)
            {
                bool isInPhysicsArea = Vehicle.SumoVehicleDetect(ref sumoSocketClient, bikeId);
                if (isInPhysicsArea)
                {
                    isSafe = true;
                    reason = $"SUMO vehicle detected in physics area";
                }
            }

            // Check condition B: Distance between SUMO and Unity positions
            if (!isSafe && !string.IsNullOrEmpty(bikeId) && sumoSocketClient != null)
            {
                Vector2 sumoPos = Vehicle.SUMO_groundtruth_back(ref sumoSocketClient, bikeId);
                Vector2 unityPos = new Vector2(rb.position.x, rb.position.z);
                float distance = Vector2.Distance(sumoPos, unityPos);

                if (distance < maxDistanceBeforeEnable)
                {
                    isSafe = true;
                    reason = $"SUMO/Unity distance {distance:F2}m < threshold {maxDistanceBeforeEnable}m (SUMO={sumoPos}, Unity={unityPos})";
                }
                else if (checkCount % 20 == 0)  // Log every 20 checks (~1 second at 50ms intervals)
                {
                    Debug.Log($"[ScenarioSpawnApplier] ⏳ Waiting for safe distance... distance={distance:F2}m, SUMO={sumoPos}, Unity={unityPos}, rb.position={rb.position}");
                }
            }

                        // If neither condition applies but enough time has passed, enable anyway (safety fallback)
            if (!isSafe && enableTimeoutSeconds > 0f && elapsed > enableTimeoutSeconds)
            {
                isSafe = true;
                reason = $"SAFETY TIMEOUT: {enableTimeoutSeconds:F1} seconds elapsed, enabling anyway. rb.position=" + rb.position;
                Debug.LogWarning($"[ScenarioSpawnApplier] \u0192s\u00ff\u2039\u00f7? {reason}");
            }
            if (isSafe)
            {
                Debug.Log($"[ScenarioSpawnApplier] ✅ SAFE TO ENABLE: {reason}");
                break;
            }

            checkCount++;
            yield return new WaitForSeconds(enableCheckInterval);
        }
    }

    /// <summary>
    /// Disable SUMO teleport and WayPoint playback for both bike controller types during spawn.
    /// </summary>
    private void DisableSumoTeleportForSpawn(string reason)
    {
        var simulatorController = GetComponent<SBPScripts.Simulator.BicycleSimulatorController>();
        if (simulatorController == null)
            simulatorController = GetComponentInParent<SBPScripts.Simulator.BicycleSimulatorController>();
            
        if (simulatorController != null)
        {
            Debug.Log($"[ScenarioSpawnApplier] 🔐 Disabling BicycleSimulatorController at: '{simulatorController.gameObject.name}' (InstanceID={simulatorController.GetInstanceID()}), path='{GetFullHierarchyPath(simulatorController.gameObject)}'");
            simulatorController.DisableSumoTeleport(reason);
            simulatorController.DisableWayPointPlayback(reason);
        }
        else
        {
            Debug.Log($"[ScenarioSpawnApplier] ℹ️ BicycleSimulatorController not found on this object or parents");
        }
        
        var standardController = GetComponent<SBPScripts.BicycleController>();
        if (standardController == null)
            standardController = GetComponentInParent<SBPScripts.BicycleController>();
            
        if (standardController != null)
        {
            Debug.Log($"[ScenarioSpawnApplier] 🔐 Disabling BicycleController at: '{standardController.gameObject.name}' (InstanceID={standardController.GetInstanceID()}), path='{GetFullHierarchyPath(standardController.gameObject)}'");
            standardController.DisableSumoTeleport(reason);
            standardController.DisableWayPointPlayback(reason);
        }
        else
        {
            Debug.Log($"[ScenarioSpawnApplier] ℹ️ BicycleController not found on this object or parents");
        }
    }
    
    /// <summary>
    /// Re-enable SUMO teleport and WayPoint playback after spawn is complete and safe.
    /// </summary>
    private void EnableSumoTeleportAfterSpawn(string reason)
    {
        var simulatorController = GetComponent<SBPScripts.Simulator.BicycleSimulatorController>();
        if (simulatorController == null)
            simulatorController = GetComponentInParent<SBPScripts.Simulator.BicycleSimulatorController>();
            
        if (simulatorController != null)
        {
            Debug.Log($"[ScenarioSpawnApplier] 🔓 Enabling BicycleSimulatorController at: '{simulatorController.gameObject.name}' (InstanceID={simulatorController.GetInstanceID()}) - Reason: {reason}");
            simulatorController.EnableSumoTeleport(reason);
            simulatorController.EnableWayPointPlayback(reason);
        }
        
        var standardController = GetComponent<SBPScripts.BicycleController>();
        if (standardController == null)
            standardController = GetComponentInParent<SBPScripts.BicycleController>();
            
        if (standardController != null)
        {
            Debug.Log($"[ScenarioSpawnApplier] 🔓 Enabling BicycleController at: '{standardController.gameObject.name}' (InstanceID={standardController.GetInstanceID()}) - Reason: {reason}");
            standardController.EnableSumoTeleport(reason);
            standardController.EnableWayPointPlayback(reason);
        }
    }

    void LateUpdate()
    {
        if (!appliedOnce) return;

        // Check if bike position has changed significantly after spawn
        if (Time.time - lastLogTime > 0.5f) // Log every 0.5 seconds
        {
            if (Vector3.Distance(transform.position, lastLoggedPosition) > 0.1f)
            {
                Debug.LogWarning($"[ScenarioSpawnApplier] ⚠️ Bike moved after spawn! From {lastLoggedPosition} to {transform.position} (Y: {lastLoggedPosition.y} → {transform.position.y})");
            }
            lastLoggedPosition = transform.position;
            lastLogTime = Time.time;
        }
    }
}
