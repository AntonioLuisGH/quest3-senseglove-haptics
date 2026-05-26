using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class TestingManager : MonoBehaviour
{
    public static TestingManager Instance { get; private set; }

    private HapticMode _currentMode = HapticMode.FullFeedback;
    private readonly List<IHapticTarget> _targets = new();

    private readonly string[] _sceneNames = { "Calibration_Scene", "Familiarization_Scene", "Egg_Scene", "Sponge_Scene" };

    private readonly string[] _modeLabels = { "Full Feedback [D]", "Only Force [B]", "Only Vibration [C]", "No Feedback [A]" };

    private GameObject _panel;
    private bool _panelVisible = true;
    private Text _targetCountLabel;
    private readonly List<Button> _modeButtons = new();

    private float _leftOffsetX = 0f;
    private float _rightOffsetX = 0f;
    private InputField _leftOffsetField;
    private InputField _rightOffsetField;
    private Text _handStatusLabel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void CreateInstance()
    {
        if (Instance != null) return;
        var go = new GameObject("TestingManager");
        go.AddComponent<TestingManager>();
        go.AddComponent<HandOffsetController>();
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        BuildUI();
        EnsureEventSystem();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _targets.Clear();
        UpdateTargetCountLabel();

        var listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        for (int i = 1; i < listeners.Length; i++)
            listeners[i].enabled = false;

        EnsureEventSystem();
        UpdateOffsetLabels();
    }

    void EnsureEventSystem()
    {
        var existing = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
        if (existing.Length == 0)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(esGO);
        }
        else if (existing.Length > 1)
        {
            // Keep the first, destroy the rest
            for (int i = 1; i < existing.Length; i++)
                Destroy(existing[i].gameObject);
        }
    }

    public void RegisterTarget(IHapticTarget target)
    {
        if (!_targets.Contains(target)) _targets.Add(target);
        UpdateTargetCountLabel();
    }

    public void UnregisterTarget(IHapticTarget target)
    {
        _targets.Remove(target);
        UpdateTargetCountLabel();
    }

    void ApplyModeToAll()
    {
        foreach (var t in _targets)
            t.ApplyHapticMode(_currentMode);
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("TestingManagerCanvas");
        DontDestroyOnLoad(canvasGO);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Toggle button ────────────────────────────────────────────
        var toggleBtn = MakeButton(canvasGO.transform, "Hide Panel",
            new Rect(10, 10, 260, 68));
        toggleBtn.onClick.AddListener(() =>
        {
            _panelVisible = !_panelVisible;
            _panel.SetActive(_panelVisible);
            toggleBtn.GetComponentInChildren<Text>().text =
                _panelVisible ? "Hide Panel" : "Show Panel";
        });

        _panel = MakePanel(canvasGO.transform, new Rect(10, 84, 540, 1100));

        float y = 16;

        // ── Scene buttons ────────────────────────────────────────────
        MakeLabel(_panel.transform, "── Load Scene ──", new Rect(16, y, 508, 44));
        y += 52;
        foreach (var sceneName in _sceneNames)
        {
            var name = sceneName;
            var btn = MakeButton(_panel.transform, sceneName, new Rect(16, y, 508, 60));
            btn.onClick.AddListener(() => SceneManager.LoadScene(name));
            y += 68;
        }

        y += 8;

        // ── Haptic mode buttons ──────────────────────────────────────
        MakeLabel(_panel.transform, "── Haptic Mode ──", new Rect(16, y, 508, 44));
        y += 52;
        foreach (HapticMode mode in System.Enum.GetValues(typeof(HapticMode)))
        {
            var captured = mode;
            var idx = (int)mode;
            var btn = MakeButton(_panel.transform, _modeLabels[idx], new Rect(16, y, 508, 60));
            btn.onClick.AddListener(() =>
            {
                _currentMode = captured;
                ApplyModeToAll();
                RefreshModeButtons();
            });
            _modeButtons.Add(btn);
            y += 68;
        }

        y += 8;
        _targetCountLabel = MakeLabel(_panel.transform, "Targets: 0",
            new Rect(16, y, 508, 44));
        y += 52;

        // ── Hand offset section ──────────────────────────────────────
        MakeLabel(_panel.transform, "── Hand Offset (X axis) ──",
            new Rect(16, y, 508, 44));
        y += 52;

        // Left hand X
        MakeLabel(_panel.transform, "Left hand X offset (metres)",
            new Rect(16, y, 508, 40));
        y += 44;
        _leftOffsetField = MakeInputField(_panel.transform, "0.00",
            new Rect(16, y, 370, 60));
        _leftOffsetField.onEndEdit.AddListener(val =>
        {
            if (float.TryParse(val,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float result))
            {
                _leftOffsetX = result;
                HandOffsetController.Instance?.SetLeftOffsetX(_leftOffsetX);
            }
            else
            {
                _leftOffsetField.text = _leftOffsetX.ToString("F2",
                    System.Globalization.CultureInfo.InvariantCulture);
            }
        });
        var leftResetBtn = MakeButton(_panel.transform, "Reset",
            new Rect(394, y, 130, 60));
        leftResetBtn.onClick.AddListener(() =>
        {
            _leftOffsetX = 0f;
            _leftOffsetField.text = "0.00";
            HandOffsetController.Instance?.SetLeftOffsetX(0f);
        });
        y += 72;

        // Right hand X
        MakeLabel(_panel.transform, "Right hand X offset (metres)",
            new Rect(16, y, 508, 40));
        y += 44;
        _rightOffsetField = MakeInputField(_panel.transform, "0.00",
            new Rect(16, y, 370, 60));
        _rightOffsetField.onEndEdit.AddListener(val =>
        {
            if (float.TryParse(val,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float result))
            {
                _rightOffsetX = result;
                HandOffsetController.Instance?.SetRightOffsetX(_rightOffsetX);
            }
            else
            {
                _rightOffsetField.text = _rightOffsetX.ToString("F2",
                    System.Globalization.CultureInfo.InvariantCulture);
            }
        });
        var rightResetBtn = MakeButton(_panel.transform, "Reset",
            new Rect(394, y, 130, 60));
        rightResetBtn.onClick.AddListener(() =>
        {
            _rightOffsetX = 0f;
            _rightOffsetField.text = "0.00";
            HandOffsetController.Instance?.SetRightOffsetX(0f);
        });
        y += 72;

        _handStatusLabel = MakeLabel(_panel.transform, "Hands: scanning...",
            new Rect(16, y, 508, 44));

        RefreshModeButtons();
    }

    void RefreshModeButtons()
    {
        for (int i = 0; i < _modeButtons.Count; i++)
        {
            var c = _modeButtons[i].colors;
            c.normalColor = i == (int)_currentMode
                ? new Color(0.2f, 0.8f, 1f)
                : new Color(0.25f, 0.25f, 0.25f);
            c.highlightedColor = i == (int)_currentMode
                ? new Color(0.3f, 0.9f, 1f)
                : new Color(0.35f, 0.35f, 0.35f);
            _modeButtons[i].colors = c;
        }
    }

    void UpdateTargetCountLabel()
    {
        if (_targetCountLabel != null)
            _targetCountLabel.text = $"Targets: {_targets.Count}";
    }

    void UpdateOffsetLabels()
    {
        if (_leftOffsetField != null)
            _leftOffsetField.text = _leftOffsetX.ToString("F2",
                System.Globalization.CultureInfo.InvariantCulture);
        if (_rightOffsetField != null)
            _rightOffsetField.text = _rightOffsetX.ToString("F2",
                System.Globalization.CultureInfo.InvariantCulture);
        if (_handStatusLabel != null && HandOffsetController.Instance != null)
            _handStatusLabel.text = HandOffsetController.Instance.StatusText();
    }

    // ── UI helpers ───────────────────────────────────────────────────────────

    GameObject MakePanel(Transform parent, Rect r)
    {
        var go = new GameObject("Panel");
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.92f);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(r.x, -r.y);
        rt.sizeDelta = new Vector2(r.width, r.height);
        return go;
    }

    Button MakeButton(Transform parent, string label, Rect r)
    {
        var go = new GameObject($"Btn_{label}");
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.25f);
        var btn = go.AddComponent<Button>();
        var c = btn.colors;
        c.highlightedColor = new Color(0.4f, 0.4f, 0.4f);
        c.pressedColor = new Color(0.15f, 0.15f, 0.15f);
        btn.colors = c;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(r.x, -r.y);
        rt.sizeDelta = new Vector2(r.width, r.height);
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        var txt = textGO.AddComponent<Text>();
        txt.text = label;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 28;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        var trt = txt.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        return btn;
    }

    InputField MakeInputField(Transform parent, string placeholder, Rect r)
    {
        var go = new GameObject("InputField");
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f);
        var field = go.AddComponent<InputField>();
        field.contentType = InputField.ContentType.DecimalNumber;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(r.x, -r.y);
        rt.sizeDelta = new Vector2(r.width, r.height);

        var phGO = new GameObject("Placeholder");
        phGO.transform.SetParent(go.transform, false);
        var ph = phGO.AddComponent<Text>();
        ph.text = placeholder;
        ph.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        ph.fontSize = 26;
        ph.color = new Color(0.5f, 0.5f, 0.5f);
        ph.alignment = TextAnchor.MiddleLeft;
        var phrt = ph.GetComponent<RectTransform>();
        phrt.anchorMin = Vector2.zero;
        phrt.anchorMax = Vector2.one;
        phrt.offsetMin = new Vector2(10, 0);
        phrt.offsetMax = Vector2.zero;

        var txtGO = new GameObject("Text");
        txtGO.transform.SetParent(go.transform, false);
        var txt = txtGO.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 26;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleLeft;
        var trt = txt.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(10, 0);
        trt.offsetMax = Vector2.zero;

        field.textComponent = txt;
        field.placeholder = ph;
        field.text = placeholder;
        return field;
    }

    Text MakeLabel(Transform parent, string content, Rect r)
    {
        var go = new GameObject($"Label_{content}");
        go.transform.SetParent(parent, false);
        var txt = go.AddComponent<Text>();
        txt.text = content;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 26;
        txt.color = new Color(0.7f, 0.7f, 0.7f);
        txt.alignment = TextAnchor.MiddleCenter;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(r.x, -r.y);
        rt.sizeDelta = new Vector2(r.width, r.height);
        return txt;
    }
}