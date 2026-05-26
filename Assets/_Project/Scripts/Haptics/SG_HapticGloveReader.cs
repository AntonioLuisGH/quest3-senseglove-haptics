using UnityEngine;

namespace SG
{
    /// <summary>
    /// Thin subclass of SG_HapticGlove that intercepts QueueFFBCmd so the
    /// brake levels actually sent to the hardware are readable each frame.
    /// Swap this component for SG_HapticGlove on your hand prefabs.
    /// </summary>
    public class SG_HapticGloveReader : SG_HapticGlove
    {
        /// <summary>
        /// The last FFB levels sent to the hardware, one per finger (thumb→pinky), 0–1.
        /// Updated every frame by SG_HandFeedback → UpdateForces → QueueFFBCmd.
        /// </summary>
        public float[] LastFFBLevels { get; private set; } = new float[5];

        public override void QueueFFBCmd(float[] values01)
        {
            // Cache before forwarding — this IS the brake signal
            if (values01 != null && values01.Length == 5)
                System.Array.Copy(values01, LastFFBLevels, 5);

            base.QueueFFBCmd(values01);   // still sends to hardware normally
        }

        // Single-finger overload — keep in sync
        public override void QueueFFBCmd(SGCore.Finger finger, float value01)
        {
            int f = (int)finger;
            if (f >= 0 && f < LastFFBLevels.Length)
                LastFFBLevels[f] = value01;

            base.QueueFFBCmd(finger, value01);
        }
    }
}