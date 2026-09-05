using System.Collections;
using UnityEngine;

/// <summary>
/// EmergencyVR - Fire alarm siren + strobe lights. Call TriggerAlarm() from your
/// alarm button/lever's interaction event.
///
/// Fixed vs. the original version:
/// 1. Each light now flashes on its OWN coroutine with a small random start offset,
///    so they don't all snap on/off in perfect unison (real alarm strobe arrays aren't
///    synced - this reads as far more convincing).
/// 2. TriggerAlarm() now reports to GameProgressTracker - previously this never
///    happened, so the "Activated Fire Alarm" objective could never be completed.
/// 3. Added diagnostics so a silent misconfiguration (missing AudioClip, empty light
///    array, etc.) shows up in the Console instead of just "nothing happens".
/// </summary>
public class FireAlarmSystem : MonoBehaviour
{
    [Header("Alarm Audio")]
    public AudioSource sirenAudioSource;

    [Header("Emergency Light GameObjects")]
    [Tooltip("Drag the Point Light GameObjects here. They should be disabled (unchecked) in the Hierarchy by default.")]
    public GameObject[] alarmLightObjects;
    public float flashInterval = 0.5f;
    [Tooltip("Each light gets a random start delay between 0 and this value, so the strobes don't all sync up perfectly.")]
    public float maxStaggerOffset = 0.2f;

    public bool IsAlarmActive { get; private set; } = false;

    private Coroutine[] lightRoutines;

    void Awake()
    {
        // Diagnostics up front - catches the most common reasons "the siren doesn't work"
        // silently, instead of you discovering it mid-testing with no clue why.
        if (sirenAudioSource == null)
            Debug.LogWarning($"{name}: No sirenAudioSource assigned - siren sound will never play.");
        else if (sirenAudioSource.clip == null)
            Debug.LogWarning($"{name}: sirenAudioSource has no AudioClip assigned - TriggerAlarm() will call Play() on silence.");

        if (alarmLightObjects == null || alarmLightObjects.Length == 0)
            Debug.LogWarning($"{name}: alarmLightObjects is empty - no strobe lights will activate.");
    }

    /// <summary>
    /// Call this from your alarm button/lever's trigger event to start the alarm.
    /// </summary>
    public void TriggerAlarm()
    {
        if (IsAlarmActive)
        {
            Debug.Log($"{name}: TriggerAlarm() called but alarm is already active - ignoring.");
            return;
        }

        IsAlarmActive = true;
        Debug.Log($"{name}: Alarm triggered."); // remove/comment out once confirmed working - useful now to confirm this method is actually being reached

        if (sirenAudioSource != null)
        {
            sirenAudioSource.loop = true;
            sirenAudioSource.Play();
        }

        if (alarmLightObjects != null)
        {
            lightRoutines = new Coroutine[alarmLightObjects.Length];
            for (int i = 0; i < alarmLightObjects.Length; i++)
            {
                if (alarmLightObjects[i] == null) continue;
                lightRoutines[i] = StartCoroutine(FlashSingleLight(i));
            }
        }

        if (GameProgressTracker.Instance != null)
            GameProgressTracker.Instance.MarkFireAlarmActivated();
        else
            Debug.LogWarning($"{name}: No GameProgressTracker found in scene - alarm activation won't be recorded for scoring.");
    }

    IEnumerator FlashSingleLight(int index)
    {
        GameObject light = alarmLightObjects[index];

        // Small random offset so this light's on/off cycle isn't perfectly synced with the others
        yield return new WaitForSeconds(Random.Range(0f, maxStaggerOffset));

        bool state = true;
        while (IsAlarmActive)
        {
            light.SetActive(state);
            state = !state;
            yield return new WaitForSeconds(flashInterval);
        }

        light.SetActive(false); // ensure it ends OFF, not mid-flash, when the alarm is silenced
    }

    public void SilenceAlarm()
    {
        IsAlarmActive = false; // each light's own coroutine will see this false and exit + turn itself off within one flashInterval

        if (sirenAudioSource != null) sirenAudioSource.Stop();

        // Force everything off immediately rather than waiting up to flashInterval for
        // each coroutine to notice - more responsive if the player silences it manually.
        if (alarmLightObjects != null)
        {
            for (int i = 0; i < alarmLightObjects.Length; i++)
            {
                if (alarmLightObjects[i] != null)
                    alarmLightObjects[i].SetActive(false);
            }
        }
    }
}