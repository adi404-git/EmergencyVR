using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// EmergencyVR - Screen reaction when player is near fire/smoke. Deliberately avoids
/// camera-position shake (a major VR motion-sickness trigger) - uses desaturation +
/// a subtle pulsing vignette instead, which reads as "stress/danger" without moving
/// the camera at all.
///
/// SETUP:
/// 1. Create a Global Volume in your scene (GameObject > Volume > Global Volume).
/// 2. On its Profile, add overrides: "Color Adjustments" and "Vignette".
/// 3. On Color Adjustments: enable the Saturation checkbox (so script can override it), leave default value.
/// 4. On Vignette: enable Intensity checkbox, leave default value.
/// 5. Assign that Volume's Profile to `volumeProfile` below.
/// 6. Place this script once in the scene (e.g. on your GameManager object) - it's a singleton
///    that any number of HazardZone triggers call into.
/// </summary>
public class VRHazardEffects : MonoBehaviour
{
    public static VRHazardEffects Instance { get; private set; }

    [Header("URP Volume")]
    public VolumeProfile volumeProfile;

    [Header("Desaturation")]
    [Tooltip("Saturation value while at full hazard intensity (-100 = fully greyscale).")]
    public float hazardSaturation = -70f;
    public float saturationLerpSpeed = 2f;

    [Header("Vignette Pulse (VR-safe stressor, replaces camera shake)")]
    public float vignetteBaseIntensity = 0.25f;
    public float vignettePulseAmplitude = 0.12f;
    [Tooltip("Low frequency = gentle pulse, not a rapid flashing strobe (avoid anything above ~1-2Hz for comfort).")]
    public float vignettePulseFrequency = 0.6f;

    [Header("Linger / Decay")]
    [Tooltip("If no hazard zone refreshes the effect within this many seconds, it decays back to normal.")]
    public float lingerDuration = 15f;
    public float decaySpeed = 1.5f;

    private ColorAdjustments colorAdjustments;
    private Vignette vignette;

    private float targetIntensity = 0f; // 0 = no effect, 1 = full hazard effect
    private float currentIntensity = 0f;
    private float lastRefreshTime = -999f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (volumeProfile == null)
        {
            Debug.LogError($"{name}: VRHazardEffects has no VolumeProfile assigned - hazard screen effects will not work.");
            return;
        }

        if (!volumeProfile.TryGet(out colorAdjustments))
            Debug.LogError($"{name}: VolumeProfile is missing a Color Adjustments override.");

        if (!volumeProfile.TryGet(out vignette))
            Debug.LogError($"{name}: VolumeProfile is missing a Vignette override.");
    }

    void Update()
    {
        // Auto-decay if no hazard zone has refreshed us recently
        if (Time.time - lastRefreshTime > lingerDuration)
            targetIntensity = 0f;

        currentIntensity = Mathf.MoveTowards(currentIntensity, targetIntensity, decaySpeed * Time.deltaTime);
        ApplyIntensity(currentIntensity);
    }

    void ApplyIntensity(float intensity)
    {
        if (colorAdjustments != null)
        {
            colorAdjustments.saturation.value = Mathf.Lerp(0f, hazardSaturation, intensity);
        }

        if (vignette != null)
        {
            float pulse = Mathf.Sin(Time.time * vignettePulseFrequency * Mathf.PI * 2f) * vignettePulseAmplitude;
            vignette.intensity.value = Mathf.Lerp(0f, vignetteBaseIntensity + pulse, intensity);
        }
    }

    /// <summary>
    /// Call every frame (or on a short repeating interval) from a HazardZone while the
    /// player is inside it. `proximity01` = 0 (edge of zone) to 1 (right at the hazard) lets
    /// the effect scale smoothly with distance rather than snapping on/off.
    /// </summary>
    public void RefreshHazard(float proximity01)
    {
        targetIntensity = Mathf.Clamp01(proximity01);
        lastRefreshTime = Time.time;
    }

    /// <summary>Call when player fully exits all hazard zones to begin the decay immediately rather than waiting for linger timeout.</summary>
    public void ClearHazard()
    {
        targetIntensity = 0f;
    }
}
