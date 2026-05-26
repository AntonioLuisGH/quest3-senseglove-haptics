using UnityEngine;
using SG;
using SGCore.Haptics;

public class EggHaptics : MonoBehaviour, IHapticTarget
{
    [Header("Waveforms")]
    public SG_CustomWaveform grabWaveform;
    public SG_CustomWaveform breakWaveform;

    [Header("Grab Vibration Repeat Interval (seconds)")]
    public float grabRepeatInterval = 0.2f;

    [Header("Dynamic Vibration Range")]
    [Tooltip("Amplitude when barely squeezing")]
    [Range(0f, 1f)] public float minAmplitude = 0.1f;
    [Tooltip("Amplitude when fully squeezing")]
    [Range(0f, 1f)] public float maxAmplitude = 1.0f;
    [Tooltip("Frequency (Hz) when barely squeezing")]
    public float minFrequency = 50f;
    [Tooltip("Frequency (Hz) when fully squeezing")]
    public float maxFrequency = 200f;

    [Header("Force Feedback Layers (for IHapticTarget)")]
    public GameObject objectFeedbackLayerLeft;
    public GameObject objectFeedbackLayerRight;

    [Header("Glove Readers (same SG_HapticGlove refs as sponge scene)")]
    public SG_HapticGlove leftGlove;
    public SG_HapticGlove rightGlove;

    public event System.Action OnEggBroken;

    // ── private ───────────────────────────────────────────────────────────────
    private SG_Grabable grabable;
    private SG_Breakable breakable;
    private System.Collections.Generic.List<SG_TrackedHand> holdingHands
        = new System.Collections.Generic.List<SG_TrackedHand>();
    private bool hasBroken = false;
    private bool isGrabbed = false;
    private float grabTimer = 0f;
    public bool vibrationEnabled = true;

    private SG_CustomWaveform stopWaveform;
    private SG_CustomWaveform runtimeWaveform; // reused every frame, values overwritten

    void Awake()
    {
        grabable = GetComponentInChildren<SG_Grabable>();
        breakable = GetComponent<SG_Breakable>();

        stopWaveform = ScriptableObject.CreateInstance<SG_CustomWaveform>();
        stopWaveform.amplitude = 0f;
        stopWaveform.attackTime = 0f;
        stopWaveform.sustainTime = 0.01f;
        stopWaveform.decayTime = 0f;

        // Runtime waveform — values get overwritten each pulse
        runtimeWaveform = ScriptableObject.CreateInstance<SG_CustomWaveform>();
    }

    void OnEnable()
    {
        grabable.ObjectGrabbed.AddListener(OnGrabbed);
        grabable.ObjectReleased.AddListener(OnReleased);
    }

    void OnDisable()
    {
        grabable.ObjectGrabbed.RemoveListener(OnGrabbed);
        grabable.ObjectReleased.RemoveListener(OnReleased);
    }

    // ── Grab / release ────────────────────────────────────────────────────────

    void OnGrabbed(SG_Interactable interactable, SG_GrabScript grabScript)
    {
        SG_TrackedHand hand = grabScript.TrackedHand;
        if (hand != null && !holdingHands.Contains(hand))
            holdingHands.Add(hand);
        isGrabbed = true;
        grabTimer = 0f;
    }

    void OnReleased(SG_Interactable interactable, SG_GrabScript grabScript)
    {
        SG_TrackedHand hand = grabScript.TrackedHand;
        if (hand != null)
        {
            hand.SendCustomWaveform(stopWaveform, VibrationLocation.Index_Tip);
            hand.SendCustomWaveform(stopWaveform, VibrationLocation.Thumb_Tip);
            holdingHands.Remove(hand);
        }
        if (holdingHands.Count == 0)
            isGrabbed = false;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (isGrabbed && !hasBroken && grabWaveform != null && vibrationEnabled)
        {
            grabTimer -= Time.deltaTime;
            if (grabTimer <= 0f)
            {
                float squeeze = GetSqueeze();

                // Always copy the designer waveform as the base
                runtimeWaveform.attackTime = grabWaveform.attackTime;
                runtimeWaveform.sustainTime = grabWaveform.sustainTime;
                runtimeWaveform.decayTime = grabWaveform.decayTime;

                if (squeeze > 0.05f)
                {
                    // We have a real squeeze reading — scale dynamically
                    runtimeWaveform.amplitude = Mathf.Lerp(minAmplitude, maxAmplitude, squeeze);
                    runtimeWaveform.startFrequency = (int)Mathf.Lerp(minFrequency, maxFrequency, squeeze);
                    runtimeWaveform.endFrequency = runtimeWaveform.startFrequency;
                    grabTimer = Mathf.Lerp(grabRepeatInterval, 0.05f, squeeze);
                }
                else
                {
                    // No squeeze reading — fall back to the original static waveform values
                    runtimeWaveform.amplitude = grabWaveform.amplitude;
                    runtimeWaveform.startFrequency = grabWaveform.startFrequency;
                    runtimeWaveform.endFrequency = grabWaveform.endFrequency;
                    grabTimer = grabRepeatInterval;
                }

                foreach (var hand in holdingHands)
                {
                    hand.SendCustomWaveform(runtimeWaveform, VibrationLocation.Index_Tip);
                    hand.SendCustomWaveform(runtimeWaveform, VibrationLocation.Thumb_Tip);
                }
            }
        }

        if (!hasBroken && breakable != null && breakable.IsBroken())
        {
            hasBroken = true;
            OnEggBroken?.Invoke();
            SendBreakVibration();
        }
    }

    // ── Squeeze reading (same pattern as SpongeInteraction) ───────────────────

    float GetSqueeze()
    {
        foreach (var hand in holdingHands)
        {
            SG_HapticGlove glove = null;
            if (leftGlove != null && hand == leftGlove.GetComponentInParent<SG_TrackedHand>())
                glove = leftGlove;
            else if (rightGlove != null && hand == rightGlove.GetComponentInParent<SG_TrackedHand>())
                glove = rightGlove;
            else
                glove = hand.GetComponent<SG_HapticGlove>()
                     ?? hand.GetComponentInChildren<SG_HapticGlove>();

            if (glove == null) continue;

            // Try brake levels first
            float[] levels = glove.LastFFBLevels;
            float brakeAvg = (levels[0] + levels[1]) * 0.5f;
            if (brakeAvg > 0.05f) return brakeAvg;

            // Fall back to flexion if brake levels are flat
            float[] flexion;
            if (glove.GetNormalizedFlexion(out flexion))
            {
                float flex = (flexion[0] + flexion[1]) * 0.5f;
                return Mathf.Clamp01((flex - 0.2f) / 0.8f); // remap: 0.2 open → 1.0 closed
            }
        }
        return 0f;
    }

    // ── Break ─────────────────────────────────────────────────────────────────

    void SendBreakVibration()
    {
        if (breakWaveform == null) return;
        if (holdingHands.Count > 0)
        {
            foreach (var hand in holdingHands)
                hand.SendCustomWaveform(breakWaveform, VibrationLocation.WholeHand);
        }
        else
        {
            foreach (var hand in FindObjectsByType<SG_TrackedHand>(FindObjectsSortMode.None))
                hand.SendCustomWaveform(breakWaveform, VibrationLocation.WholeHand);
        }
    }

    // ── IHapticTarget ─────────────────────────────────────────────────────────

    void Start()
    {
        EggCounter counter = FindFirstObjectByType<EggCounter>();
        if (counter != null) counter.RegisterEgg(this);

        TestingManager.Instance?.RegisterTarget(this);
    }
    void OnDestroy()
    {
        EggCounter counter = FindFirstObjectByType<EggCounter>();
        if (counter != null) counter.UnregisterEgg(this);

        TestingManager.Instance?.UnregisterTarget(this);
    }

    public void ApplyHapticMode(HapticMode mode)
    {
        bool forceOn = mode == HapticMode.FullFeedback || mode == HapticMode.OnlyForce;
        bool vibrationOn = mode == HapticMode.FullFeedback || mode == HapticMode.OnlyVibration;

        if (objectFeedbackLayerLeft != null) objectFeedbackLayerLeft.SetActive(forceOn);
        if (objectFeedbackLayerRight != null) objectFeedbackLayerRight.SetActive(forceOn);

        if (leftGlove != null) leftGlove.ForceFeedbackActive = forceOn;
        if (rightGlove != null) rightGlove.ForceFeedbackActive = forceOn;

        vibrationEnabled = vibrationOn;
    }
}