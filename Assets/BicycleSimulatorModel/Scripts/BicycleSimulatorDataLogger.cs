using System;
using System.IO;
using System.Text;
using UnityEngine;

public class BikeSimulatorDataLogger : MonoBehaviour
{
    public GameObject BikeConnectorTCP;
    private tcp_client tcp_bike_connection;
    public float globalSpeedGainSimBike = 1.0f;
    public float constantSimVelocity = 5.0f;
    public bool useConstantVelocity = false;
    public float mass_kg = 80.0f; // 你可以根据实际情况修改

    public float TargetVelocityWahooBike { get; private set; } = 0.0f;

    private float actual_velocity_unity_bike = 0.0f;
    private float acceleration = 0.0f;

    private string filePath;
    private StringBuilder csvBuilder;
    private string timestamp;

    void Start()
    {
        tcp_bike_connection = BikeConnectorTCP.GetComponent<tcp_client>();
        string folderPath = @"C:\Users\TUBVVTK-VTSIM14\unity_2025_2\Test_Logging";
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        filePath = Path.Combine(folderPath, $"BikeLog_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        csvBuilder = new StringBuilder();
        csvBuilder.AppendLine("Timestamp,TargetVelocity,TargetPower");

        // 初始写入表头
        File.WriteAllText(filePath, csvBuilder.ToString());
    }

    void Update()
    {
        if (useConstantVelocity)
        {
            TargetVelocityWahooBike = constantSimVelocity;
        }
        else
        {
            TargetVelocityWahooBike = ((float)tcp_bike_connection.targetOutputVelocity / 3.6f) * globalSpeedGainSimBike;
        }

        actual_velocity_unity_bike = GetComponent<Rigidbody>().velocity.magnitude;



        // 获取时间戳（以秒为单位）
        timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
 
        // 记录数据
        string newLine = $"{timestamp};{TargetVelocityWahooBike.ToString("F1")};{(float)tcp_bike_connection.targetOutputPower}";
        File.AppendAllText(filePath, newLine + Environment.NewLine);
    }
}
