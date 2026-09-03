using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// EmergencyVR - NPC behavior:
/// 1. IDLE/SHOUTING: NPC stays put, periodically shouts "help" (volume grows as player nears).
/// 2. Player enters horizontal distance `detectionDistance` -> switches to FOLLOWING permanently.
/// 3. FOLLOWING: NPC paths to player via NavMeshAgent (no manual movement, so it can't clip
///    through walls or get stuck on geometry - the NavMesh handles avoidance).
/// 4. Optional: NPC reaches a SafeZone trigger -> fires OnRescued for GameManager/scoring.
///
/// REQUIREMENTS BEFORE THIS WORKS:
/// - Scene must have a baked NavMesh covering all floors/stairs the NPC needs to path across
///   (Window > AI > Navigation, mark floor/stairs geometry as Navigation Static, Bake).
///   Without this, agent.SetDestination will silently fail to move the NPC.
/// - NPC GameObject needs: NavMeshAgent, AudioSource, Animator (with a float parameter,
///   default named "Speed", driven from your walking animation blend tree).
/// - VRSubtitleManager must exist in the scene (singleton) for on-screen text lines to show.
/// - GameProgressTracker must exist in the scene (singleton) - MarkRescued() writes to it
///   directly so the end-game dashboard can read progress without this script needing
///   to know about the dashboard at all.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(AudioSource))]
public class NPCFollower : MonoBehaviour
{
    public enum NPCState { Idle, Shouting, Following, Rescued }

    [Header("References")]
    [Tooltip("Assign the player root transform (not the camera) so horizontal distance is measured correctly.")]
    public Transform player;
    public Animator animator;
    [Tooltip("Name of the float parameter on the Animator that drives walk-speed blending.")]
    public string animatorSpeedParam = "Speed";

    [Header("Detection")]
    [Tooltip("Horizontal (XZ) distance at which the NPC notices the player and starts following.")]
    public float detectionDistance = 6f;

    [Header("Shouting (Idle State)")]
    public AudioClip helpShoutClip;
    [Tooltip("Seconds between each shout while waiting for the player.")]
    public float shoutInterval = 4f;
    [Tooltip("Volume when player is at max audible range.")]
    public float minShoutVolume = 0.3f;
    [Tooltip("Volume when player is right at the detection boundary (gets louder as they approach).")]
    public float maxShoutVolume = 1f;
    [Tooltip("Distance beyond which shout volume is clamped to minShoutVolume.")]
    public float maxAudibleDistance = 25f;
    [TextArea] public string helpDialogueText = "Help! Please, help me!";

    [Header("Following (Following State)")]
    [Tooltip("How often (seconds) the NPC recalculates its path to the player. Lower = more responsive, higher = cheaper.")]
    public float pathUpdateInterval = 0.3f;
    [Tooltip("How close the NavMeshAgent stops from the player's exact position.")]
    public float stoppingDistance = 1.5f;

    
    [System.Serializable]
    public class DialogueLine
    {
        public string id;
        [TextArea] public string text;
        public AudioClip clip;
    }
    [Header("Extra Dialogue (extend later)")]
    [Tooltip("Additional lines you can trigger later via PlayExtraDialogue(id) from other scripts/events.")]
    public List<DialogueLine> extraDialogueLines = new List<DialogueLine>();

    [Header("Events (hook GameManager/scoring here)")]
    public UnityEngine.Events.UnityEvent OnStartedFollowing;
    public UnityEngine.Events.UnityEvent OnRescued;

    // --- internal state ---
    public NPCState CurrentState { get; private set; } = NPCState.Idle;
    private NavMeshAgent agent;
    private AudioSource audioSource;
    private Coroutine shoutRoutine;
    private Coroutine pathRoutine;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        audioSource = GetComponent<AudioSource>();
        audioSource.spatialBlend = 1f; // 3D sound - required for distance-based volume to make sense
        audioSource.playOnAwake = false;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // Agent should not move until we explicitly enter Following state
        agent.enabled = true;
        agent.isStopped = true;
    }

    void Start()
    {
        if (player == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
            else Debug.LogWarning($"{name}: No player assigned and no object tagged 'Player' found. Assign manually.");
        }

        EnterIdleShouting();
    }

    void Update()
    {
        if (CurrentState == NPCState.Rescued || player == null) return;

        float horizontalDist = GetHorizontalDistance();

        // One-way transition: Idle/Shouting -> Following, once in range
        if ((CurrentState == NPCState.Idle || CurrentState == NPCState.Shouting) && horizontalDist <= detectionDistance)
        {
            EnterFollowing();
        }

        // While shouting, update volume live based on current distance (gets louder as player nears)
        if (CurrentState == NPCState.Shouting && audioSource.isPlaying)
        {
            audioSource.volume = CalculateShoutVolume(horizontalDist);
        }

        // Drive animator from actual agent velocity so animation matches real movement (no floaty statue-walk)
        if (CurrentState == NPCState.Following && animator != null)
        {
            float speedNormalized = agent.velocity.magnitude / Mathf.Max(agent.speed, 0.01f);
            animator.SetFloat(animatorSpeedParam, speedNormalized);
        }
    }

    float GetHorizontalDistance()
    {
        Vector3 a = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 b = new Vector3(player.position.x, 0f, player.position.z);
        return Vector3.Distance(a, b);
    }

    float CalculateShoutVolume(float horizontalDist)
    {
        // Closer = louder. Clamp between min/max based on distance vs maxAudibleDistance.
        float t = 1f - Mathf.Clamp01(horizontalDist / maxAudibleDistance);
        return Mathf.Lerp(minShoutVolume, maxShoutVolume, t);
    }

    // ---------------- IDLE / SHOUTING ----------------

    void EnterIdleShouting()
    {
        CurrentState = NPCState.Shouting;
        if (shoutRoutine != null) StopCoroutine(shoutRoutine);
        shoutRoutine = StartCoroutine(ShoutLoop());
    }

    IEnumerator ShoutLoop()
    {
        while (CurrentState == NPCState.Shouting)
        {
            if (helpShoutClip != null)
            {
                audioSource.clip = helpShoutClip;
                audioSource.volume = CalculateShoutVolume(GetHorizontalDistance());
                audioSource.Play();
            }

            if (VRSubtitleManager.Instance != null)
                VRSubtitleManager.Instance.ShowDialogue(helpDialogueText);

            yield return new WaitForSeconds(shoutInterval);
        }
    }

    // ---------------- FOLLOWING ----------------

    void EnterFollowing()
    {
        CurrentState = NPCState.Following;

        if (shoutRoutine != null)
        {
            StopCoroutine(shoutRoutine);
            shoutRoutine = null;
        }
        audioSource.Stop();

        agent.isStopped = false;
        agent.stoppingDistance = stoppingDistance;

        if (pathRoutine != null) StopCoroutine(pathRoutine);
        pathRoutine = StartCoroutine(PathUpdateLoop());

        if (VRSubtitleManager.Instance != null)
            VRSubtitleManager.Instance.ShowDialogue("Thank you! I'll follow you.");

        OnStartedFollowing?.Invoke();
    }

    IEnumerator PathUpdateLoop()
    {
        while (CurrentState == NPCState.Following)
        {
            if (player != null && agent.isOnNavMesh)
            {
                agent.SetDestination(player.position);
            }
            yield return new WaitForSeconds(pathUpdateInterval);
        }
    }

    // ---------------- RESCUE / SAFE ZONE ----------------

    /// <summary>
    /// Call this from a SafeZone trigger (OnTriggerEnter checking this NPC's collider)
    /// once evacuation exit is reached. Kept separate from this script so multiple NPCs
    /// can share one SafeZone trigger script.
    /// </summary>
    public void MarkRescued()
    {
        if (CurrentState == NPCState.Rescued) return;

        CurrentState = NPCState.Rescued;

        if (pathRoutine != null) StopCoroutine(pathRoutine);
        agent.isStopped = true;

        if (animator != null) animator.SetFloat(animatorSpeedParam, 0f);

        if (VRSubtitleManager.Instance != null)
            VRSubtitleManager.Instance.ShowDialogue("Made it out safely!");

        // Writes directly to the central tracker - the dashboard (or anything else)
        // reads this later without NPCFollower needing to know it exists.
        if (GameProgressTracker.Instance != null)
            GameProgressTracker.Instance.MarkNPCRescued();
        else
            Debug.LogWarning($"{name}: No GameProgressTracker found in scene - NPC rescue won't be recorded for scoring.");

        OnRescued?.Invoke(); // still fires for any additional Inspector-wired hooks (VFX, sound, etc.)

        // Extend here later: play a relief animation, despawn after delay, etc.
    }

    // ---------------- EXTRA DIALOGUE (for later content) ----------------

    /// <summary>
    /// Call from anywhere (other scripts, UnityEvents, trigger zones) to play an
    /// additional line by its id, defined in the extraDialogueLines list in the Inspector.
    /// </summary>
    public void PlayExtraDialogue(string id)
    {
        var line = extraDialogueLines.Find(l => l.id == id);
        if (line == null)
        {
            Debug.LogWarning($"{name}: No dialogue line found with id '{id}'.");
            return;
        }

        if (line.clip != null)
        {
            audioSource.clip = line.clip;
            audioSource.Play();
        }

        if (VRSubtitleManager.Instance != null)
            VRSubtitleManager.Instance.ShowDialogue(line.text);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionDistance);
    }
}