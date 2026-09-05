using System.Collections;
using UnityEngine;

public class FireAlarmSystem : MonoBehaviour
{
    [Header("Alarm Audio")]
    public AudioSource sirenAudioSource;

    [Header("Emergency Light GameObjects")]
    [Tooltip("Drag the Point Light GameObjects here. They should be disabled (unchecked) in the Hierarchy by default.")]
    public GameObject[] alarmLightObjects;
    public float flashInterval = 0.5f;

    [Header("System State")]
    public bool isAlarmActive = false;

    private Coroutine flashCoroutine;

    /// <summary>
    /// Call this from your slider trigger event to start the alarm.
    /// </summary>
    public void TriggerAlarm()
    {
        if (isAlarmActive) return;

        isAlarmActive = true;

        // Play looping siren sound
        if (sirenAudioSource != null)
        {
            sirenAudioSource.loop = true;
            sirenAudioSource.Play();
        }

        // Start flashing lights
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashLightsRoutine());
    }

    private IEnumerator FlashLightsRoutine()
    {
        bool currentState = true;

        while (isAlarmActive)
        {
            // Toggle all assigned Light GameObjects on/off
            for (int i = 0; i < alarmLightObjects.Length; i++)
            {
                if (alarmLightObjects[i] != null)
                {
                    alarmLightObjects[i].SetActive(currentState);
                }
            }

            currentState = !currentState;
            yield return new WaitForSeconds(flashInterval);
        }
    }

    public void SilenceAlarm()
    {
        isAlarmActive = false;

        if (flashCoroutine != null) StopCoroutine(flashCoroutine);

        if (sirenAudioSource != null) sirenAudioSource.Stop();

        // Turn off all light GameObjects when alarm is silenced
        if (alarmLightObjects != null)
        {
            for (int i = 0; i < alarmLightObjects.Length; i++)
            {
                if (alarmLightObjects[i] != null)
                {
                    alarmLightObjects[i].SetActive(false);
                }
            }
        }
    }
}