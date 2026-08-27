using System.IO;
using System.Text;
using UnityEngine;

public class BikeLogger : MonoBehaviour
{
    public Transform bikeRoot;
    public Rigidbody bikeRigidbody;
    public Transform cameraTransform;

    [Header("Logging Settings")]
    public float logHz = 50f;
    public string conditionName = "LOD1";

    private float nextLogTime;
    private float trialStartTime;
    private bool isLogging;

    private StringBuilder sb;
    private string filePath;

    void Start()
    {
        sb = new StringBuilder(2048);

        // Improved header
        sb.AppendLine("timeGlobal,trialTime,condition,posX,posY,posZ,speed_mps,speed_kmh,yaw,cameraYaw,cameraPitch,isMoving");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
            StartLogging();

        if (Input.GetKeyDown(KeyCode.K))
            StopLogging();
    }

    void FixedUpdate()
    {
        if (!isLogging) return;

        float t = Time.time;
        if (t < nextLogTime) return;

        nextLogTime = t + (1f / Mathf.Max(1f, logHz));

        Vector3 p = bikeRoot.position;
        float speed = bikeRigidbody.velocity.magnitude;
        float speedKmh = speed * 3.6f;

        float yaw = bikeRoot.eulerAngles.y;
        float camYaw = cameraTransform.eulerAngles.y;
        float camPitch = cameraTransform.eulerAngles.x;

        float trialTime = t - trialStartTime;

        int isMoving = speed > 0.05f ? 1 : 0; // threshold avoids tiny physics noise

        sb.Append(t.ToString("F4")).Append(',')
          .Append(trialTime.ToString("F4")).Append(',')
          .Append(conditionName).Append(',')
          .Append(p.x.ToString("F3")).Append(',')
          .Append(p.y.ToString("F3")).Append(',')
          .Append(p.z.ToString("F3")).Append(',')
          .Append(speed.ToString("F4")).Append(',')
          .Append(speedKmh.ToString("F2")).Append(',')
          .Append(yaw.ToString("F2")).Append(',')
          .Append(camYaw.ToString("F2")).Append(',')
          .Append(camPitch.ToString("F2")).Append(',')
          .Append(isMoving)
          .AppendLine();
    }

    public void StartLogging()
    {
        if (isLogging) return;

        string folder = Path.Combine(Application.dataPath, "Logs");
        Directory.CreateDirectory(folder);

        string fname = $"bike_log_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv";
        filePath = Path.Combine(folder, fname);

        trialStartTime = Time.time;
        nextLogTime = 0f;
        isLogging = true;

        Debug.Log("[BikeLogger] Logging STARTED");
    }

    public void StopLogging()
    {
        if (!isLogging) return;

        isLogging = false;
        File.WriteAllText(filePath, sb.ToString());

        Debug.Log("[BikeLogger] Logging SAVED: " + filePath);
    }
}
