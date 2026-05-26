using System.Collections.Generic;
using UnityEngine;

public class IndexPalmDistance : MonoBehaviour
{
    [SerializeField] private Transform indexTransform;
    [SerializeField] private Transform palmTransform;

    public float CurrentDistance { get; private set; }
    public float AverageDistance { get; private set; }
    public float WindowDuration => windowDuration;

    private Queue<(float time, float value)> samples = new Queue<(float, float)>();
    private float runningSum = 0f;

    [SerializeField] private float windowDuration = 3f;

    void Update()
    {
        if (indexTransform == null || palmTransform == null) return;

        float currentTime = Time.time;

        // X-axis distance
        float distanceX = Mathf.Abs(palmTransform.position.x - indexTransform.position.x);

        // Add sample
        samples.Enqueue((currentTime, distanceX));
        runningSum += distanceX;

        // Remove old samples
        while (samples.Count > 0 && currentTime - samples.Peek().time > windowDuration)
        {
            var oldSample = samples.Dequeue();
            runningSum -= oldSample.value;
        }

        // Compute average
        float average = samples.Count > 0 ? runningSum / samples.Count : 0f;

        CurrentDistance = distanceX;
        AverageDistance = average;
    }
}