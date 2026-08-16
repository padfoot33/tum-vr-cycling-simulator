using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CommandLineHandler : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool debugMode = true;
    
    [Header("Settings")]
    [SerializeField] private bool dontDestroyOnLoad = true;
    
    void Awake()
    {
        // 确保这个对象在场景切换时不被销毁
        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }
        
        ParseCommandLineArguments();
    }

    private void ParseCommandLineArguments()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        
        if (debugMode)
        {
            Debug.Log($"=== CommandLineHandler Debug Info ===");
            Debug.Log($"Command line arguments received: {string.Join(" ", args)}");
            Debug.Log($"Total arguments count: {args.Length}");
            
            // 显示每个参数
            for (int i = 0; i < args.Length; i++)
            {
                Debug.Log($"  Arg[{i}]: {args[i]}");
            }
        }
        
        string simulationId = null;
        string participantId = null;
        
        // Look for --id and --Participantid parameters
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--id")
            {
                simulationId = args[i + 1];
                if (debugMode)
                {
                    Debug.Log($"Found simulation ID: {simulationId}");
                }
            }
            else if (args[i] == "--Participantid")
            {
                participantId = args[i + 1];
                if (debugMode)
                {
                    Debug.Log($"Found participant ID: {participantId}");
                }
            }
        }
        
        // 如果找到了simulation ID，设置到ScenarioManager
        if (!string.IsNullOrEmpty(simulationId))
        {
            StartCoroutine(SetSimulationParametersWithRetry(simulationId, participantId));
        }
        else
        {
            if (debugMode)
            {
                Debug.LogWarning("No --id parameter found in command line arguments");
            }
        }
    }
    
    private IEnumerator SetSimulationParametersWithRetry(string simulationId, string participantId)
    {
        int maxRetries = 10;
        int retryCount = 0;

        if (debugMode)
        {
            Debug.Log($"Attempting to set simulation parameters:");
            Debug.Log($"  Simulation ID: {simulationId}");
            Debug.Log($"  Participant ID: {participantId ?? "not provided"}");
        }

        // 计算 result
        int simIdInt, participantIdInt;
        if (!int.TryParse(simulationId, out simIdInt))
        {
            Debug.LogError($"❌ Invalid simulationId: {simulationId}");
            yield break;
        }
        if (!int.TryParse(participantId, out participantIdInt))
        {
            Debug.LogError($"❌ Invalid participantId: {participantId}");
            yield break;
        }

        int result = 3 * (participantIdInt - 1) + (simIdInt - 1);
        string resultStr = result.ToString();

        if (debugMode)
        {
            Debug.Log($"[CommandLineHandler] ✅ Calculated permutation ID: {resultStr} (from SimID={simulationId}, ParticipantID={participantId})");
        }

        while (retryCount < maxRetries)
        {
            ScenarioManager scenarioManager = FindObjectOfType<ScenarioManager>();
            if (scenarioManager != null)
            {
                // Pass the calculated permutation ID to ScenarioManager
                scenarioManager.SetPermutationId(resultStr);
                Debug.Log($"[CommandLineHandler] ✅ SetPermutationId({resultStr}) called on ScenarioManager");
                yield break;
            }

            retryCount++;
            if (debugMode)
            {
                Debug.Log($"[CommandLineHandler] ⏳ ScenarioManager not found, retry {retryCount}/{maxRetries}");
            }

            yield return new WaitForSeconds(0.1f);
        }

        Debug.LogError("[CommandLineHandler] ❌ ScenarioManager not found after maximum retries!");
    }

}
