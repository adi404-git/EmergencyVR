using UnityEngine;

/// <summary>
/// EmergencyVR - Place on a trigger volume around fire/dense smoke sources. Feeds
/// distance-based proximity into VRHazardEffects every frame the player is inside.
/// Multiple zones can exist simultaneously (e.g. two separate fires) - VRHazardEffects
/// just takes whatever the latest refresh call gives it, decaying naturally if the
/// player leaves all zones.
/// </summary>
[RequireComponent(typeof(Collider))]
public class HazardZone : MonoBehaviour
{
    [Tooltip("The hazard source used to calculate proximity (usually the fire's transform, can be this object itself).")]
    public Transform hazardSource;

    [Tooltip("Distance at which the effect is at 0 intensity (edge of the zone).")]
    public float outerRadius = 6f;
    [Tooltip("Distance at which the effect is at full (1.0) intensity.")]
    public float innerRadius = 1.5f;

    public string playerTag = "Player";

    private Transform playerInZone;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
        if (hazardSource == null) hazardSource = transform;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
            playerInZone = other.transform;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            playerInZone = null;
            if (VRHazardEffects.Instance != null)
                VRHazardEffects.Instance.ClearHazard();
        }
    }

    void Update()
    {
        if (playerInZone == null || VRHazardEffects.Instance == null) return;

        Vector3 source = hazardSource != null ? hazardSource.position : transform.position;
        float dist = Vector3.Distance(source, playerInZone.position);

        float proximity = 1f - Mathf.InverseLerp(innerRadius, outerRadius, dist);
        VRHazardEffects.Instance.RefreshHazard(proximity);
    }
}
