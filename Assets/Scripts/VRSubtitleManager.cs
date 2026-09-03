using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// EmergencyVR - GTA-style world-space subtitle/instruction system for VR.
/// Uses TextMeshProUGUI's maxVisibleCharacters for the typewriter effect
/// (much cheaper than rebuilding the string every frame, and rich-text safe).
///
/// SETUP: Create a World Space Canvas parented under your VR camera, positioned
/// ~1.2-1.5m forward and slightly below eye level (local pos roughly (0, -0.1, 1.4)),
/// small scale (e.g. 0.002 per axis - world space canvases use real-world meters).
/// Add a TextMeshProUGUI child, assign it + a CanvasGroup here.
/// </summary>
public class VRSubtitleManager : MonoBehaviour
{
    public static VRSubtitleManager Instance { get; private set; }

    [Header("References")]
    public TextMeshProUGUI subtitleText;
    public CanvasGroup canvasGroup;

    [Header("Typewriter Settings")]
    [Tooltip("Characters revealed per second. Frame-rate independent (accumulates via Time.deltaTime).")]
    public float charsPerSecond = 35f;
    [Tooltip("How long a fully-typed line stays on screen before erasing/advancing, in seconds.")]
    public float lineDisplayDuration = 7f;
    public float fadeInTime = 0.2f;
    public float fadeOutTime = 0.35f;

    private Queue<string> lineQueue = new Queue<string>();
    private Coroutine activeRoutine;
    [Header("VR Head Tracking Follow")]
    [Tooltip("Target head camera to follow. If null, automatically finds Camera.main at Start.")]
    public Transform headCamera;
    
    [Tooltip("Local offset relative to the head camera (X: left/right, Y: up/down, Z: forward distance).")]
    public Vector3 localOffset = new Vector3(0f, -0.15f, 1.4f);
    
    [Tooltip("How smoothly the UI follows head rotation (higher = faster).")]
    public float followSpeed = 12f;

    void Start()
    {
        // Auto-find main VR camera if not assigned
        if (headCamera == null && Camera.main != null)
        {
            headCamera = Camera.main.transform;
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (subtitleText != null) subtitleText.maxVisibleCharacters = 0;
    }

    /// <summary>
    /// Queues a multi-line sequence. Each line types out, holds, then fades before the next
    /// begins. If a sequence is already playing, new lines are appended to the queue rather
    /// than interrupting - so overlapping triggers never visually clash.
    /// </summary>
    public void ShowDialogue(string[] lines)
    {
        if (lines == null || lines.Length == 0) return;

        foreach (var line in lines)
        {
            if (!string.IsNullOrEmpty(line))
                lineQueue.Enqueue(line);
        }

        if (activeRoutine == null)
            activeRoutine = StartCoroutine(ProcessQueue());
    }

    /// <summary>Convenience overload for a single line.</summary>
    public void ShowDialogue(string line) => ShowDialogue(new[] { line });

    IEnumerator ProcessQueue()
    {
        while (lineQueue.Count > 0)
        {
            string line = lineQueue.Dequeue();
            yield return TypeLine(line);
            yield return new WaitForSeconds(lineDisplayDuration);
            yield return FadeCanvas(1f, 0f, fadeOutTime);
        }

        activeRoutine = null;
    }

    IEnumerator TypeLine(string line)
    {
        if (subtitleText == null) yield break;

        subtitleText.text = line;
        subtitleText.ForceMeshUpdate(); // required so textInfo.characterCount is accurate immediately
        int totalChars = subtitleText.textInfo.characterCount;
        subtitleText.maxVisibleCharacters = 0;

        yield return FadeCanvas(0f, 1f, fadeInTime);

        float revealed = 0f;
        while (revealed < totalChars)
        {
            revealed += charsPerSecond * Time.deltaTime; // frame-rate independent
            subtitleText.maxVisibleCharacters = Mathf.FloorToInt(revealed);
            yield return null;
        }

        subtitleText.maxVisibleCharacters = totalChars; // ensure fully revealed even on frame overshoot
    }

    IEnumerator FadeCanvas(float from, float to, float duration)
    {
        if (canvasGroup == null) yield break;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        canvasGroup.alpha = to;
    }

    /// <summary>Immediately clears any queued/typing dialogue. Use sparingly (e.g. scene transitions).</summary>
    public void ClearAll()
    {
        lineQueue.Clear();
        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = null;
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }
    void LateUpdate()
    {
        if (headCamera == null)
        {
            if (Camera.main != null) headCamera = Camera.main.transform;
            return;
        }

        // Calculate target position in front of the head
        Vector3 targetPosition = headCamera.position + (headCamera.rotation * localOffset);
        Quaternion targetRotation = headCamera.rotation;

        // Smoothly interpolate position and rotation to prevent VR motion discomfort
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * followSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * followSpeed);
    }
}
