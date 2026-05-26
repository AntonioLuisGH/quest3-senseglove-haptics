using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class EggCounter : MonoBehaviour
{
    [Header("Game Settings")]
    public int eggsToWin = 3;

    [Header("UI Elements")]
    public TextMeshProUGUI nestText;
    public TextMeshProUGUI brokenText;
    public TextMeshProUGUI introText;
    public TextMeshProUGUI timerText;

    [Header("Audio & Visuals")]
    public AudioSource successSound;
    public ParticleSystem successParticles;

    private int eggsInNest = 0;
    private int eggsBroken = 0;
    private bool hasWon = false;
    private float currentTime = 0f;
    private List<EggHaptics> eggs = new List<EggHaptics>();

    // ── Auto-registration from EggHaptics ────────────────────────────────────

    public void RegisterEgg(EggHaptics egg)
    {
        if (!eggs.Contains(egg))
        {
            eggs.Add(egg);
            egg.OnEggBroken += OnEggBrokenHandler;
        }
    }

    public void UnregisterEgg(EggHaptics egg)
    {
        if (eggs.Contains(egg))
        {
            egg.OnEggBroken -= OnEggBrokenHandler;
            eggs.Remove(egg);
        }
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        UpdateNestDisplay();
        UpdateBrokenDisplay();
    }

    private void OnDestroy()
    {
        foreach (var egg in eggs)
            if (egg != null) egg.OnEggBroken -= OnEggBrokenHandler;
    }

    private void Update()
    {
        if (!hasWon)
        {
            currentTime += Time.deltaTime;
            if (timerText != null)
                timerText.text = "Time: " + currentTime.ToString("F2");
        }
    }

    // ── Nest trigger (place / remove eggs) ───────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (hasWon) return;
        if (other.CompareTag("Egg"))
        {
            eggsInNest++;
            UpdateNestDisplay();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (hasWon) return;
        if (other.CompareTag("Egg"))
        {
            eggsInNest = Mathf.Max(0, eggsInNest - 1);
            UpdateNestDisplay();
        }
    }

    // ── Broken event ──────────────────────────────────────────────────────────

    private void OnEggBrokenHandler()
    {
        eggsBroken++;
        UpdateBrokenDisplay();
    }

    // ── UI ────────────────────────────────────────────────────────────────────

    private void UpdateNestDisplay()
    {
        if (nestText == null) return;

        nestText.text = "Bowl: " + eggsInNest + " / " + eggsToWin;

        if (eggsInNest >= eggsToWin && !hasWon)
        {
            hasWon = true;
            nestText.color = Color.green;
            if (successSound != null) successSound.Play();
            if (successParticles != null) successParticles.Play();
            if (introText != null) { introText.text = "Completed!"; introText.color = Color.green; }
        }
        else if (!hasWon)
        {
            nestText.color = Color.blue;
        }
    }

    private void UpdateBrokenDisplay()
    {
        if (brokenText == null) return;
        brokenText.text = "Broken: " + eggsBroken;
        brokenText.color = eggsBroken > 0 ? Color.red : Color.white;
    }
}