using UnityEngine;

public class FrameRateController : MonoBehaviour
{
    [Header("帧率控制参数")]
    [Tooltip("是否启用垂直同步（VSync）")]
    public bool useVSync = false;

    [Tooltip("目标帧率（-1 表示不限制）")]
    public int targetFrameRate = -1;

    [Header("显示设置")]
    public bool showFPS = true;
    public Color textColor = Color.yellow;
    public int fontSize = 24;
    public Vector2 screenPosition = new Vector2(10, 10);

    private float deltaTime = 0.0f;

    void Awake()
    {
        ApplySettings();
    }

    void Update()
    {
        deltaTime += (Time.deltaTime - deltaTime) * 0.1f;
    }

    void ApplySettings()
    {
        QualitySettings.vSyncCount = useVSync ? 1 : 0;
        Application.targetFrameRate = targetFrameRate;
        Debug.Log($"[FrameRateController] VSync = {useVSync}, TargetFPS = {targetFrameRate}");
    }

    void OnValidate() // 编辑器里修改时自动更新
    {
        ApplySettings();
    }

    void OnGUI()
    {
        if (!showFPS) return;

        float msec = deltaTime * 1000.0f;
        float fps = 1.0f / deltaTime;
        string text = $"{msec:0.0} ms ({fps:0.} FPS)  |  " +
                      $"VSync: {(useVSync ? "ON" : "OFF")}  |  Target: {(targetFrameRate < 0 ? "Unlimited" : targetFrameRate.ToString())}";

        GUIStyle style = new GUIStyle();
        style.fontSize = fontSize;
        style.normal.textColor = textColor;
        GUI.Label(new Rect(screenPosition.x, screenPosition.y, 600, 50), text, style);
    }

    // 运行时快捷键控制
    void LateUpdate()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            useVSync = !useVSync;
            ApplySettings();
        }
        if (Input.GetKeyDown(KeyCode.F2))
        {
            targetFrameRate = (targetFrameRate == 60) ? -1 : 60;
            ApplySettings();
        }
    }
}
