using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BikeSimulatorDataLogger))]
public class BikeSimulatorRealtimePlotter : MonoBehaviour
{
    public Camera targetCamera;
    public LineRenderer velocityLine;
    public LineRenderer powerLine;
    public float timeWindow = 30f; // 总宽度
    public float scrollSpeed = 2f;  // 向左滚动的速度

    public float distanceInFront = 2f;
    public float heightOffset = -0.5f;
    public float sideOffset = 0f;

    private BikeSimulatorDataLogger bikeLogger;
    private List<Vector3> velocityPoints = new List<Vector3>();
    private List<Vector3> powerPoints = new List<Vector3>();

    void Start()
    {
        bikeLogger = GetComponent<BikeSimulatorDataLogger>();

        velocityLine.positionCount = 0;
        powerLine.positionCount = 0;

        velocityLine.material = new Material(Shader.Find("Sprites/Default"));
        velocityLine.startColor = velocityLine.endColor = Color.red;

        powerLine.material = new Material(Shader.Find("Sprites/Default"));
        powerLine.startColor = powerLine.endColor = Color.blue;
    }

    void Update()
    {
        float vel = bikeLogger.TargetVelocityWahooBike;
        float power = (float)bikeLogger.BikeConnectorTCP.GetComponent<tcp_client>().targetOutputPower;

        // 新值总是插入在最右边
        velocityPoints.Add(new Vector3(timeWindow * 0.5f, vel * 1.0f, 0));
        powerPoints.Add(new Vector3(timeWindow * 0.5f, power * 0.1f, 0));

        // 所有点向左平移
        for (int i = 0; i < velocityPoints.Count; i++)
        {
            velocityPoints[i] = new Vector3(velocityPoints[i].x - Time.deltaTime * scrollSpeed, velocityPoints[i].y, 0);
        }
        for (int i = 0; i < powerPoints.Count; i++)
        {
            powerPoints[i] = new Vector3(powerPoints[i].x - Time.deltaTime * scrollSpeed, powerPoints[i].y, 0);
        }

        // 移除超出左边界的点
        velocityPoints.RemoveAll(p => p.x < -timeWindow * 0.5f);
        powerPoints.RemoveAll(p => p.x < -timeWindow * 0.5f);

        // 更新 LineRenderer
        velocityLine.positionCount = velocityPoints.Count;
        velocityLine.SetPositions(velocityPoints.ToArray());

        powerLine.positionCount = powerPoints.Count;
        powerLine.SetPositions(powerPoints.ToArray());

        // 永远挂在摄像机前面
        Transform cam = targetCamera.transform;
        transform.position = cam.position 
                   + cam.forward * distanceInFront 
                   + cam.up * heightOffset 
                   + cam.right * sideOffset;
        transform.rotation = cam.rotation;
    }
}
