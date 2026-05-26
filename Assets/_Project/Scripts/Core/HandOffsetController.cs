using UnityEngine;
using UnityEngine.SceneManagement;
using SG;

public class HandOffsetController : MonoBehaviour
{
    public static HandOffsetController Instance { get; private set; }

    private float _leftOffsetX = 0f;
    private float _rightOffsetX = 0f;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void FindHands()
    {
        Debug.Log("[HandOffsetController] World-space offset ready");
        ApplyOffsets();
    }

    public void SetLeftOffsetX(float x)
    {
        _leftOffsetX = x;
        ApplyOffsets();
    }

    public void SetRightOffsetX(float x)
    {
        _rightOffsetX = x;
        ApplyOffsets();
    }

    public float GetLeftOffsetX() => _leftOffsetX;
    public float GetRightOffsetX() => _rightOffsetX;

    void ApplyOffsets()
    {
        SG_TrackedHand.WorldDriftOffset_L = new Vector3(_leftOffsetX, 0f, 0f);
        SG_TrackedHand.WorldDriftOffset_R = new Vector3(_rightOffsetX, 0f, 0f);
        Debug.Log($"[HandOffsetController] L={_leftOffsetX:F2}  R={_rightOffsetX:F2}");
    }

    public string StatusText() => $"L: {_leftOffsetX:F2}m   R: {_rightOffsetX:F2}m";
}