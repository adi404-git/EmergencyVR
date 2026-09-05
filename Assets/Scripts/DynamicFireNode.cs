using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EmergencyVR - Pre-placed fire source. Point `fireVisualsParent` at a GameObject
/// (e.g. your "VFX_Fire" object) whose DIRECT CHILDREN are individual pre-placed,
/// pre-sized VFX prefab instances (VFX_Fire_01_Small_Simple, _Medium_Simple, etc.),
/// ALL DISABLED by default in the Hierarchy.
///
/// This script does NOT scale, resize, or adjust intensity of anything - it simply
/// enables those children one at a time as currentHealth rises (so the fire visually
/// "grows" by more flames switching on, not by any single flame getting bigger), and
/// disables them one at a time as the fire is extinguished.
///
/// PERFORMANCE: children are cached once in Awake. Enabling is just SetActive calls -
/// negligible cost, no Instantiate, no per-frame allocations.
/// </summary>
public class DynamicFireNode : MonoBehaviour
{
    public enum FireState { Unignited, Active, Extinguished }
    public FireState CurrentState { get; private set; } = FireState.Unignited;

    [Header("Health / Growth")]
    [Range(0f, 100f)] public float currentHealth = 0f;
    [Tooltip("Health gained per second while active. This also paces how quickly children turn on - higher growthRate = fire visually escalates faster.")]
    public float growthRate = 2f;
    [Tooltip("Starting health when first ignited (should be low enough that only 1 child activates at first).")]
    [Range(1f, 100f)] public float igniteStartHealth = 10f;

    [Header("Visuals - Pre-placed Children")]
    [Tooltip("Parent object whose direct children are individual VFX prefab instances, all disabled by default. They activate one by one as health rises - no scaling, no intensity changes, just on/off.")]
    public Transform fireVisualsParent;

    [Header("Trigger / Damage Radius")]
    [Tooltip("Fixed-size trigger collider the extinguisher/player detects. Set its radius directly in the Inspector - this script does not resize it.")]
    public SphereCollider triggerCollider;

    // --- cached children, fetched once ---
    private List<GameObject> fireChildren = new List<GameObject>();
    private int lastActiveCount = -1; // tracks last applied count so we don't call SetActive redundantly every frame

    /// <summary>Static registry of EVERY placed fire node in the scene, regardless of state. Used to ignite all fires at once when the Emergency phase begins.</summary>
    public static readonly List<DynamicFireNode> AllNodes = new List<DynamicFireNode>();

    /// <summary>Static registry of only currently-active (ignited, not-yet-extinguished) fire nodes. FireScreenTint reads this - no FindObjectsOfType calls anywhere, ever.</summary>
    public static readonly List<DynamicFireNode> ActiveNodes = new List<DynamicFireNode>();

    void Awake()
    {
        if (!AllNodes.Contains(this))
            AllNodes.Add(this);

        if (fireVisualsParent != null)
        {
            for (int i = 0; i < fireVisualsParent.childCount; i++)
            {
                GameObject child = fireVisualsParent.GetChild(i).gameObject;
                fireChildren.Add(child);
                child.SetActive(false); // enforce fully-off starting state regardless of what was left on in the Editor
            }
        }
        else
        {
            Debug.LogWarning($"{name}: DynamicFireNode has no fireVisualsParent assigned - this fire will never show anything visually.");
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

        UpdateActiveChildren();
    }

    /// <summary>Starts this fire. Safe to call multiple times - no-op if already active or extinguished.</summary>
    public void Ignite()
    {
        if (CurrentState != FireState.Unignited) return;

        CurrentState = FireState.Active;
        currentHealth = igniteStartHealth;

        UpdateActiveChildren();

        if (!ActiveNodes.Contains(this))
            ActiveNodes.Add(this);
    }

    /// <summary>
    /// Maps currentHealth (0-100) to how many of the pre-placed children should be
    /// active right now, and only touches SetActive on the ones that actually need
    /// to change state (cheap, no redundant calls every frame).
    /// </summary>
    void UpdateActiveChildren()
    {
        if (fireChildren.Count == 0) return;

        int targetActiveCount = Mathf.Clamp(
            Mathf.CeilToInt((currentHealth / 100f) * fireChildren.Count),
            0,
            fireChildren.Count
        );

        if (targetActiveCount == lastActiveCount) return; // nothing changed, skip entirely

        for (int i = 0; i < fireChildren.Count; i++)
        {
            bool shouldBeActive = i < targetActiveCount;
            if (fireChildren[i].activeSelf != shouldBeActive)
                fireChildren[i].SetActive(shouldBeActive);
        }

        lastActiveCount = targetActiveCount;
    }

    /// <summary>
    /// Reduces health by dousePower worth of extinguishing power (pass a per-second
    /// value multiplied by Time.deltaTime - see FireExtinguisher.cs for the exact
    /// call pattern used). As health drops, children switch off in reverse order.
    /// Returns true the exact frame this node becomes fully extinguished, so the
    /// caller can fire objective-tracking logic exactly once.
    /// </summary>
    public bool Extinguish(float dousePower)
    {
        if (CurrentState != FireState.Active) return false;

        currentHealth -= dousePower;

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            UpdateActiveChildren(); // will correctly switch every child off since target count = 0
            FinishExtinguish();
            return true;
        }

        UpdateActiveChildren();
        return false;
    }

    void FinishExtinguish()
    {
        CurrentState = FireState.Extinguished;
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
        if (triggerCollider == null) return;
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, triggerCollider.radius);
    }
}