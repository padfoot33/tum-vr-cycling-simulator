using UnityEngine;

namespace CyclingExperiment.UI
{
    /// <summary>
    /// Optional operator HUD. Speed, scenario banners, and toasts stay off in Play
    /// so participants do not see control instructions.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, Tooltip("Bicycle controller or ICyclistMotion adapter")]
        private MonoBehaviour _bicycleController;

        private ICyclistMotion _cyclist;

        [Header("Display Settings")]
        [SerializeField] private bool _showSpeed = false;
        [SerializeField] private bool _showScenarioInfo = false;
        [SerializeField] private bool _showDebugInfo = false;

        // Internal UI references
        private UnityEngine.UI.Text _speedText;
        private UnityEngine.UI.Text _scenarioText;
        private UnityEngine.UI.Text _debugText;
        private Canvas _canvas;

        // Cached method info for getting speed
        private System.Reflection.MethodInfo _getSpeedKphMethod;

        private void Start()
        {
            CreateHUD();
            BindCyclist();
            CacheSpeedMethod();
        }

        private void BindCyclist()
        {
            var refs = ExperimentSceneRefs.Instance;
            if (refs != null && refs.Cyclist != null)
            {
                _bicycleController = refs.Cyclist as MonoBehaviour;
                _cyclist = refs.Cyclist;
                return;
            }

            _cyclist = _bicycleController as ICyclistMotion;
        }

        private void CacheSpeedMethod()
        {
            if (_bicycleController != null)
            {
                _getSpeedKphMethod = _bicycleController.GetType().GetMethod("GetSpeedKph");
                if (_getSpeedKphMethod == null)
                {
                    Debug.LogWarning("[HUDController] Could not find GetSpeedKph() method on bicycle controller.");
                }
            }
        }

        private void CreateHUD()
        {
            if (!_showSpeed && !_showScenarioInfo && !_showDebugInfo)
                return;

            GameObject canvasObj = new GameObject("HUD_Canvas");
            canvasObj.transform.SetParent(transform);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>().uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Speed display (top-left)
            if (_showSpeed)
            {
                _speedText = CreateTextElement(canvasObj.transform, "SpeedText",
                    new Vector2(20, -20), TextAnchor.UpperLeft, 28);
                _speedText.text = "0 km/h";
            }

            // Scenario info (top-center)
            if (_showScenarioInfo)
            {
                _scenarioText = CreateTextElement(canvasObj.transform, "ScenarioText",
                    new Vector2(0, -20), TextAnchor.UpperCenter, 22);
                _scenarioText.text = "";
            }

            // Debug info (bottom-left)
            if (_showDebugInfo)
            {
                _debugText = CreateTextElement(canvasObj.transform, "DebugText",
                    new Vector2(20, 60), TextAnchor.LowerLeft, 16);
                _debugText.text = "";
            }
        }

        private UnityEngine.UI.Text CreateTextElement(Transform parent, string name, Vector2 anchoredPos, TextAnchor anchor, int fontSize)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);

            RectTransform rect = textObj.AddComponent<RectTransform>();

            // Set anchors based on alignment
            switch (anchor)
            {
                case TextAnchor.UpperLeft:
                    rect.anchorMin = new Vector2(0, 1);
                    rect.anchorMax = new Vector2(0, 1);
                    rect.pivot = new Vector2(0, 1);
                    break;
                case TextAnchor.UpperCenter:
                    rect.anchorMin = new Vector2(0.5f, 1);
                    rect.anchorMax = new Vector2(0.5f, 1);
                    rect.pivot = new Vector2(0.5f, 1);
                    break;
                case TextAnchor.LowerLeft:
                    rect.anchorMin = new Vector2(0, 0);
                    rect.anchorMax = new Vector2(0, 0);
                    rect.pivot = new Vector2(0, 0);
                    break;
                default:
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    break;
            }

            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(400, 50);

            UnityEngine.UI.Text text = textObj.AddComponent<UnityEngine.UI.Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = anchor;

            // Add shadow for readability
            var shadow = textObj.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(1, -1);

            // Add outline for extra readability
            var outline = textObj.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.5f);
            outline.effectDistance = new Vector2(1, -1);

            return text;
        }

        private void Update()
        {
            UpdateSpeedDisplay();
            UpdateScenarioDisplay();
            UpdateDebugDisplay();
        }

        private void UpdateSpeedDisplay()
        {
            if (_speedText == null || _bicycleController == null) return;

            float speed = 0f;
            if (_cyclist != null)
            {
                speed = _cyclist.GetSpeedKph();
            }
            else if (_getSpeedKphMethod != null)
            {
                speed = (float)_getSpeedKphMethod.Invoke(_bicycleController, null);
            }

            _speedText.text = $"{speed:F0} km/h";
        }

        private void UpdateScenarioDisplay()
        {
            if (_scenarioText == null) return;

            // Try to find ScenarioManager
            var scenarioManager = Scenarios.ScenarioManager.Instance;
            if (scenarioManager != null)
            {
                if (scenarioManager.IsScenarioActive)
                {
                    _scenarioText.text = $"<color=yellow>▶ {scenarioManager.ActiveScenarioName}</color>";
                }
                else
                {
                    _scenarioText.text = $"Condition: {scenarioManager.CurrentCondition}";
                }
            }
        }

        private void UpdateDebugDisplay()
        {
            if (_debugText == null || _bicycleController == null) return;

            Vector3 pos = _bicycleController.transform.position;
            _debugText.text = $"Pos: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})\nFPS: {(1f / Time.deltaTime):F0}";
        }

        /// <summary>
        /// Set the bicycle controller reference at runtime.
        /// </summary>
        public void SetBicycleController(MonoBehaviour controller)
        {
            _bicycleController = controller;
            _cyclist = controller as ICyclistMotion;
            CacheSpeedMethod();
        }

        /// <summary>
        /// Participant Play has no on-screen toasts. Kept so callers stay valid.
        /// </summary>
        public void ShowMessage(string message, float duration = 3f)
        {
        }

    }
}
