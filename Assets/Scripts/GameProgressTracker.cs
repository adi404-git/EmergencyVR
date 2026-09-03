using System;
using UnityEngine;

/// <summary>
/// EmergencyVR - Single source of truth for all scoreable objectives/progress.
/// Any gameplay script (NPCFollower, alarm button, phone system, elevator-misuse
/// penalty, etc.) writes here via the Mark___ methods. EvaluationDashboard (or any
/// future end-screen/analytics script) just reads Instance values - it never needs
/// to know which script accomplished what.
///
/// Add new objectives here as the game grows - this is the one place to extend.
/// </summary>
public class GameProgressTracker : MonoBehaviour
{
    public static GameProgressTracker Instance { get; private set; }

    // --- Objective state (read-only from outside, only this script's Mark methods write) ---
    public bool NPCRescued { get; private set; } = false;
    public bool FireAlarmActivated { get; private set; } = false;
    public bool HelplineCalled { get; private set; } = false;
    public bool UsedElevatorDuringEmergency { get; private set; } = false; // example penalty flag, ready for later
    public bool ExtinguisherUsedCorrectly { get; private set; } = false;   // ready for your extinguisher phase

    /// <summary>Fires whenever any objective changes, passing the name of the objective. UI/HUD scripts can subscribe to update live rather than polling.</summary>
    public static event Action<string> OnObjectiveChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void MarkNPCRescued()
    {
        if (NPCRescued) return;
        NPCRescued = true;
        OnObjectiveChanged?.Invoke(nameof(NPCRescued));
    }

    public void MarkFireAlarmActivated()
    {
        if (FireAlarmActivated) return;
        FireAlarmActivated = true;
        OnObjectiveChanged?.Invoke(nameof(FireAlarmActivated));
    }

    public void MarkHelplineCalled()
    {
        if (HelplineCalled) return;
        HelplineCalled = true;
        OnObjectiveChanged?.Invoke(nameof(HelplineCalled));
    }

    public void MarkElevatorMisuse()
    {
        UsedElevatorDuringEmergency = true; // no early-return guard - could happen more than once, that's fine, it's a flag not a counter
        OnObjectiveChanged?.Invoke(nameof(UsedElevatorDuringEmergency));
    }

    public void MarkExtinguisherUsedCorrectly()
    {
        if (ExtinguisherUsedCorrectly) return;
        ExtinguisherUsedCorrectly = true;
        OnObjectiveChanged?.Invoke(nameof(ExtinguisherUsedCorrectly));
    }

    /// <summary>Resets all objectives - call if you add a "restart scenario" option later.</summary>
    public void ResetAll()
    {
        NPCRescued = false;
        FireAlarmActivated = false;
        HelplineCalled = false;
        UsedElevatorDuringEmergency = false;
        ExtinguisherUsedCorrectly = false;
    }
}