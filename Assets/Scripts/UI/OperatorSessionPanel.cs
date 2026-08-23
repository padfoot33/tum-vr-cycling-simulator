using UnityEngine;
using UnityEngine.UI;
using CyclingExperiment.Logging;
using ExperimentRefs = CyclingExperiment.ExperimentSceneRefs;

namespace CyclingExperiment.UI
{
    /// <summary>
    /// Hidden operator overlay (Ctrl+Space) to edit participant id and trial index at runtime.
    /// Drawn on Display 4 (rider / CAVE view). Stays available when the participant lock hides the scenario menu.
    /// </summary>
    public class OperatorSessionPanel : MonoBehaviour
    {
        [SerializeField, Tooltip("0 = Display 1. Rider view is Display 4.")]
        private int targetDisplayIndex = 3;
        [SerializeField] private ExperimentRefs sceneRefs;
        [SerializeField] private ScenarioSelectionUI scenarioUi;

        private GameObject _root;
        private InputField _participantField;
        private InputField _trialField;
        private Text _statusText;
        private bool _isOpen;
        private bool _suppressFieldEvents;

        public bool IsOpen => _isOpen;

        public static bool BlocksGameplaySpace { get; private set; }

        private ExperimentRefs Refs => sceneRefs != null ? sceneRefs : ExperimentRefs.Instance;

        private ExperimentRunLogger Logger
        {
            get
            {
                if (ExperimentRunLogger.Instance != null)
                    return ExperimentRunLogger.Instance;
                return Refs != null ? Refs.runLogger : null;
            }
        }

        private void Awake()
        {
            if (sceneRefs == null)
                sceneRefs = ExperimentRefs.Instance;
            if (scenarioUi == null)
                scenarioUi = GetComponent<ScenarioSelectionUI>();
            CreateUi();
            Show(false);
        }

        private void Update()
        {
            BlocksGameplaySpace = _isOpen || ToggleModifierHeld();
            if (ToggleModifierHeld() && Input.GetKeyDown(KeyCode.Space))
                Show(!_isOpen);
        }

        private void OnDisable()
        {
            BlocksGameplaySpace = false;
        }

        private static bool ToggleModifierHeld()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        }

        public void Show(bool show)
        {
            _isOpen = show;
            if (_root != null)
                _root.SetActive(show);

            if (show)
                RefreshFromLogger();

            ApplyCursor();
        }

        public void ApplyCursor()
        {
            bool scenarioOpen = scenarioUi != null && scenarioUi.IsModalOpen;
            bool showCursor = _isOpen || scenarioOpen;
            Cursor.lockState = showCursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = showCursor;
        }

        private void CreateUi()
        {
            GameObject canvasObj = new GameObject("OperatorSession_Canvas");
            canvasObj.transform.SetParent(transform, false);

            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 210;
            canvas.pixelPerfect = true;
            canvas.targetDisplay = Mathf.Clamp(targetDisplayIndex, 0, 7);

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            _root = new GameObject("OperatorSession_Root");
            _root.transform.SetParent(canvasObj.transform, false);

            var rootRect = _root.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;

            var overlay = _root.AddComponent<Image>();
            overlay.color = new Color(0.04f, 0.07f, 0.12f, 0.55f);
            overlay.raycastTarget = true;

            GameObject card = new GameObject("OperatorSession_Card");
            card.transform.SetParent(_root.transform, false);
            var cardRect = card.AddComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(560, 360);
            cardRect.anchoredPosition = Vector2.zero;

            var cardBg = card.AddComponent<Image>();
            cardBg.color = new Color(0.11f, 0.15f, 0.22f, 0.98f);

            CreateLabel(card.transform, "Title", "OPERATOR SESSION", new Vector2(0, 140), 22, FontStyle.Bold, Color.white);
            CreateLabel(card.transform, "Hint", "Ctrl+Space to hide  •  Next log file uses these IDs", new Vector2(0, 108), 14, FontStyle.Italic, new Color(0.7f, 0.82f, 0.92f));

            CreateStepperRow(card.transform, "Participant", new Vector2(0, 48), out _participantField,
                OnParticipantMinus, OnParticipantPlus);
            _participantField.onEndEdit.AddListener(OnParticipantEndEdit);

            CreateStepperRow(card.transform, "Trial", new Vector2(0, -16), out _trialField,
                OnTrialMinus, OnTrialPlus);
            _trialField.contentType = InputField.ContentType.IntegerNumber;
            _trialField.onEndEdit.AddListener(OnTrialEndEdit);

            _statusText = CreateLabel(card.transform, "Status", "", new Vector2(0, -78), 14, FontStyle.Normal, new Color(0.85f, 0.9f, 0.95f));
            var statusRect = _statusText.GetComponent<RectTransform>();
            statusRect.sizeDelta = new Vector2(500, 56);

            CreateButton(card.transform, "Start new log", new Vector2(0, -140), new Vector2(240, 44),
                new Color(0.18f, 0.48f, 0.88f, 1f), StartNewLog);
        }

        private void CreateStepperRow(
            Transform parent,
            string label,
            Vector2 pos,
            out InputField field,
            UnityEngine.Events.UnityAction onMinus,
            UnityEngine.Events.UnityAction onPlus)
        {
            GameObject row = new GameObject(label + "Row");
            row.transform.SetParent(parent, false);
            var rowRect = row.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(500, 44);
            rowRect.anchoredPosition = pos;

            CreateLabel(row.transform, "Label", label, new Vector2(-180, 0), 16, FontStyle.Bold, Color.white)
                .GetComponent<RectTransform>().sizeDelta = new Vector2(120, 40);

            CreateButton(row.transform, "−", new Vector2(-70, 0), new Vector2(44, 40),
                new Color(0.28f, 0.22f, 0.22f, 1f), onMinus);

            field = CreateInputField(row.transform, label + "Field", new Vector2(40, 0), new Vector2(140, 40));

            CreateButton(row.transform, "+", new Vector2(150, 0), new Vector2(44, 40),
                new Color(0.18f, 0.38f, 0.28f, 1f), onPlus);
        }

        private InputField CreateInputField(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.08f, 0.1f, 0.14f, 1f);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8, 2);
            textRect.offsetMax = new Vector2(-8, -2);

            var text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.supportRichText = false;
            text.raycastTarget = false;

            var input = go.AddComponent<InputField>();
            input.textComponent = text;
            input.targetGraphic = img;
            input.characterLimit = 16;
            return input;
        }

        private Button CreateButton(
            Transform parent,
            string label,
            Vector2 pos,
            Vector2 size,
            Color color,
            UnityEngine.Events.UnityAction action)
        {
            GameObject go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = color;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(action);

            CreateLabel(go.transform, "Text", label, Vector2.zero, 16, FontStyle.Bold, Color.white)
                .GetComponent<RectTransform>().sizeDelta = size;
            return btn;
        }

        private static Text CreateLabel(Transform parent, string name, string content, Vector2 pos, int fontSize, FontStyle style, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(500, 32);

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        private void OnParticipantMinus()
        {
            ApplyParticipantField();
            Logger?.DecrementParticipant();
            RefreshFromLogger();
        }

        private void OnParticipantPlus()
        {
            ApplyParticipantField();
            Logger?.IncrementParticipant();
            RefreshFromLogger();
        }

        private void OnTrialMinus()
        {
            ApplyTrialField();
            Logger?.DecrementTrial();
            RefreshFromLogger();
        }

        private void OnTrialPlus()
        {
            ApplyTrialField();
            Logger?.IncrementTrial();
            RefreshFromLogger();
        }

        private void OnParticipantEndEdit(string _)
        {
            if (_suppressFieldEvents)
                return;
            ApplyParticipantField();
            RefreshFromLogger();
        }

        private void OnTrialEndEdit(string _)
        {
            if (_suppressFieldEvents)
                return;
            ApplyTrialField();
            RefreshFromLogger();
        }

        private void ApplyParticipantField()
        {
            var logger = Logger;
            if (logger == null || _participantField == null)
                return;
            logger.SetParticipantId(_participantField.text);
        }

        private void ApplyTrialField()
        {
            var logger = Logger;
            if (logger == null || _trialField == null)
                return;

            if (int.TryParse(_trialField.text, out int trial))
                logger.SetTrialIndex(trial);
        }

        private void StartNewLog()
        {
            ApplyParticipantField();
            ApplyTrialField();

            var logger = Logger;
            if (logger == null)
                return;

            logger.RestartLogging();
            RefreshFromLogger();
        }

        private void RefreshFromLogger()
        {
            var logger = Logger;
            _suppressFieldEvents = true;
            if (logger != null)
            {
                if (_participantField != null)
                    _participantField.text = logger.ParticipantId;
                if (_trialField != null)
                    _trialField.text = logger.TrialIndex.ToString();
            }
            _suppressFieldEvents = false;

            if (_statusText == null)
                return;

            if (logger == null)
            {
                _statusText.text = "Logger not ready.";
                return;
            }

            string logging = logger.IsLogging ? "LOGGING" : "idle";
            string scenario = string.IsNullOrEmpty(logger.ScenarioName) ? "—" : logger.ScenarioName;
            string path = string.IsNullOrEmpty(logger.RunFilePath) ? "no file yet" : logger.RunFilePath;
            _statusText.text = $"{logging}  •  {logger.ParticipantId}  trial {logger.TrialIndex}  •  {scenario}\n{path}";
        }
    }
}
