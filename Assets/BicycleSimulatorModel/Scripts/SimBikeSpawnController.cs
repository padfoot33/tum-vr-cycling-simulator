using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[DefaultExecutionOrder(-2000)]
public class SimBikeSpawnController : MonoBehaviour
{
    [Header("Scenario Spawn Points")]
    [SerializeField] private Transform scenario1Spawn;
    [SerializeField] private Transform scenario2Spawn;
    [SerializeField] private Transform scenario3Spawn;

    [Header("Which scenario to spawn on")]
    [SerializeField] private int activeScenario = 1;

    [Header("Spawn Configuration")]
    [SerializeField] private LayerMask groundMask = -1;
    [SerializeField] private float groundYOffset = 0.05f;
    [SerializeField] private float yawOffsetScenario1Degrees = 0f;
    [SerializeField] private float yawOffsetScenario2Degrees = 0f;
    [SerializeField] private float yawOffsetScenario3Degrees = 0f;
    [SerializeField] private bool lockXZRotationAfterSpawn = true;
    [SerializeField] private float rotationLockDuration = 1.0f;
    [SerializeField] private float postSpawnPositionTolerance = 0.5f;

    private Transform simBikeRoot;

    // Scripts to temporarily disable during spawn
    private static readonly string[] ScriptsToDisable = new[]
    {
        "BicycleController",
        "BicycleSimulatorController",
        "SteeringInput",
        "SimulatorSteeringInput",
        "FFB_Bike",
        "SaveBicycleReplay",
        "CyclistSetup",
        "BicycleStatus"
    };

    private Rigidbody rootRigidbody;
    private Rigidbody[] allRigidbodies;
    private ConfigurableJoint[] allJoints;
    private Rigidbody[] jointConnectedBodies;
    private MonoBehaviour[] disabledScripts;
    private Vector3 initialSpawnPos;
    private Quaternion initialSpawnRot;
    private Vector3 postLockPos;
    private RigidbodyConstraints originalRootConstraints;
    private float rotationLockTimer = 0f;
    private bool isRotationLocked = false;

    private void Awake()
    {
        FindReferences();

        if (simBikeRoot == null || rootRigidbody == null)
        {
            Debug.LogError("[SimBikeSpawnController] Failed to find SimBike root or rigidbody. Aborting spawn.");
            return;
        }

        StartCoroutine(PerformSpawn());
    }

    private void FindReferences()
    {
        if (simBikeRoot == null)
            simBikeRoot = transform;

        rootRigidbody = simBikeRoot.GetComponent<Rigidbody>();
        if (rootRigidbody == null)
        {
            Debug.LogError("[SimBikeSpawnController] Root Rigidbody not found on SimBike");
            return;
        }

        allRigidbodies = simBikeRoot.GetComponentsInChildren<Rigidbody>(true);

        allJoints = simBikeRoot.GetComponentsInChildren<ConfigurableJoint>(true);
        if (allJoints.Length > 0)
        {
            jointConnectedBodies = new Rigidbody[allJoints.Length];
            for (int i = 0; i < allJoints.Length; i++)
            {
                jointConnectedBodies[i] = allJoints[i].connectedBody;
            }
        }

        originalRootConstraints = rootRigidbody.constraints;
    }

    private IEnumerator PerformSpawn()
    {
        if (rootRigidbody == null || simBikeRoot == null)
            yield break;

        Transform targetTransform = GetTargetSpawn();
        if (targetTransform == null)
        {
            Debug.LogError($"[SimBikeSpawnController] No SpawnPoint assigned for activeScenario={activeScenario}");
            yield break;
        }

        float yawOffset = GetYawOffsetForScenario();
        float targetYaw = targetTransform.eulerAngles.y + yawOffset;

        Vector3 targetPos = targetTransform.position;

        float groundY = targetPos.y;
        Vector3 rayOrigin = targetPos + Vector3.up * 10f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 50f, groundMask))
        {
            groundY = hit.point.y + groundYOffset;
            Debug.Log($"[SimBikeSpawnController] Raycast hit '{hit.collider.gameObject.name}' at Y={hit.point.y}");
        }

        Vector3 finalSpawnPos = new Vector3(targetPos.x, groundY, targetPos.z);
        Quaternion yawOnly = Quaternion.Euler(0f, targetYaw, 0f);

        Debug.Log($"[SimBikeSpawnController] SPAWN START: Scenario={activeScenario}");
        Debug.Log($"[SimBikeSpawnController] BEFORE: SimBike pos={simBikeRoot.position}, rot={simBikeRoot.eulerAngles}");
        Debug.Log($"[SimBikeSpawnController] TARGET: pos={finalSpawnPos}, yaw={targetYaw}");

        initialSpawnPos = simBikeRoot.position;
        initialSpawnRot = simBikeRoot.rotation;

        disabledScripts = DisableMovementScripts();
        Debug.Log($"[SimBikeSpawnController] Disabled {disabledScripts.Length} movement scripts");

        foreach (var joint in allJoints)
        {
            joint.connectedBody = null;
        }
        if (allJoints.Length > 0)
            Debug.Log($"[SimBikeSpawnController] Disconnected {allJoints.Length} ConfigurableJoint(s)");

        foreach (var rb in allRigidbodies)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        simBikeRoot.SetPositionAndRotation(finalSpawnPos, yawOnly);
        Physics.SyncTransforms();

        Debug.Log($"[SimBikeSpawnController] AFTER APPLY: SimBike pos={simBikeRoot.position}, rot={simBikeRoot.eulerAngles}");

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        postLockPos = simBikeRoot.position;

        float posDelta = Vector3.Distance(postLockPos, finalSpawnPos);
        if (posDelta > postSpawnPositionTolerance)
        {
            Debug.LogWarning($"[SimBikeSpawnController] POSITION DRIFT: {posDelta}m after spawn lock! Expected {finalSpawnPos}, got {postLockPos}");
            ListEnabledScripts();
        }

        for (int i = 0; i < allJoints.Length; i++)
        {
            allJoints[i].connectedBody = jointConnectedBodies[i];
        }
        if (allJoints.Length > 0)
            Debug.Log($"[SimBikeSpawnController] Reconnected {allJoints.Length} ConfigurableJoint(s)");

        foreach (var rb in allRigidbodies)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (lockXZRotationAfterSpawn)
        {
            rootRigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rotationLockTimer = rotationLockDuration;
            isRotationLocked = true;
            Debug.Log($"[SimBikeSpawnController] Root RB rotation X/Z locked for {rotationLockDuration}s");
        }

        ReenableScripts();
        Debug.Log($"[SimBikeSpawnController] Re-enabled movement scripts");

        Physics.SyncTransforms();

        Debug.Log($"[SimBikeSpawnController] SPAWN COMPLETE: SimBike pos={simBikeRoot.position}, rot={simBikeRoot.eulerAngles}");
    }

    private Transform GetTargetSpawn()
    {
        switch (activeScenario)
        {
            case 1: return scenario1Spawn;
            case 2: return scenario2Spawn;
            case 3: return scenario3Spawn;
            default: return scenario1Spawn;
        }
    }

    private float GetYawOffsetForScenario()
    {
        switch (activeScenario)
        {
            case 1: return yawOffsetScenario1Degrees;
            case 2: return yawOffsetScenario2Degrees;
            case 3: return yawOffsetScenario3Degrees;
            default: return 0f;
        }
    }

    private MonoBehaviour[] DisableMovementScripts()
    {
        var disabled = new List<MonoBehaviour>();
        var scriptNameSet = new HashSet<string>(ScriptsToDisable);

        var allComponents = simBikeRoot.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (var mb in allComponents)
        {
            if (mb != null && mb.enabled && scriptNameSet.Contains(mb.GetType().Name))
            {
                mb.enabled = false;
                disabled.Add(mb);
            }
        }

        if (disabled.Count > 0)
        {
            var disabledNames = new List<string>();
            foreach (var mb in disabled)
                disabledNames.Add(mb.GetType().Name);
            Debug.Log($"[SimBikeSpawnController] Disabled scripts: {string.Join(", ", disabledNames)}");
        }

        return disabled.ToArray();
    }

    private void ReenableScripts()
    {
        if (disabledScripts == null)
            return;

        foreach (var script in disabledScripts)
        {
            if (script != null)
                script.enabled = true;
        }
    }

    private void ListEnabledScripts()
    {
        var enabled = simBikeRoot.GetComponentsInChildren<MonoBehaviour>();
        var enabledNames = new List<string>();

        foreach (var mb in enabled)
        {
            if (mb != null && mb.enabled)
                enabledNames.Add(mb.GetType().Name);
        }

        Debug.LogWarning($"[SimBikeSpawnController] Enabled scripts on SimBike: {string.Join(", ", enabledNames)}");
    }

    private void Update()
    {
        if (isRotationLocked)
        {
            rotationLockTimer -= Time.deltaTime;
            if (rotationLockTimer <= 0f)
            {
                rootRigidbody.constraints = originalRootConstraints;
                isRotationLocked = false;
                Debug.Log("[SimBikeSpawnController] Root RB rotation freeze released. Constraints restored.");
            }
        }
    }
}