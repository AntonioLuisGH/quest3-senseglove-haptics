using UnityEngine;
using SG;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UI;

[System.Serializable]
public class JarScorePanel
{
    public TextMeshProUGUI jarNameText;
    public TextMeshProUGUI percentText;   // big number, e.g. "63%"
    public TextMeshProUGUI feedbackText;  // e.g. "Slightly over — stop sooner"
    public TextMeshProUGUI scoreText;     // e.g. "82 / 100"
    public Image fillBarImage;  // UI Image, Fill Method = Horizontal
}

[System.Serializable]
public class JarData
{
    public string jarName = "Jar";
    public Transform liquidPivot;
    public Collider pouringZone;
    public Transform jarBottom;
    public Transform jarTop;

    [Range(0f, 1f)] public float targetFillLevel = 0.6f;
    [Range(0f, 0.25f)] public float tolerance = 0.05f;
    public float fillSpeed = 0.5f;
    public float maxLiquidScale = 1.0f;

    public Transform targetLevelMarker;
    public Transform upperToleranceMarker;
    public Transform lowerToleranceMarker;

    public JarScorePanel scorePanel;

    [Header("Scientific Setup")]
    public float maxVolumeML = 500f; // e.g., a 500mL beaker

    [HideInInspector] public float currentFillNormalised = 0f;
    [HideInInspector] public bool isJarFull = false;
}

public class SpongeInteraction : MonoBehaviour
{
    [Header("SenseGlove – Tracked Hands")]
    public SG_TrackedHand leftHand;
    public SG_TrackedHand rightHand;
    public SG_Grabable grabable;

    [Header("SenseGlove – Glove Readers")]
    public SG_HapticGlove leftGlove;
    public SG_HapticGlove rightGlove;

    [Header("Which fingers count toward squeeze (thumb → pinky)")]
    public bool useThumb = true;
    public bool useIndex = true;
    public bool useMiddle = true;
    public bool useRing = false;
    public bool usePinky = false;

    [Header("Visuals & UI")]
    public ParticleSystem spongeParticles;
    public TextMeshProUGUI forceText;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI introText;

    [Header("Haptic Feedback")]
    public bool enableHaptics = true;

    [Header("Jars")]
    public List<JarData> jars = new List<JarData>();

    [Header("Audio")]
    public AudioSource waterAudio;
    [Range(0f, 1f)] public float maxVolume = 1.0f;

    [Header("Flexion Fallback")]
    [Range(0f, 0.5f)] public float restThreshold = 0.2f;

    [Header("Debug")]
    public bool enableDebugSqueeze = false;
    [Range(0f, 1f)] public float debugSqueezeValue = 0f;

    // ── private ───────────────────────────────────────────────────────────────
    private bool wasGrabbed = false;
    private SG_TrackedHand activeHand = null;
    private SG_HapticGlove activeReader = null;
    private float currentTime = 0f;
    private int activeJarIndex = -1;

    void Start()
    {
        if (grabable == null) grabable = GetComponent<SG_Grabable>();
        ResetSqueezeEffects();

        foreach (var jar in jars)
        {
            if (jar.pouringZone != null) jar.pouringZone.isTrigger = true;
            PositionLevelMarkers(jar);
            RefreshPanel(jar);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        for (int i = 0; i < jars.Count; i++)
            if (jars[i].pouringZone != null && other == jars[i].pouringZone)
            { activeJarIndex = i; return; }
    }

    void OnTriggerExit(Collider other)
    {
        if (activeJarIndex >= 0 &&
            jars[activeJarIndex].pouringZone != null &&
            other == jars[activeJarIndex].pouringZone)
            activeJarIndex = -1;
    }

    void Update()
    {
        bool allFull = jars.Count > 0 && jars.TrueForAll(j => j.isJarFull);
        if (!allFull)
        {
            currentTime += Time.deltaTime;
            if (timerText != null) timerText.text = "Time: " + currentTime.ToString("F2");
        }

        if (enableDebugSqueeze)
        { ProcessSqueezeEffects(debugSqueezeValue, false); return; }

        if (grabable == null) return;

        bool isGrabbedNow = grabable.IsGrabbed();

        if (isGrabbedNow && !wasGrabbed)
        { activeHand = GetActiveGrabbingHand(); activeReader = GetReader(activeHand); }

        if (!isGrabbedNow && wasGrabbed)
        { activeHand = null; activeReader = null; ResetSqueezeEffects(); }

        wasGrabbed = isGrabbedNow;

        if (!isGrabbedNow || activeHand == null) return;

        ProcessSqueezeEffects(GetBrakeSqueeze(), true);
    }

    // ── Core squeeze → fill ───────────────────────────────────────────────────

    void ProcessSqueezeEffects(float squeeze, bool sendHaptics)
    {
        if (forceText != null)
            forceText.text = $"Force: {Mathf.RoundToInt(squeeze * 100)}%";

        if (spongeParticles != null)
        {
            var em = spongeParticles.emission;
            em.rateOverTime = squeeze * 50f;
            if (squeeze > 0f && !spongeParticles.isPlaying) spongeParticles.Play();
            else if (squeeze == 0f && spongeParticles.isPlaying) spongeParticles.Stop();
        }

        if (waterAudio != null)
        {
            if (squeeze > 0f) { if (!waterAudio.isPlaying) waterAudio.Play(); waterAudio.volume = squeeze * maxVolume; }
            else if (waterAudio.isPlaying) waterAudio.Stop();
        }

        if (sendHaptics && enableHaptics && squeeze > 0.05f && grabable != null)
            grabable.SendVibrationCmd(VibrationLocation.WholeHand, squeeze, 0.1f, 170f);

        if (activeJarIndex >= 0)
        {
            JarData jar = jars[activeJarIndex];
            if (jar.liquidPivot != null && squeeze > 0f && !jar.isJarFull)
            {
                Vector3 s = jar.liquidPivot.localScale;
                s.y = Mathf.Min(s.y + squeeze * jar.fillSpeed * Time.deltaTime, jar.maxLiquidScale);
                jar.liquidPivot.localScale = s;
                jar.currentFillNormalised = s.y / jar.maxLiquidScale;

                if (s.y >= jar.maxLiquidScale)
                {
                    jar.isJarFull = true;
                    if (timerText != null) timerText.color = Color.green;
                    if (introText != null) introText.color = Color.green;
                }

                RefreshPanel(jar);
            }
        }
    }

    // ── Panel update — the only UI method you need ────────────────────────────

    void RefreshPanel(JarData jar)
    {
        var p = jar.scorePanel;
        if (p == null) return;

        float fill = jar.currentFillNormalised;
        float target = jar.targetFillLevel;
        float tol = jar.tolerance;
        float dev = fill - target;
        float absDev = Mathf.Abs(dev);

        // ── NEW: Scientific Percentage Error ──────────────────────────────────
        // Formula: (|Actual - Target| / Target) * 100
        // We check target > 0f to prevent dividing by zero if the target is set to 0.
        float percentageError = target > 0f ? (absDev / target) * 100f : 0f;

        // Feedback string and colors (PRESERVED ORIGINAL LOGIC)
        string feedback;
        Color col;
        if (fill < 0.01f)
        { feedback = "Start filling!"; col = Color.white; }
        else if (absDev <= tol * 0.2f)
        { feedback = "Perfect — right on target!"; col = Color.green; }
        else if (dev < 0)
        {
            if (absDev > tol * 3f) { feedback = "Keep going — far from target."; col = Color.red; }
            else if (absDev > tol) { feedback = "Getting closer — more water."; col = new Color(1f, 0.6f, 0f); }
            else { feedback = "Almost there — a little more!"; col = new Color(0.6f, 1f, 0.2f); }
        }
        else
        {
            if (absDev > tol * 3f) { feedback = "Way too much — past the target."; col = Color.red; }
            else if (absDev > tol) { feedback = "Too much — went past the line."; col = new Color(1f, 0.6f, 0f); }
            else { feedback = "Slightly over — stop sooner."; col = new Color(0.6f, 1f, 0.2f); }
        }

        if (jar.isJarFull && fill > target + tol)
        { feedback = "Overfilled!"; col = Color.red; }

        // UI Updates
        if (p.jarNameText != null) p.jarNameText.text = jar.jarName;

        // (PRESERVED) Big percentage number 
        if (p.percentText != null) { p.percentText.text = $"{Mathf.RoundToInt(fill * 100)}%"; p.percentText.color = col; }

        // (PRESERVED) Helpful text string
        if (p.feedbackText != null) { p.feedbackText.text = feedback; p.feedbackText.color = col; }

        // (REPLACED) Now displays scientific percentage error (e.g., "Error: 4.50%")
        if (p.scoreText != null) { p.scoreText.text = $"Error: {percentageError.ToString("F2")}%"; p.scoreText.color = col; }

        if (p.fillBarImage != null) p.fillBarImage.fillAmount = fill;
    }

    void ResetSqueezeEffects()
    {
        if (forceText != null) forceText.text = "Force: 0%";
        if (spongeParticles != null && spongeParticles.isPlaying) spongeParticles.Stop();
        if (waterAudio != null && waterAudio.isPlaying) waterAudio.Stop();
    }

    // ── Hand / glove helpers (unchanged) ─────────────────────────────────────

    SG_HapticGlove GetReader(SG_TrackedHand hand)
    {
        if (hand == null) return null;
        if (hand == leftHand) return leftGlove != null ? leftGlove : hand.GetComponent<SG_HapticGlove>();
        if (hand == rightHand) return rightGlove != null ? rightGlove : hand.GetComponent<SG_HapticGlove>();
        return null;
    }

    SG_TrackedHand GetActiveGrabbingHand()
    {
        SG_GrabScript lGS = leftHand != null ? leftHand.GetComponent<SG_GrabScript>() : null;
        SG_GrabScript rGS = rightHand != null ? rightHand.GetComponent<SG_GrabScript>() : null;
        if (lGS != null && grabable.GrabbedBy(lGS)) return leftHand;
        if (rGS != null && grabable.GrabbedBy(rGS)) return rightHand;
        if (lGS != null && lGS.IsGrabbing) return leftHand;
        if (rGS != null && rGS.IsGrabbing) return rightHand;
        float ld = leftHand != null ? Vector3.Distance(leftHand.transform.position, transform.position) : float.MaxValue;
        float rd = rightHand != null ? Vector3.Distance(rightHand.transform.position, transform.position) : float.MaxValue;
        if (ld == float.MaxValue && rd == float.MaxValue) return null;
        return ld < rd ? leftHand : rightHand;
    }

    float GetBrakeSqueeze()
    {
        if (activeReader == null) return 0f;
        bool[] use = { useThumb, useIndex, useMiddle, useRing, usePinky };

        if (activeReader.ForceFeedbackActive)
        {
            float[] levels = activeReader.LastFFBLevels;
            float total = 0f; int count = 0;
            for (int f = 0; f < 5; f++) { if (!use[f]) continue; total += levels[f]; count++; }
            return count > 0 ? total / count : 0f;
        }

        float[] flexion;
        if (!activeReader.GetNormalizedFlexion(out flexion)) return 0f;
        float fTotal = 0f; int fCount = 0;
        for (int f = 0; f < 5; f++)
        {
            if (!use[f]) continue;
            fTotal += Mathf.Clamp01((flexion[f] - restThreshold) / (1f - restThreshold));
            fCount++;
        }
        return fCount > 0 ? fTotal / fCount : 0f;
    }

    // ── Markers ───────────────────────────────────────────────────────────────

    void PositionLevelMarkers(JarData jar)
    {
        if (jar.jarBottom == null || jar.jarTop == null) return;
        float bY = jar.jarBottom.position.y, tY = jar.jarTop.position.y;
        SetMarkerHeight(jar.targetLevelMarker, Mathf.Lerp(bY, tY, jar.targetFillLevel));
        SetMarkerHeight(jar.upperToleranceMarker, Mathf.Lerp(bY, tY, Mathf.Min(jar.targetFillLevel + jar.tolerance, 1f)));
        SetMarkerHeight(jar.lowerToleranceMarker, Mathf.Lerp(bY, tY, Mathf.Max(jar.targetFillLevel - jar.tolerance, 0f)));
    }

    void SetMarkerHeight(Transform t, float worldY)
    {
        if (t == null) return;
        Vector3 p = t.position; p.y = worldY; t.position = p;
    }

    void OnValidate()
    {
        foreach (var jar in jars)
            if (jar.jarBottom != null && jar.jarTop != null) PositionLevelMarkers(jar);
    }
}