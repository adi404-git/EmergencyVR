using UnityEngine;

/// <summary>
/// EmergencyVR - Solves the "AutoHand destroys/rebuilds its own child hierarchy at
/// runtime" problem: instead of parenting this canvas to the VR camera in the Editor
/// (which AutoHand would wipe out), this script finds the real camera AFTER AutoHand
/// has finished setting itself up (in Start), and attaches to it then.
///
/// Put this script on your World Space Canvas root object (the same object that has
/// the Canvas component). Leave the Canvas at the scene root in the Hierarchy - do
/// NOT manually nest it under the AutoHand rig.
/// </summary>
public class CanvasFollowVRCamera : MonoBehaviour
{
    [Header("Camera Finding")]
    [Tooltip("If your VR camera is tagged 'MainCamera', leave this blank - Camera.main will find it automatically. Otherwise, type the exact GameObject name to search for (e.g. 'CenterEyeAnchor').")]
    public string cameraObjectNameFallback = "";

    [Header("Positioning (local space relative to the camera once attached)")]
    [Tooltip("Forward distance from the eyes, and vertical offset (negative = lower, per GTA-style subtitles sitting slightly below center).")]
    public Vector3 localOffset = new Vector3(0f, -0.1f, 1.4f);

    [Tooltip("How many seconds to keep retrying if the camera isn't found immediately (AutoHand may take a frame or two to spawn its rig).")]
    public float findCameraTimeout = 5f;

    private Transform vrCamera;
    private float searchTimer = 0f;
    private bool attached = false;

    void Start()
    {
        TryFindAndAttach();
    }

    void Update()
    {
        if (attached) return;

        searchTimer += Time.deltaTime;
        if (searchTimer > findCameraTimeout)
        {
            Debug.LogError($"{name}: CanvasFollowVRCamera could not find the VR camera within {findCameraTimeout}s. " +
                "Check that your VR camera is tagged 'MainCamera' or set cameraObjectNameFallback to its exact name.");
            enabled = false; // stop retrying every frame once we've given up
            return;
        }

        TryFindAndAttach();
    }

    void TryFindAndAttach()
    {
        Camera cam = Camera.main;

        if (cam == null && !string.IsNullOrEmpty(cameraObjectNameFallback))
        {
            GameObject found = GameObject.Find(cameraObjectNameFallback);
            if (found != null) cam = found.GetComponent<Camera>();
        }

        if (cam == null) return; // not found yet, will retry next frame via Update

        vrCamera = cam.transform;

        transform.SetParent(vrCamera, worldPositionStays: false);
        transform.localPosition = localOffset;
        transform.localRotation = Quaternion.identity;

        attached = true;
        Debug.Log($"{name}: Canvas successfully attached to VR camera '{vrCamera.name}'.");
    }
}