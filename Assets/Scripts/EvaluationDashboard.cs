using UnityEngine;
using TMPro;

/// <summary>
/// EmergencyVR - Physical world-space dashboard at the exit/safe zone. Displays total
/// emergency-phase time and an objectives checklist. Other scripts (NPCFollower's rescue
/// event, alarm button, phone system) call the public Mark___ methods to tick objectives
/// off as gameplay happens - this script doesn't need to know about them.
///
/// SETUP: World Space Canvas placed physically at your exit, with TMP text fields for
/// time + each objective, assigned below. Put a BoxCollider (Is Trigger) at the exit
/// doorway with this script (or a separate ExitTrigger script calling ShowDashboard())
/// so it activates when the player arrives.
/// </summary>
public class EvaluationDashboard : MonoBehaviour
{
    [Header("UI References")]
    public GameObject dashboardRoot; // parent panel, enabled on show
    public TextMeshProUGUI timeText;
    public TextMeshProUGUI npcObjectiveText;
    public TextMeshProUGUI alarmObjectiveText;
    public TextMeshProUGUI helplineObjectiveText;

    [Header("Objective Display Strings")]
    public string completedSuffix = " - Done";
    public string incompleteSuffix = " - Missed";

    [Header("Exit Trigger")]
    [Tooltip("If true, this object's own Collider (set to trigger) activates the dashboard on player entry.")]
    public bool useOwnTrigger = true;
    public string playerTag = "Player";

    // --- Objectives now live in GameProgressTracker - this script just reads them ---
    private bool hasShown = false;

    void Start()
    {
        if (dashboardRoot != null)
            dashboardRoot.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!useOwnTrigger) return;
        if (!other.CompareTag(playerTag)) return;

        ShowDashboard();
    }

    /// <summary>Call directly (e.g. from a separate exit trigger script) if you don't want this object's own collider driving it.</summary>
    public void ShowDashboard()
    {
        if (hasShown) return;
        hasShown = true;

        if (EmergencyFlowManager.Instance != null)
            EmergencyFlowManager.Instance.CompleteGame();

        float elapsed = EmergencyFlowManager.Instance != null
            ? EmergencyFlowManager.Instance.GetElapsedEmergencyTime()
            : 0f;

        if (dashboardRoot != null) dashboardRoot.SetActive(true);
        RefreshDisplay(elapsed);
    }

    void RefreshDisplay(float elapsedSeconds)
    {
        if (timeText != null)
        {
            int minutes = Mathf.FloorToInt(elapsedSeconds / 60f);
            int seconds = Mathf.FloorToInt(elapsedSeconds % 60f);
            timeText.text = $"Time: {minutes:00}:{seconds:00}";
        }

        if (GameProgressTracker.Instance == null)
        {
            Debug.LogWarning($"{name}: No GameProgressTracker found in scene - objective checklist will show as incomplete.");
            SetObjectiveText(npcObjectiveText, "Saved NPC", false);
            SetObjectiveText(alarmObjectiveText, "Activated Fire Alarm", false);
            SetObjectiveText(helplineObjectiveText, "Called Helpline", false);
            return;
        }

        var tracker = GameProgressTracker.Instance;
        SetObjectiveText(npcObjectiveText, "Saved NPC", tracker.NPCRescued);
        SetObjectiveText(alarmObjectiveText, "Activated Fire Alarm", tracker.FireAlarmActivated);
        SetObjectiveText(helplineObjectiveText, "Called Helpline", tracker.HelplineCalled);
    }

    void SetObjectiveText(TextMeshProUGUI field, string label, bool complete)
    {
        if (field == null) return;
        field.text = label + (complete ? completedSuffix : incompleteSuffix);
        field.color = complete ? Color.green : Color.red;
    }

    // Note: objectives are no longer set through this script. Gameplay scripts
    // (NPCFollower, alarm button, phone system) call GameProgressTracker.Instance
    // directly - this dashboard only reads from it when ShowDashboard() fires.
}