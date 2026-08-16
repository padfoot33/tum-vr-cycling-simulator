using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class Scenario
{
    //,Participant,Drive,Route_Order,R1_HMI,R2_HMI,H1,H2,H3,H4,H5,H6
    private string id;
    private string participant;
    private string drive;
    private string routeOrder;
    private string r1HMI;
    private string r2HMI;
    private string h1;
    private string h2;
    private string h3;
    private string h4;
    private string h5;
    private string h6;

    public Scenario(string id, string participant, string drive, string routeOrder, string r1HMI, string r2HMI, string h1, string h2, string h3, string h4, string h5, string h6)
    {
        this.id = id;
        this.participant = participant;
        this.drive = drive;
        this.routeOrder = routeOrder;
        this.r1HMI = r1HMI;
        this.r2HMI = r2HMI;
        this.h1 = h1;
        this.h2 = h2;
        this.h3 = h3;
        this.h4 = h4;
        this.h5 = h5;
        this.h6 = h6;
    }

    // getters setters
    public string Id { get => id; set => id = value; }
    public string Participant { get => participant; set => participant = value; }
    public string Drive { get => drive; set => drive = value; }
    public string RouteOrder { get => routeOrder; set => routeOrder = value; }
    public string R1HMI { get => r1HMI; set => r1HMI = value; }
    public string R2HMI { get => r2HMI; set => r2HMI = value; }
    public string H1 { get => h1; set => h1 = value; }
    public string H2 { get => h2; set => h2 = value; }
    public string H3 { get => h3; set => h3 = value; }
    public string H4 { get => h4; set => h4 = value; }
    public string H5 { get => h5; set => h5 = value; }
    public string H6 { get => h6; set => h6 = value; }
}

public class ScenarioManager : MonoBehaviour
{
    // csv file from random_seed.csv

    private const string scenarioFileName = "random_seed.csv";
    private const string scenarioFilePath = "Assets/";

    [SerializeField]
    private string permutationId;

    private Scenario currentScenario;
    public string CurrentActiveRoute = "";

    [Header("Settings")]
    public bool guiMode = false;   // GUI mode 开关

    // Public getter for permutationId
    public string GetPermutationId()
    {
        return permutationId;
    }

    #region  Unity Methods

    private void Awake()
    {
        if (!guiMode)
        {
            LoadScenarioData();
        }
    }

    private void Start()
    {
        if (guiMode)
        {
            LoadScenarioData();
        }
    }



    // // Start is called before the first frame update
    // // void Awake()
    // void Start()
    // {
    //     // Change to Awake() in case of non GUI Mode
    //     // Change to Start() in case of GUI mode
    //     LoadScenarioData();
    // }

    // Update is called once per frame
    void Update()
    {

    }
    #endregion

    #region Scenario Loading

    private void LoadScenarioData()
    {
        // Debug.Log($"ScenarioManager: Loading scenario data for permutation ID: {permutationId}");
        string filePath = Path.Combine(scenarioFilePath, scenarioFileName);
        if (File.Exists(filePath))
        {
            string[] lines = File.ReadAllLines(filePath);
            // Debug.Log($"ScenarioManager: Found {lines.Length} lines in CSV file");

            currentScenario = null; // Reset current scenario

            foreach (string line in lines)
            {
                //,Participant,Drive,Route_Order,R1_HMI,R2_HMI,H1,H2,H3,H4,H5,H6
                string[] values = line.Split(',');
                if (values.Length == 12)
                {
                    if (permutationId == values[0])
                    {
                        Debug.Log($"ScenarioManager: Found matching scenario for ID: {permutationId}");
                        // Parse the values and create a scenario object
                        currentScenario = new Scenario(
                            values[0], // id
                            values[1], // participant
                            values[2], // drive
                            values[3], // routeOrder
                            values[4], // r1HMI
                            values[5], // r2HMI
                            values[6], // h1
                            values[7], // h2
                            values[8], // h3
                            values[9], // h4
                            values[10], // h5
                            values[11]  // h6
                        );
                        
                        // Initialize CurrentActiveRoute based on RouteOrder
                        if (currentScenario.RouteOrder.StartsWith("R1"))
                        {
                            CurrentActiveRoute = "R1";
                        }
                        else
                        {
                            CurrentActiveRoute = "R2";
                        }
                        
                        break; // Found the scenario, exit loop
                    }
                }
            }
        }
        else
        {
            Debug.LogError("Scenario file not found: " + filePath);
        }
    }

    public Scenario GetCurrentScenario()
    {
        return currentScenario;
    }

    public string GetHMIType(string stationNumber)
    {
        if (currentScenario != null)
        {
            switch (stationNumber)
            {
                case "1":
                    return currentScenario.H1;
                case "2":
                    return currentScenario.H2;
                case "3":
                    return currentScenario.H3;
                case "4":
                    return currentScenario.H4;
                case "5":
                    return currentScenario.H5;
                case "6":
                    return currentScenario.H6;
                default:
                    Debug.LogWarning("Invalid station number: " + stationNumber);
                    return null;
            }
        }
        return null;
    }
    // 用于从外部（如GUI）设置simulationID并刷新场景数据
    public void SetPermutationId(string simulationID)
    {
        // Debug.Log($"ScenarioManager: Setting permutation ID to: {simulationID}");
        permutationId = simulationID;
        LoadScenarioData();

        // if (currentScenario != null)
        // {
        //     Debug.Log($"ScenarioManager: Successfully loaded scenario for ID: {simulationID}");
        // }
        // else
        // {
        //     Debug.LogWarning($"ScenarioManager: No scenario found for ID: {simulationID}");
        // }
    }
    #endregion

}
