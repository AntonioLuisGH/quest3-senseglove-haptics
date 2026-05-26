using UnityEngine;
using System.Collections.Generic;

public class FamiliarizationHapticTarget : MonoBehaviour, IHapticTarget
{
    [Header("Force Feedback Layers")]
    public GameObject objectFeedbackLayerLeft;
    public GameObject objectFeedbackLayerRight;

    [Header("Glove Readers")]
    public SG.SG_HapticGlove leftGlove;
    public SG.SG_HapticGlove rightGlove;

    [Header("All EggHaptics scripts in this scene")]
    public List<EggHaptics> objects = new List<EggHaptics>();

    void Start() { TestingManager.Instance?.RegisterTarget(this); }
    void OnDestroy() { TestingManager.Instance?.UnregisterTarget(this); }

    public void ApplyHapticMode(HapticMode mode)
    {
        bool forceOn = mode == HapticMode.FullFeedback || mode == HapticMode.OnlyForce;
        bool vibrationOn = mode == HapticMode.FullFeedback || mode == HapticMode.OnlyVibration;

        if (objectFeedbackLayerLeft != null) objectFeedbackLayerLeft.SetActive(forceOn);
        if (objectFeedbackLayerRight != null) objectFeedbackLayerRight.SetActive(forceOn);

        if (leftGlove != null) leftGlove.ForceFeedbackActive = forceOn;
        if (rightGlove != null) rightGlove.ForceFeedbackActive = forceOn;

        foreach (var obj in objects)
            if (obj != null) obj.vibrationEnabled = vibrationOn;
    }
}