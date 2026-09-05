using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EmergencyVR - Core phase controller. Singleton, event-driven so other systems
/// (hazards, dialogue, dashboard) subscribe without needing direct references.
///
/// Flow: Tutorial (timed, spawns pre-emergency hints) -> Emergency (starts main timer,
/// fires OnEmergencyStarted) -> Complete (fired externally, e.g. by EvaluationDashboard
/// when player reaches the exit).
/// </summary>
public class EmergencyFlowManager : MonoBehaviour
{
    public static EmergencyFlowManager Instance { get; private set; }

    public enum GamePhase { Tutorial, Emergency, Complete }
    public GamePhase CurrentPhase { get; private set; } = GamePhase.Tutorial;

    [Header("Tutorial Timing")]
    [Tooltip("How long the tutorial phase lasts before the emergency triggers, in seconds.")]
    [Range(60f, 120f)] public float tutorialDuration = 90f;

    [Serializable]
    public class HintEvent
    {
        public string label; // for your own reference in the Inspector, not used at runtime
        [Tooltip("Seconds into the tutorial phase when this hint activates.")]
        public float activateAtTime = 60f;
        public ParticleSystem smokeParticles;
        public AudioSource sparkingWireAudio; // assign an AudioSource with a looping spark/electrical clip already set
    }
    
    [Header("Pre-Emergency Hints")]
    [Tooltip("Ambient hints (small smoke wisps, sparking sounds) that hint at the coming emergency before it starts.")]
    public List<HintEvent> hints = new List<HintEvent>();

    /// <summary>Time.time value when the Emergency phase began. Used by EvaluationDashboard for elapsed time.</summary>
    public float EmergencyStartTime { get; private set; }

    // --- Static events - subscribe from any script, e.g. EmergencyFlowManager.OnEmergencyStarted += MyHandler; ---
    public static event Action OnTutorialStarted;
    public static event Action OnEmergencyStarted;
    public static event Action<float> OnGameComplete; // passes total elapsed emergency time in seconds

    private HashSet<HintEvent> firedHints = new HashSet<HintEvent>();
    private float tutorialTimer = 0f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        CurrentPhase = GamePhase.Tutorial;
        tutorialTimer = 0f;
        OnTutorialStarted?.Invoke();
    }

    void Update()
    {
        if (CurrentPhase != GamePhase.Tutorial) return;

        tutorialTimer += Time.deltaTime;

        // Check for any hints that should fire at this point in the tutorial
        foreach (var hint in hints)
        {
            if (!firedHints.Contains(hint) && tutorialTimer >= hint.activateAtTime)
            {
                FireHint(hint);
                firedHints.Add(hint);
            }
        }

        if (tutorialTimer >= tutorialDuration)
        {
            BeginEmergency();
        }
    }

    void FireHint(HintEvent hint)
    {
        if (hint.smokeParticles != null)
            hint.smokeParticles.Play();

        if (hint.sparkingWireAudio != null && !hint.sparkingWireAudio.isPlaying)
            hint.sparkingWireAudio.Play();
    }

    void BeginEmergency()
    {
        CurrentPhase = GamePhase.Emergency;
        EmergencyStartTime = Time.time;

        // Find all dynamic fire nodes placed across the scene and ignite them
        DynamicFireNode[] fireNodes = FindObjectsOfType<DynamicFireNode>();
        foreach (var node in fireNodes)
        {
            if (node != null)
            {
                node.Ignite();
            }
        }

        OnEmergencyStarted?.Invoke();
    }

    /// <summary>
    /// Call this externally (e.g. from EvaluationDashboard's exit trigger) when the
    /// player successfully completes the scenario.
    /// </summary>
    public void CompleteGame()
    {
        if (CurrentPhase == GamePhase.Complete) return; // prevent double-firing

        CurrentPhase = GamePhase.Complete;
        float elapsed = Time.time - EmergencyStartTime;
        OnGameComplete?.Invoke(elapsed);
    }

    /// <summary>Elapsed seconds since the emergency began, or 0 if not started yet. Useful for live HUD timers.</summary>
    public float GetElapsedEmergencyTime()
    {
        if (CurrentPhase == GamePhase.Tutorial) return 0f;
        return Time.time - EmergencyStartTime;
    }
}