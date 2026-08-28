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
    [Tooltip("When off, ScenarioSelectionUI owns spawn via TeleportTo. Leave off in MainScene.")]
    [SerializeField] private bool spawnOnAwake = false;

    [SerializeField] private LayerMask groundMask = -1;
    [SerializeField] private float groundYOffset = 0.05f;

    [SerializeField] private float yawOffsetScenario1Degrees = 0f;
    [SerializeField] private float yawOffsetScenario2Degrees = 0f;
    [SerializeField] private float yawOffsetScenario3Degrees = 0f;

    [Header("Spawn Stabilization")]
    [SerializeField] private bool lockXZRotationAfterSpawn = true;
    [SerializeField] private float rotationLockDuration = 1.0f;

    [SerializeField] private float postSpawnPositionTolerance = 0.5f;

    private Transform simBikeRoot;

    private static readonly string[] ScriptsToDisable = new[]
    {
        "BicycleController",
        "BicycleSimulatorController",
        "SteeringInput",
        "SimulatorSteeringInput",
        "FFB_Bike",
        "FFBInspectorBike",
        "SaveBicycleReplay",
        "CyclistSetup",
        "BicycleStatus"
    };

    private Rigidbody rootRigidbody;
    private Rigidbody[] allRigidbodies;
    private ConfigurableJoint[] allJoints;
    private Rigidbody[] jointConnectedBodies;
    private MonoBehaviour[] disabledScripts;

    private Vector3 postLockPos;

    private RigidbodyConstraints originalRootConstraints;
    private float rotationLockTimer = 0f;
    private bool isRotationLocked = false;

    private void Awake()
    {
        FindReferences();

        if (simBikeRoot == null || rootRigidbody == null)
        {
            Debug.LogError(
                "[SimBikeSpawnController] Failed to find SimBike root or rigidbody. Aborting spawn."
            );
            return;
        }

        // Keep normal bicycle behaviour outside the short spawn stabilization period.
        ClearRotationFreeze();

        if (spawnOnAwake)
            StartCoroutine(PerformScenarioSpawn());
    }

    private void FindReferences()
    {
        if (simBikeRoot == null)
            simBikeRoot = transform;

        rootRigidbody = simBikeRoot.GetComponent<Rigidbody>();

        if (rootRigidbody == null)
        {
            Debug.LogError(
                "[SimBikeSpawnController] Root Rigidbody not found on SimBike"
            );
            return;
        }

        allRigidbodies =
            simBikeRoot.GetComponentsInChildren<Rigidbody>(true);

        allJoints =
            simBikeRoot.GetComponentsInChildren<ConfigurableJoint>(true);

        if (allJoints.Length > 0)
        {
            jointConnectedBodies = new Rigidbody[allJoints.Length];

            for (int i = 0; i < allJoints.Length; i++)
            {
                jointConnectedBodies[i] =
                    allJoints[i].connectedBody;
            }
        }
    }

    private void ClearRotationFreeze()
    {
        if (rootRigidbody == null)
            return;

        rootRigidbody.constraints &=
            ~(RigidbodyConstraints.FreezeRotationX |
              RigidbodyConstraints.FreezeRotationZ);

        originalRootConstraints = rootRigidbody.constraints;
    }

    /// <summary>
    /// Joint-safe teleport used by scenario selection and play-area clamp.
    /// </summary>
    public void TeleportTo(
        Vector3 worldPosition,
        float yawDegrees)
    {
        FindReferences();

        if (simBikeRoot == null ||
            rootRigidbody == null)
            return;

        StopAllCoroutines();

        StartCoroutine(
            PerformTeleport(
                worldPosition,
                yawDegrees,
                true
            )
        );
    }

    public void SetSpawnPoints(
        Transform route1,
        Transform route2,
        Transform route3 = null)
    {
        scenario1Spawn = route1;
        scenario2Spawn = route2;
        scenario3Spawn = route3;
    }

    private IEnumerator PerformScenarioSpawn()
    {
        Transform targetTransform = GetTargetSpawn();

        if (targetTransform == null)
        {
            Debug.LogError(
                $"[SimBikeSpawnController] No SpawnPoint assigned for activeScenario={activeScenario}"
            );

            yield break;
        }

        float yawOffset =
            GetYawOffsetForScenario();

        float targetYaw =
            targetTransform.eulerAngles.y +
            yawOffset;

        yield return PerformTeleport(
            targetTransform.position,
            targetYaw,
            true
        );
    }

    private IEnumerator PerformTeleport(
        Vector3 targetPos,
        float targetYaw,
        bool snapToGround)
    {
        if (rootRigidbody == null ||
            simBikeRoot == null)
            yield break;

        float groundY = targetPos.y;

        if (snapToGround)
        {
            Vector3 rayOrigin =
                targetPos + Vector3.up * 10f;

            if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                50f,
                groundMask))
            {
                groundY =
                    hit.point.y +
                    groundYOffset;
            }
        }

        Vector3 finalSpawnPos =
            new Vector3(
                targetPos.x,
                groundY,
                targetPos.z
            );

        Quaternion yawOnly =
            Quaternion.Euler(
                0f,
                targetYaw,
                0f
            );

        disabledScripts =
            DisableMovementScripts();

        // Temporarily disconnect joints.
        if (allJoints != null)
        {
            foreach (var joint in allJoints)
            {
                if (joint != null)
                    joint.connectedBody = null;
            }
        }

        // Freeze complete bicycle assembly.
        foreach (var rb in allRigidbodies)
        {
            if (rb == null)
                continue;

            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            rb.useGravity = false;
            rb.isKinematic = true;
        }

        // Move whole bicycle.
        simBikeRoot.SetPositionAndRotation(
            finalSpawnPos,
            yawOnly
        );

        Physics.SyncTransforms();

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        postLockPos =
            simBikeRoot.position;

        float posDelta =
            Vector3.Distance(
                postLockPos,
                finalSpawnPos
            );

        if (posDelta >
            postSpawnPositionTolerance)
        {
            Debug.LogWarning(
                $"[SimBikeSpawnController] Position drift after spawn: {posDelta:F3} m"
            );

            ListEnabledScripts();
        }

        // Reconnect joints.
        if (allJoints != null &&
            jointConnectedBodies != null)
        {
            for (int i = 0;
                 i < allJoints.Length;
                 i++)
            {
                if (allJoints[i] != null)
                {
                    allJoints[i].connectedBody =
                        jointConnectedBodies[i];
                }
            }
        }

        // Restore physics.
        foreach (var rb in allRigidbodies)
        {
            if (rb == null)
                continue;

            rb.isKinematic = false;
            rb.useGravity = true;

            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // ---- Dilawar-style startup stabilization ----
        if (lockXZRotationAfterSpawn)
        {
            rootRigidbody.constraints =
                originalRootConstraints |
                RigidbodyConstraints.FreezeRotationX |
                RigidbodyConstraints.FreezeRotationZ;

            rootRigidbody.angularVelocity =
                Vector3.zero;

            rotationLockTimer =
                rotationLockDuration;

            isRotationLocked = true;

            Debug.Log(
                $"[SimBikeSpawnController] X/Z rotation locked for {rotationLockDuration:F1}s after spawn."
            );
        }

        ReenableScripts();

        Physics.SyncTransforms();
    }

    private void Update()
    {
        if (!isRotationLocked ||
            rootRigidbody == null)
            return;

        rotationLockTimer -=
            Time.deltaTime;

        if (rotationLockTimer <= 0f)
        {
            rootRigidbody.angularVelocity =
                Vector3.zero;

            rootRigidbody.constraints =
                originalRootConstraints;

            isRotationLocked = false;

            Debug.Log(
                "[SimBikeSpawnController] X/Z startup rotation lock released."
            );
        }
    }

    public void ZeroAllVelocities()
    {
        if (allRigidbodies == null)
            FindReferences();

        if (allRigidbodies == null)
            return;

        foreach (var body in allRigidbodies)
        {
            if (body == null)
                continue;

            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }

    private Transform GetTargetSpawn()
    {
        switch (activeScenario)
        {
            case 1:
                return scenario1Spawn;

            case 2:
                return scenario2Spawn;

            case 3:
                return scenario3Spawn;

            default:
                return scenario1Spawn;
        }
    }

    private float GetYawOffsetForScenario()
    {
        switch (activeScenario)
        {
            case 1:
                return yawOffsetScenario1Degrees;

            case 2:
                return yawOffsetScenario2Degrees;

            case 3:
                return yawOffsetScenario3Degrees;

            default:
                return 0f;
        }
    }

    private MonoBehaviour[] DisableMovementScripts()
    {
        var disabled =
            new List<MonoBehaviour>();

        var scriptNameSet =
            new HashSet<string>(
                ScriptsToDisable
            );

        var allComponents =
            simBikeRoot
            .GetComponentsInChildren<MonoBehaviour>(
                true
            );

        foreach (var mb in allComponents)
        {
            if (mb != null &&
                mb.enabled &&
                scriptNameSet.Contains(
                    mb.GetType().Name))
            {
                mb.enabled = false;
                disabled.Add(mb);
            }
        }

        return disabled.ToArray();
    }

    private void ReenableScripts()
    {
        if (disabledScripts == null)
            return;

        foreach (var script in
                 disabledScripts)
        {
            if (script != null)
                script.enabled = true;
        }
    }

    private void ListEnabledScripts()
    {
        var enabled =
            simBikeRoot
            .GetComponentsInChildren<MonoBehaviour>(
                true
            );

        var enabledNames =
            new List<string>();

        foreach (var mb in enabled)
        {
            if (mb != null &&
                mb.enabled)
            {
                enabledNames.Add(
                    mb.GetType().Name
                );
            }
        }

        Debug.LogWarning(
            $"[SimBikeSpawnController] Enabled scripts on SimBike: {string.Join(", ", enabledNames)}"
        );
    }
}