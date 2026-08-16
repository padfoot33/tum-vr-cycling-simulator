using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugLogsManager : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField]
    private bool enableDebugLogs = true;
    public bool EnableDebugLogs => enableDebugLogs;

    // Start is called before the first frame update
    void Awake()
    {
        // Initialize the DebugLogsManager
        if (enableDebugLogs)
        {
            Debug.Log("DebugLogsManager initialized. Debug logs are enabled.");
        }
        else
        {
            Debug.Log("DebugLogsManager initialized. Debug logs are disabled.");
        }
    }
}
