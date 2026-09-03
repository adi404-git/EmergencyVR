using UnityEngine;

/// <summary>
/// EmergencyVR - Put on a BoxCollider (Is Trigger = ON) anywhere you want a location-based
/// subtitle line to fire, e.g. reaching the second floor landing.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DialogueTriggerZone : MonoBehaviour
{
    [TextArea(2, 4)]
    [Tooltip("Each entry types out in sequence when triggered.")]
    public string[] lines;

    [Tooltip("If true, this zone only fires once. If false, it fires every time the player re-enters.")]
    public bool triggerOnce = true;

    [Tooltip("Tag used to identify the player's collider.")]
    public string playerTag = "Player";

    private bool hasFired = false;

    void Reset()
    {
        // Convenience default - most people forget to tick this and then wonder why nothing happens
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (triggerOnce && hasFired) return;

        if (VRSubtitleManager.Instance == null)
        {
            Debug.LogWarning($"{name}: DialogueTriggerZone fired but no VRSubtitleManager instance found in scene.");
            return;
        }

        VRSubtitleManager.Instance.ShowDialogue(lines);
        hasFired = true;
    }
}
