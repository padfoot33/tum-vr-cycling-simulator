using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using CyclingExperiment.Camera;
using CyclingExperiment.AI;
using CyclingExperiment.Logging;
using CyclingExperiment.Scenarios;
using ExperimentRefs = CyclingExperiment.ExperimentSceneRefs;

namespace CyclingExperiment.UI
{
    /// <summary>
    /// Interactive in-game Scenario Selector UI & Teleportation System.
    /// Supports Combined Route 1, Route 2 Construction, Free Roam, Test Run, and Live Traffic ON/OFF Toggle.
    /// Press 'M' or 'Tab' to open menu; 'T' to toggle city traffic.
    /// </summary>
    public class ScenarioSelectionUI : MonoBehaviour
    {
        [Header("Starting Spawn Positions")]
        public Vector3 scenario1Position = new Vector3(436.1f, 0.2f, -80.0f);
        public float scenario1Heading = 0f;

        public Vector3 scenario2Position = new Vector3(721.5f, 0.2f, 70.0f);
        public float scenario2Heading = 0f;

        [Header("Cyclist")]
        [SerializeField, Tooltip("Play cap in km/h. Applied when you pick a scenario.")]
        private float cyclistMaxSpeedKph = 20f;

        [Header("Keys")]
        [SerializeField] private KeyCode toggleMenuKey = KeyCode.M;
        [SerializeField] private KeyCode toggleTrafficKey = KeyCode.T;

        [Header("Scene refs")]
        [SerializeField] private ExperimentRefs sceneRefs;

        private GameObject _modalPanel;
        private Canvas _canvas;
        private bool _isModalOpen;
        private Text _trafficBtnText;
        private OperatorSessionPanel _operatorPanel;

        public bool IsModalOpen => _isModalOpen;

        private ExperimentRefs Refs => sceneRefs != null ? sceneRefs : ExperimentRefs.Instance;

        private void Awake()
        {
            if (sceneRefs == null) sceneRefs = ExperimentRefs.EnsureExists();
            EnsureEventSystemExists();
        }

        private void Start()
        {
            EnsureEventSystemExists();
            if (AlmostEqual(scenario2Position, new Vector3(450.0f, 0.2f, 0.0f)))
            {
                scenario2Position = Scenario3_ConstructionNarrowing.ApproachPosition;
                scenario2Heading = Scenario3_ConstructionNarrowing.ApproachHeading;
            }

            EnsureOperatorSessionPanel();

            bool locked = ExperimentBuildSession.LocksParticipantUi;
            if (!locked)
                CreateScenarioUI();

            ShowModal(false);

            int route = ExperimentBuildSession.IsActive ? ExperimentBuildSession.RouteIndex : 1;
            SelectScenario(route);
        }

        private void EnsureEventSystemExists()
        {
            if (Refs != null && Refs.eventSystem != null) return;

            GameObject eventSystemObj = new GameObject("EventSystem");
            var created = eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<StandaloneInputModule>();
            if (Refs != null) Refs.eventSystem = created;
        }

        private void Update()
        {
            if (ExperimentBuildSession.LocksParticipantUi) return;

            if (Input.GetKeyDown(toggleMenuKey) || Input.GetKeyDown(KeyCode.Tab))
            {
                ToggleModal();
            }

            if (Input.GetKeyDown(toggleTrafficKey))
            {
                ToggleGlobalTraffic();
            }

            if (_isModalOpen)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) SelectScenario(1);
                if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) SelectScenario(2);
                if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3) || Input.GetKeyDown(KeyCode.Escape)) SelectScenario(0);
                if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) SelectScenario(3);
            }
        }

        public void ToggleModal()
        {
            ShowModal(!_isModalOpen);
        }

        public void ShowModal(bool show)
        {
            _isModalOpen = show;
            if (_modalPanel != null)
            {
                _modalPanel.SetActive(show);
            }

            if (_operatorPanel != null)
                _operatorPanel.ApplyCursor();
            else
            {
                Cursor.lockState = show ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = show;
            }
        }

        private void EnsureOperatorSessionPanel()
        {
            if (_operatorPanel == null)
                _operatorPanel = GetComponent<OperatorSessionPanel>();
            if (_operatorPanel == null)
                _operatorPanel = gameObject.AddComponent<OperatorSessionPanel>();
        }

        private void CreateScenarioUI()
        {
            GameObject canvasObj = new GameObject("ScenarioUI_Canvas");
            canvasObj.transform.SetParent(transform);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200;
            _canvas.pixelPerfect = true;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.dynamicPixelsPerUnit = 2.0f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // Modal Background Overlay
            _modalPanel = new GameObject("ScenarioModal_Panel");
            _modalPanel.transform.SetParent(canvasObj.transform, false);

            var panelRect = _modalPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.sizeDelta = Vector2.zero;

            var panelBg = _modalPanel.AddComponent<Image>();
            panelBg.color = new Color(0.04f, 0.07f, 0.12f, 0.92f);
            panelBg.raycastTarget = true;

            // Modal Card
            GameObject card = new GameObject("ModalCard");
            card.transform.SetParent(_modalPanel.transform, false);
            var cardRect = card.AddComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(760, 640);
            cardRect.anchoredPosition = Vector2.zero;

            var cardBg = card.AddComponent<Image>();
            cardBg.color = new Color(0.11f, 0.15f, 0.22f, 0.98f);

            // Title
            CreateText(card.transform, "Title", "TUM VR CYCLING EXPERIMENT", new Vector2(0, 260), 28, FontStyle.Bold, Color.white);
            CreateText(card.transform, "Subtitle", "Select a scenario to start simulation (or press 1, 2, 3, 4):", new Vector2(0, 215), 18, FontStyle.Normal, new Color(0.75f, 0.85f, 0.95f));

            // Buttons
            CreateScenarioButton(card.transform, "[1]  Route 1: Combined Bus Stop & Right-Turn Sequence\n<size=15><color=#90CDF4>Gabelsbergerstr. • Bus Overtake & Park ➔ Red Strip Right-Turn</color></size>", new Vector2(0, 135), () => SelectScenario(1));
            CreateScenarioButton(card.transform, "[2]  Route 2: Construction Narrowing Sequence\n<size=15><color=#90CDF4>Narrowed Roadway Chute with Passing Traffic Squeeze</color></size>", new Vector2(0, 50), () => SelectScenario(2));
            CreateScenarioButton(card.transform, "[3]  Free Roam & City Exploration\n<size=15><color=#A0AEC0>Freely Ride and Explore the Munich TUM Campus with Ambient Traffic</color></size>", new Vector2(0, -35), () => SelectScenario(0));
            CreateScenarioButton(card.transform, "[4]  Test Run\n<size=15><color=#A0AEC0>Free roam with no traffic and no scenario events</color></size>", new Vector2(0, -120), () => SelectScenario(3));

            // Traffic Toggle inside modal
            CreateTrafficToggleButton(card.transform, new Vector2(0, -200));

            // Hint
            CreateText(card.transform, "Hint", "Press 'M' for Menu  •  Press 'T' to Toggle Traffic  •  Press 'V' for Cockpit View", new Vector2(0, -275), 16, FontStyle.Italic, new Color(0.6f, 0.75f, 0.9f));
        }

        private void CreateTopButtons(Transform parent)
        {
            // Menu Button
            GameObject menuBtnObj = new GameObject("Top_Menu_Button");
            menuBtnObj.transform.SetParent(parent, false);

            var rect = menuBtnObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-25, -25);
            rect.sizeDelta = new Vector2(170, 46);

            var img = menuBtnObj.AddComponent<Image>();
            img.color = new Color(0.18f, 0.48f, 0.88f, 0.95f);
            img.raycastTarget = true;

            var btn = menuBtnObj.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(ToggleModal);
            CreateText(menuBtnObj.transform, "BtnText", "☰ Scenarios [M]", Vector2.zero, 17, FontStyle.Bold, Color.white);

            // Traffic Toggle Button
            GameObject trafficBtnObj = new GameObject("Top_Traffic_Button");
            trafficBtnObj.transform.SetParent(parent, false);

            var tRect = trafficBtnObj.AddComponent<RectTransform>();
            tRect.anchorMin = new Vector2(1, 1);
            tRect.anchorMax = new Vector2(1, 1);
            tRect.pivot = new Vector2(1, 1);
            tRect.anchoredPosition = new Vector2(-205, -25);
            tRect.sizeDelta = new Vector2(170, 46);

            var tImg = trafficBtnObj.AddComponent<Image>();
            tImg.color = new Color(0.15f, 0.65f, 0.40f, 0.95f);
            tImg.raycastTarget = true;

            var tBtn = trafficBtnObj.AddComponent<Button>();
            tBtn.targetGraphic = tImg;
            tBtn.onClick.AddListener(ToggleGlobalTraffic);
            _trafficBtnText = CreateText(trafficBtnObj.transform, "TBtnText", "🚗 Traffic: ON [T]", Vector2.zero, 16, FontStyle.Bold, Color.white);
        }

        private void CreateTrafficToggleButton(Transform parent, Vector2 pos)
        {
            GameObject btnObj = new GameObject("Modal_Traffic_Toggle");
            btnObj.transform.SetParent(parent, false);

            var rect = btnObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400, 50);
            rect.anchoredPosition = pos;

            var img = btnObj.AddComponent<Image>();
            img.color = new Color(0.15f, 0.55f, 0.35f, 1f);
            img.raycastTarget = true;

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(ToggleGlobalTraffic);

            CreateText(btnObj.transform, "Text", "🚗 Ambient City Traffic: ON / OFF (Press T)", Vector2.zero, 16, FontStyle.Bold, Color.white);
        }

        private void CreateScenarioButton(Transform parent, string text, Vector2 pos, UnityEngine.Events.UnityAction action)
        {
            GameObject btnObj = new GameObject("Btn_" + pos.y);
            btnObj.transform.SetParent(parent, false);

            var rect = btnObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(680, 70);
            rect.anchoredPosition = pos;

            var img = btnObj.AddComponent<Image>();
            img.color = new Color(0.18f, 0.25f, 0.36f, 1f);
            img.raycastTarget = true;

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.highlightedColor = new Color(0.25f, 0.42f, 0.65f, 1f);
            colors.pressedColor = new Color(0.10f, 0.20f, 0.35f, 1f);
            btn.colors = colors;

            btn.onClick.AddListener(action);

            CreateText(btnObj.transform, "Text", text, Vector2.zero, 18, FontStyle.Normal, Color.white);
        }

        private Text CreateText(Transform parent, string name, string content, Vector2 pos, int fontSize, FontStyle style, Color color)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);

            var rect = textObj.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(680, 70);

            var text = textObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.supportRichText = true;
            text.raycastTarget = false;

            return text;
        }

        public void ToggleGlobalTraffic()
        {
            if (ExperimentBuildSession.LocksParticipantUi) return;

            var trafficMgr = Refs != null ? Refs.cityTraffic : null;
            if (trafficMgr == null) return;

            trafficMgr.ToggleTraffic();
            bool state = trafficMgr.IsTrafficEnabled;
            if (_trafficBtnText != null)
            {
                _trafficBtnText.text = state ? "🚗 Traffic: ON [T]" : "🚗 Traffic: OFF [T]";
            }
        }

        public void SelectScenario(int scenarioIndex)
        {
            ShowModal(false);

            GameObject bike = Refs != null ? Refs.bicycle : null;
            if (bike == null)
            {
                Debug.LogError("[ScenarioSelectionUI] Bicycle reference is missing. Assign ExperimentSceneRefs.");
                return;
            }

            ApplyCyclistSpeedLimit(Refs.Cyclist);

            if (Refs.route1 != null) Refs.route1.ResetScenario();

            bool leavingTestRun = ExperimentBuildSession.IsTestRun && scenarioIndex != ExperimentBuildSession.TestRunRouteIndex;
            ExperimentBuildSession.SetPlayTestRun(scenarioIndex == ExperimentBuildSession.TestRunRouteIndex);

            switch (scenarioIndex)
            {
                case 1:
                    RestoreTrafficAfterTestRun(leavingTestRun);
                    if (Refs.route1CyclistSpawn != null)
                    {
                        Vector3 spawnPos = Refs.route1CyclistSpawn.position;
                        float spawnHeading = Refs.route1CyclistSpawn.eulerAngles.y;
                        TeleportBike(Refs.Cyclist, bike.transform, spawnPos, spawnHeading);
                    }
                    else
                    {
                        Debug.LogWarning("[ScenarioSelectionUI] Cyclist_Spawn_Route1 missing; using fallback spawn.");
                        TeleportBike(Refs.Cyclist, bike.transform, scenario1Position, scenario1Heading);
                    }
                    BeginRunLog("Route1_BusStop_RightTurn", "C1", "approach");
                    if (EventMarkerLogger.Instance != null) EventMarkerLogger.Instance.LogEvent("ROUTE1_START");
                    break;

                case 2:
                    RestoreTrafficAfterTestRun(leavingTestRun);
                    if (Refs.route2CyclistSpawn != null)
                    {
                        TeleportBike(Refs.Cyclist, bike.transform,
                            Refs.route2CyclistSpawn.position, Refs.route2CyclistSpawn.eulerAngles.y);
                    }
                    else
                    {
                        TeleportBike(Refs.Cyclist, bike.transform,
                            Scenario3_ConstructionNarrowing.ApproachPosition, Scenario3_ConstructionNarrowing.ApproachHeading);
                    }
                    BeginRunLog("Route2_Construction", "C1", "approach");
                    if (EventMarkerLogger.Instance != null) EventMarkerLogger.Instance.LogEvent("ROUTE2_START");
                    break;

                case 3:
                    if (Refs.cityTraffic != null) Refs.cityTraffic.SetTrafficEnabled(false);
                    if (Refs.intersectionTraffic != null) Refs.intersectionTraffic.StopTrafficFlow();
                    BeginRunLog("TestRun", "C0", "debug");
                    if (EventMarkerLogger.Instance != null) EventMarkerLogger.Instance.LogEvent("TEST_RUN_START");
                    break;

                case 0:
                default:
                    RestoreTrafficAfterTestRun(leavingTestRun);
                    BeginRunLog("FreeRoam", "C1", "approach");
                    break;
            }

            Refs.ApplyPlayArea(scenarioIndex == ExperimentBuildSession.TestRunRouteIndex ? 0 : scenarioIndex);

            if (Refs.followCamera != null) Refs.followCamera.SnapToTarget();
        }

        private void RestoreTrafficAfterTestRun(bool leavingTestRun)
        {
            if (!leavingTestRun || ExperimentBuildSession.LocksParticipantUi) return;
            if (ExperimentBuildSession.IsActive && !ExperimentBuildSession.TrafficEnabled) return;
            if (Refs == null || Refs.cityTraffic == null) return;
            Refs.cityTraffic.SetTrafficEnabled(true);
        }

        private void BeginRunLog(string scenarioName, string segmentId, string taskContext)
        {
            var logger = ExperimentRunLogger.Instance;

            if (logger == null && Refs != null)
            {
                Refs.EnsureRunLogger();
                logger = Refs.runLogger;
            }

            if (logger == null)
            {
                Debug.LogError("[ScenarioSelectionUI] ExperimentRunLogger could not be created.");
                return;
            }

            logger.StartLogging(scenarioName, segmentId, taskContext);
        }

        private static bool AlmostEqual(Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude < 0.01f;
        }

        private static void TeleportBike(ICyclistMotion motion, Transform bike, Vector3 position, float heading)
        {
            if (motion != null)
            {
                motion.Teleport(position, heading);
                return;
            }

            bike.position = position;
            bike.rotation = Quaternion.Euler(0, heading, 0);
        }

        private void ApplyCyclistSpeedLimit(ICyclistMotion motion)
        {
            if (motion == null) return;

            float kph = Mathf.Max(1f, cyclistMaxSpeedKph);
            motion.MaxSpeedMps = kph / 3.6f;
            motion.StopLongitudinalSpeed();
        }

    }
}
