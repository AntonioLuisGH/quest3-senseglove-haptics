using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Standalone GUI panel that displays distance data from IndexPalmDistance.
/// Place this on any GameObject and assign the source reference in the Inspector.
/// Does NOT modify IndexPalmDistance in any way beyond reading its public properties.
/// </summary>
public class IndexPalmDistanceGUI : MonoBehaviour
{
    [SerializeField] private IndexPalmDistance source;

    // ── Panel layout ──────────────────────────────────────────────────────────
    [Header("Panel Layout")]
    [SerializeField] private float panelWidth = 260f;
    [SerializeField] private float panelHeight = 220f;
    [SerializeField] private float marginRight = 20f;
    [SerializeField] private float marginTop = 20f;

    // ── Graph settings ────────────────────────────────────────────────────────
    [Header("Graph")]
    [SerializeField] private int graphSamples = 80;
    [SerializeField] private float graphMaxY = 0.15f; // expected max distance (m), tune to your range

    // ── Colours ───────────────────────────────────────────────────────────────
    private static readonly Color ColPanel = new Color(0.06f, 0.06f, 0.10f, 0.88f);
    private static readonly Color ColBorder = new Color(0.30f, 0.55f, 1.00f, 0.70f);
    private static readonly Color ColRaw = new Color(0.35f, 0.85f, 0.50f, 1.00f);
    private static readonly Color ColAvg = new Color(0.95f, 0.65f, 0.20f, 1.00f);
    private static readonly Color ColGrid = new Color(1f, 1f, 1f, 0.06f);

    // ── Internal ──────────────────────────────────────────────────────────────
    private Queue<float> _graphBuf = new Queue<float>();
    private Texture2D _solidTex;
    private GUIStyle _labelStyle;
    private GUIStyle _bigNumStyle;
    private GUIStyle _titleStyle;

    // ─────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        _solidTex = new Texture2D(1, 1);
        _solidTex.SetPixel(0, 0, Color.white);
        _solidTex.Apply();
    }

    void Update()
    {
        if (source == null) return;

        _graphBuf.Enqueue(source.CurrentDistance);
        if (_graphBuf.Count > graphSamples) _graphBuf.Dequeue();
    }

    // ─────────────────────────────────────────────────────────────────────────
    void OnGUI()
    {
        if (source == null) return;
        InitStyles();

        float px = Screen.width - panelWidth - marginRight;
        float py = marginTop;

        Rect panel = new Rect(px, py, panelWidth, panelHeight);

        DrawRect(panel, ColPanel);
        DrawBorder(panel, ColBorder, 1.5f);

        float pad = 12f;
        float inner = panelWidth - pad * 2f;
        float cy = py + pad;

        // Title
        GUI.color = Color.white;
        GUI.Label(new Rect(px + pad, cy, inner, 20f), "◈  INDEX → PALM  DISTANCE", _titleStyle);
        cy += 22f;

        DrawLine(new Rect(px + pad, cy, inner, 1f), ColBorder * 0.6f);
        cy += 6f;

        // Rows
        DrawMetricRow(px + pad, ref cy, inner, "Current",
            $"{source.CurrentDistance * 100f:F1} cm", ColRaw);

        DrawMetricRow(px + pad, ref cy, inner, $"Avg ({source.WindowDuration:F0}s)",
            $"{source.AverageDistance * 100f:F1} cm", ColAvg);

        cy += 4f;

        // Sparkline
        float graphH = panelHeight - (cy - py) - pad;
        if (graphH > 30f)
            DrawGraph(new Rect(px + pad, cy, inner, graphH));
    }

    // ─────────────────────────────────────────────────────────────────────────
    void DrawMetricRow(float x, ref float y, float w, string label, string value, Color valueColor)
    {
        float rowH = 32f;
        GUI.color = new Color(0.75f, 0.75f, 0.85f, 1f);
        GUI.Label(new Rect(x, y, w * 0.5f, rowH), label, _labelStyle);
        GUI.color = valueColor;
        GUI.Label(new Rect(x + w * 0.35f, y - 4f, w * 0.65f, rowH + 4f), value, _bigNumStyle);
        y += rowH;
    }

    void DrawGraph(Rect r)
    {
        DrawRect(r, new Color(0f, 0f, 0f, 0.35f));
        DrawBorder(r, ColBorder * 0.4f, 1f);

        // Grid lines
        for (int g = 0; g <= 2; g++)
        {
            float gy = r.y + r.height - (g / 2f) * r.height;
            DrawLine(new Rect(r.x + 1, gy, r.width - 2, 1), ColGrid);
        }

        // Average line
        float avgNorm = Mathf.Clamp01(source.AverageDistance / graphMaxY);
        float avgY = r.y + r.height - avgNorm * r.height;
        DrawLine(new Rect(r.x + 1, avgY, r.width - 2, 1), ColAvg * 0.8f);

        // Sparkline
        float[] buf = _graphBuf.ToArray();
        if (buf.Length < 2) return;

        float stepX = (r.width - 2f) / (graphSamples - 1);
        for (int i = 1; i < buf.Length; i++)
        {
            float x0 = r.x + 1 + (i - 1) * stepX;
            float x1 = r.x + 1 + i * stepX;
            float y0 = r.y + r.height - Mathf.Clamp01(buf[i - 1] / graphMaxY) * r.height;
            float y1 = r.y + r.height - Mathf.Clamp01(buf[i] / graphMaxY) * r.height;
            DrawThickLine(new Vector2(x0, y0), new Vector2(x1, y1), ColRaw, 1.5f);
        }

        // Scale labels
        GUI.color = new Color(0.6f, 0.6f, 0.7f, 0.8f);
        GUI.Label(new Rect(r.x + 2, r.y + 1, 50, 14), $"{graphMaxY * 100f:F0}cm", _labelStyle);
        GUI.Label(new Rect(r.x + 2, r.yMax - 14, 20, 14), "0", _labelStyle);
    }

    // ── Drawing helpers ───────────────────────────────────────────────────────
    void DrawRect(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, _solidTex); GUI.color = Color.white; }
    void DrawLine(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, _solidTex); GUI.color = Color.white; }

    void DrawBorder(Rect r, Color c, float t)
    {
        DrawLine(new Rect(r.x, r.y, r.width, t), c);
        DrawLine(new Rect(r.x, r.yMax - t, r.width, t), c);
        DrawLine(new Rect(r.x, r.y, t, r.height), c);
        DrawLine(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    void DrawThickLine(Vector2 a, Vector2 b, Color c, float thickness)
    {
        Vector2 d = b - a;
        float len = d.magnitude;
        if (len < 0.01f) return;
        Matrix4x4 backup = GUI.matrix;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a);
        DrawRect(new Rect(a.x, a.y - thickness * 0.5f, len, thickness), c);
        GUI.matrix = backup;
    }

    void InitStyles()
    {
        if (_labelStyle != null) return;
        _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleLeft };
        _bigNumStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
        _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
    }

    void OnDestroy() { if (_solidTex != null) Destroy(_solidTex); }
}