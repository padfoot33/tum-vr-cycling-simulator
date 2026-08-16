using System.Collections;
using UnityEngine;

public class HookCarSpawner : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject carPrefab;

    [Header("Path Points (Scene)")]
    public Transform carSpawn;
    public Transform carApproach;
    public Transform carTurnRight;
    public Transform carDespawn;

    [Header("Logger")]
    public RunLogger runLogger;
    public string activeEventName = "EVENT02_INTERSECTION";
    public string endMarkerName = "EVENT02_INTERSECTION_END";

    [Header("Safety")]
    public bool preventDoubleSpawn = true;

    [Header("Speed Settings (m/s)")]
    public float approachSpeed = 6.0f;   // ~21.6 km/h
    public float turnSpeed = 3.5f;       // ~12.6 km/h
    public float exitSpeed = 5.0f;       // ~18 km/h

    [Header("Rotation Smoothing")]
    public float rotationSmooth = 8f;

    [Header("Curve Settings")]
    [Range(0.05f, 0.5f)]
    public float waypointReachDistance = 0.6f;

    private GameObject activeCar;
    private bool isSpawningOrRunning = false;

    public void SpawnHookCar()
    {
        if (carPrefab == null || carSpawn == null || carApproach == null || carTurnRight == null || carDespawn == null)
        {
            Debug.LogWarning("[HookCarSpawner] Missing prefab or one/more path points.");
            return;
        }

        if (preventDoubleSpawn && (isSpawningOrRunning || activeCar != null))
        {
            Debug.Log("[HookCarSpawner] Prevented double spawn.");
            return;
        }

        activeCar = Instantiate(carPrefab, carSpawn.position, carSpawn.rotation);
        isSpawningOrRunning = true;

        StartCoroutine(MoveHookCarRoutine(activeCar));
    }

    private IEnumerator MoveHookCarRoutine(GameObject car)
    {
        if (car == null)
        {
            isSpawningOrRunning = false;
            yield break;
        }

        // Phase 1: straight approach
        yield return MoveStraight(car.transform, carApproach.position, approachSpeed, activeEventName);

        // Phase 2: smooth turning curve
        yield return MoveCurvedTurn(car.transform, carApproach.position, carTurnRight.position, carDespawn.position, activeEventName);

        if (runLogger != null)
        {
            runLogger.MarkEvent(endMarkerName);
            runLogger.SetEvent("");
            runLogger.ClearEventVehicleData();
        }

        if (car != null)
        {
            Destroy(car);
        }

        activeCar = null;
        isSpawningOrRunning = false;
    }

    private IEnumerator MoveStraight(Transform obj, Vector3 target, float speed, string eventName)
    {
        while (obj != null && Vector3.Distance(obj.position, target) > waypointReachDistance)
        {
            Vector3 dir = (target - obj.position).normalized;
            obj.position += dir * speed * Time.deltaTime;

            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
                obj.rotation = Quaternion.Slerp(obj.rotation, targetRot, rotationSmooth * Time.deltaTime);
            }

            if (runLogger != null && runLogger.IsLogging)
            {
                runLogger.SetEvent(eventName);
                runLogger.UpdateEventVehicleData(obj.position.x, obj.position.z, speed * 3.6f);
            }

            yield return null;
        }

        if (obj != null)
            obj.position = target;
    }

    private IEnumerator MoveCurvedTurn(Transform obj, Vector3 p0, Vector3 p1, Vector3 p2, string eventName)
    {
        float approxLength = Vector3.Distance(p0, p1) + Vector3.Distance(p1, p2);
        float t = 0f;

        while (obj != null && t < 1f)
        {
            float currentSpeed = Mathf.Lerp(turnSpeed, exitSpeed, t * 0.8f);
            float tStep = (currentSpeed / Mathf.Max(approxLength, 0.01f)) * Time.deltaTime;

            t += tStep;
            t = Mathf.Clamp01(t);

            Vector3 pos = EvaluateQuadraticBezier(p0, p1, p2, t);
            Vector3 nextPos = EvaluateQuadraticBezier(p0, p1, p2, Mathf.Clamp01(t + 0.02f));
            Vector3 dir = (nextPos - pos).normalized;

            obj.position = pos;

            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
                obj.rotation = Quaternion.Slerp(obj.rotation, targetRot, rotationSmooth * Time.deltaTime);
            }

            if (runLogger != null && runLogger.IsLogging)
            {
                runLogger.SetEvent(eventName);
                runLogger.UpdateEventVehicleData(obj.position.x, obj.position.z, currentSpeed * 3.6f);
            }

            yield return null;
        }

        if (obj != null)
        {
            obj.position = p2;
        }
    }

    private Vector3 EvaluateQuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        return (u * u * p0) + (2f * u * t * p1) + (t * t * p2);
    }
}