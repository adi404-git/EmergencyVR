using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EmergencyVR - Pre-placed fire/smoke source. Place this script on an empty GameObject
/// wherever a fire can start or spread to in your building, with fire/smoke ParticleSystems
/// and a Light as children (already in the scene, just inactive - NOT instantiated at
/// runtime, so there's zero Instantiate/GC cost when a fire starts).
///
/// PERFORMANCE NOTE: All active nodes register themselves in a static list on Ignite and
/// remove themselves on Extinguish/disable. FireScreenTint reads this list directly - no
/// FindObjectsOfType calls anywhere, ever.
/// </summary>
public class DynamicFireNode : MonoBehaviour
{
    public enum FireState { Unignited, Active, Extinguished }
    public FireState CurrentState { get; private set; } = FireState.Unignited;

    [Header("Health / Growth")]
    [Range(0f, 100f)] public float currentHealth = 0f;
    [Tooltip("Health gained per second while active and not being extinguished.")]
    public float growthRate = 2f;
    [Tooltip("Starting health when first ignited.")]
    [Range(1f, 100f)] public float igniteStartHealth = 15f;

    [Header("Visuals - VFX Prefab Roots")]
    [Tooltip("Parent transform holding your fire VFX prefab instance (e.g. VFX_Fire_01_Small_Simple), pre-placed as a child of this node. Can contain any number of internal ParticleSystems - the whole thing scales together.")]
    public Transform fireVisualRoot;
    [Tooltip("Parent transform holding your smoke VFX prefab instance (e.g. VFX_Fire_01_Small_Simple_Smoke).")]
    public Transform smokeVisualRoot;
    public Light fireLight;

    [Tooltip("Uniform scale applied to fireVisualRoot/smokeVisualRoot at health 0 and 100. Tune these by eye once the prefab is in place - a Small VFX prefab might range e.g. 0.5 to 1.3.")]
    public float minVisualScale = 0.5f;
    public float maxVisualScale = 1.4f;
    public float maxLightRange = 8f;
    public float maxLightIntensity = 3f;

    [Header("Trigger / Damage Radius")]
    [Tooltip("SphereCollider whose radius scales with health - defines how close the player/extinguisher must be to interact with this fire.")]
    public SphereCollider triggerCollider;
    public float minTriggerRadius = 0.5f;
    public float maxTriggerRadius = 2.5f;

    // --- cached nested particle systems, fetched once so we never call GetComponentsInChildren at runtime repeatedly ---
    private ParticleSystem[] fireSystems;
    private ParticleSystem[] smokeSystems;
    private float baseLightIntensity;

    /// <summary>Static registry of EVERY placed fire node in the scene, regardless of state. Used to ignite all fires at once when the Emergency phase begins.</summary>
    public static readonly List<DynamicFireNode> AllNodes = new List<DynamicFireNode>();

    /// <summary>Static registry of only currently-active (ignited, not-yet-extinguished) fire nodes. FireScreenTint reads this - no FindObjectsOfType calls anywhere, ever.</summary>
    public static readonly List<DynamicFireNode> ActiveNodes = new List<DynamicFireNode>();

    void Awake()
    {
        if (!AllNodes.Contains(this))
            AllNodes.Add(this);

        if (fireVisualRoot != null)
        {
            fireSystems = fireVisualRoot.GetComponentsInChildren<ParticleSystem>(true);
            fireVisualRoot.localScale = Vector3.one * minVisualScale;
        }
        if (smokeVisualRoot != null)
        {
            smokeSystems = smokeVisualRoot.GetComponentsInChildren<ParticleSystem>(true);
            smokeVisualRoot.localScale = Vector3.one * minVisualScale;
        }
        if (fireLight != null)
        {
            baseLightIntensity = fireLight.intensity;
            fireLight.enabled = false;
        }
    }

    /// <summary>
    /// Ignites every placed DynamicFireNode in the scene at once. Call this exactly once,
    /// from EmergencyFlowManager the moment the Emergency phase begins - never before,
    /// so fires stay off during the tutorial/prep phase.
    /// </summary>
    public static void IgniteAllPlacedFires()
    {
        for (int i = 0; i < AllNodes.Count; i++)
        {
            if (AllNodes[i] != null)
                AllNodes[i].Ignite();
        }
    }

    void Update()
    {
        if (CurrentState != FireState.Active) return;

        currentHealth += growthRate * Time.deltaTime;
        currentHealth = Mathf.Clamp(currentHealth, 0f, 100f);

        ApplyVisualScale();
    }

    /// <summary>Starts this fire. Safe to call multiple times - no-op if already active or extinguished.</summary>
    public void Ignite()
    {
        if (CurrentState != FireState.Unignited) return;

        CurrentState = FireState.Active;
        currentHealth = igniteStartHealth;

        PlayAll(fireSystems);
        PlayAll(smokeSystems);
        if (fireLight != null) fireLight.enabled = true;

        ApplyVisualScale();

        if (!ActiveNodes.Contains(this))
            ActiveNodes.Add(this);
    }

    static void PlayAll(ParticleSystem[] systems)
    {
        if (systems == null) return;
        for (int i = 0; i < systems.Length; i++)
            if (systems[i] != null) systems[i].Play();
    }

    static void StopAll(ParticleSystem[] systems)
    {
        if (systems == null) return;
        for (int i = 0; i < systems.Length; i++)
            if (systems[i] != null) systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void ApplyVisualScale()
    {
        float t = currentHealth / 100f;
        float scale = Mathf.Lerp(minVisualScale, maxVisualScale, t);

        if (fireVisualRoot != null) fireVisualRoot.localScale = Vector3.one * scale;
        if (smokeVisualRoot != null) smokeVisualRoot.localScale = Vector3.one * scale;

        if (fireLight != null)
        {
            fireLight.range = Mathf.Lerp(0.5f, maxLightRange, t);
            fireLight.intensity = Mathf.Lerp(baseLightIntensity, maxLightIntensity, t);
        }

        if (triggerCollider != null)
        {
            triggerCollider.radius = Mathf.Lerp(minTriggerRadius, maxTriggerRadius, t);
        }
    }

    /// <summary>
    /// Reduces health by dousePower * Time.deltaTime worth of "extinguishing power" -
    /// call this every frame the extinguisher spray is hitting this node (pass
    /// dousePower already multiplied by deltaTime, or raw per-second value - see
    /// FireExtinguisher.cs for the exact call pattern used).
    /// Returns true the exact frame this node becomes fully extinguished (health hits 0),
    /// so the caller can fire objective-tracking logic exactly once.
    /// </summary>
    public bool Extinguish(float dousePower)
    {
        if (CurrentState != FireState.Active) return false;

        currentHealth -= dousePower;

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            ApplyVisualScale();
            FinishExtinguish();
            return true;
        }

        ApplyVisualScale();
        return false;
    }

    void FinishExtinguish()
    {
        CurrentState = FireState.Extinguished;

        StopAll(fireSystems);
        StopAll(smokeSystems);
        if (fireLight != null) fireLight.enabled = false;

        ActiveNodes.Remove(this);
    }

    void OnDisable()
    {
        // Safety net - ensures a destroyed/disabled node never lingers in either static list
        ActiveNodes.Remove(this);
        AllNodes.Remove(this);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.4f);
        float radius = triggerCollider != null ? triggerCollider.radius : maxTriggerRadius;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}