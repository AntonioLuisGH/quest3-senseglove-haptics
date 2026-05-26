using UnityEngine;

public class SpongeHapticTarget : MonoBehaviour, IHapticTarget
{
    [Header("Force Feedback Layers")]
    [Tooltip("The 'object feedback layer' child GameObject on the LEFT glove")]
    public GameObject objectFeedbackLayerLeft;
    [Tooltip("The 'object feedback layer' child GameObject on the RIGHT glove")]
    public GameObject objectFeedbackLayerRight;

    [Header("Vibration — SpongeInteraction script")]
    [Tooltip("Drag the SpongeInteraction component from Sponge_Flex here")]
    public SpongeInteraction spongeInteraction;

    void Start()
    {
        TestingManager.Instance?.RegisterTarget(this);
    }

    void OnDestroy()
    {
        TestingManager.Instance?.UnregisterTarget(this);
    }

    public void ApplyHapticMode(HapticMode mode)
    {
        bool forceOn = mode == HapticMode.FullFeedback || mode == HapticMode.OnlyForce;
        bool vibrationOn = mode == HapticMode.FullFeedback || mode == HapticMode.OnlyVibration;

        if (objectFeedbackLayerLeft != null) objectFeedbackLayerLeft.SetActive(forceOn);
        if (objectFeedbackLayerRight != null) objectFeedbackLayerRight.SetActive(forceOn);

        // Tell the glove readers which measurement mode to use
        if (spongeInteraction != null)
        {
            if (spongeInteraction.leftGlove != null) spongeInteraction.leftGlove.ForceFeedbackActive = forceOn;
            if (spongeInteraction.rightGlove != null) spongeInteraction.rightGlove.ForceFeedbackActive = forceOn;

            spongeInteraction.enableHaptics = vibrationOn;
        }
    }
}