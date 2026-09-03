using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class VRMockPlayer : MonoBehaviour
{
    [Header("Movement & Look")]
    public float moveSpeed = 3.5f;
    public float sprintMultiplier = 1.6f;
    public float mouseSensitivity = 2.5f;
    public float gravity = -9.81f;
    public float jumpHeight = 1.0f;

    [Header("VR Rig References")]
    [Tooltip("The camera representing the VR HMD / Head.")]
    public Camera playerCamera;
    [Tooltip("Transform representing the Left Hand controller.")]
    public Transform leftHand;
    [Tooltip("Transform representing the Right Hand controller.")]
    public Transform rightHand;

    [Header("Simulated Hand Physics & Reach")]
    public float defaultHandDistance = 0.55f;
    public float maxHandReach = 1.2f;
    public float handFollowSpeed = 15f;
    public float grabRadius = 0.15f;
    public LayerMask interactionLayers = ~0;

    [Header("Input State (Read-Only)")]
    public bool rightGrip;
    public bool rightTrigger;
    public bool buttonA;
    public bool buttonB;
    public bool leftGrip;
    public bool leftTrigger;
    public bool buttonX;
    public bool buttonY;

    // Internal State
    private CharacterController controller;
    private float verticalRotation = 0f;
    private Vector3 velocity;
    private float currentReach = 0.55f;

    private Rigidbody heldObjectRight;
    private Transform originalParentRight;
    private Rigidbody heldObjectLeft;
    private Transform originalParentLeft;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        // Auto-configure CharacterController for realistic VR body dimensions
        controller.height = 1.75f;
        controller.radius = 0.3f;
        controller.center = new Vector3(0, 0.875f, 0);

        // Fallback: create head camera if not assigned
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera == null)
            {
                GameObject camObj = new GameObject("MockVR_HeadCamera");
                camObj.transform.parent = transform;
                camObj.transform.localPosition = new Vector3(0, 1.65f, 0);
                playerCamera = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
            }
        }

        // Fallback: create hands if missing
        if (rightHand == null) rightHand = CreateMockHand("MockVR_RightHand", new Vector3(0.2f, -0.2f, 0.5f));
        if (leftHand == null) leftHand = CreateMockHand("MockVR_LeftHand", new Vector3(-0.2f, -0.2f, 0.5f));

        currentReach = defaultHandDistance;
        LockCursor(true);
    }

    Transform CreateMockHand(string handName, Vector3 offset)
    {
        GameObject hand = new GameObject(handName);
        hand.transform.parent = playerCamera.transform;
        hand.transform.localPosition = offset;

        // Add a sphere collider so hands physically push doors/triggers
        SphereCollider col = hand.AddComponent<SphereCollider>();
        col.radius = 0.08f;
        col.isTrigger = false;

        return hand.transform;
    }

    void Update()
    {
        HandleCursorLock();
        HandleLook();
        HandleMovement();
        HandleHandReach();
        HandleSimulatedVRInputs();
    }

    void HandleLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Horizontal rotation (Yaw) rotates the entire body
        transform.Rotate(Vector3.up * mouseX);

        // Vertical rotation (Pitch) rotates the head camera only
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -85f, 85f);
        playerCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }

    void HandleMovement()
    {
        bool isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1f);
        Vector3 move = transform.right * x + transform.forward * z;
        controller.Move(move * speed * Time.deltaTime);

        // Jump
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // Gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    void HandleHandReach()
    {
        // Scroll wheel pushes hands further away / brings them closer
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            currentReach = Mathf.Clamp(currentReach + scroll * 1.5f, 0.3f, maxHandReach);
        }

        // Keep hands positioned in front of the camera view
        Vector3 targetRight = playerCamera.transform.position + 
                              (playerCamera.transform.forward * currentReach) + 
                              (playerCamera.transform.right * 0.2f) - 
                              (playerCamera.transform.up * 0.15f);

        Vector3 targetLeft = playerCamera.transform.position + 
                             (playerCamera.transform.forward * currentReach) - 
                             (playerCamera.transform.right * 0.2f) - 
                             (playerCamera.transform.up * 0.15f);

        rightHand.position = Vector3.Lerp(rightHand.position, targetRight, Time.deltaTime * handFollowSpeed);
        leftHand.position = Vector3.Lerp(leftHand.position, targetLeft, Time.deltaTime * handFollowSpeed);

        rightHand.rotation = playerCamera.transform.rotation;
        leftHand.rotation = playerCamera.transform.rotation;
    }

    void HandleSimulatedVRInputs()
    {
        // --- Right Controller Inputs ---
        rightTrigger = Input.GetMouseButton(0); // Left Click = Right Trigger
        rightGrip    = Input.GetKey(KeyCode.G); // G = Right Grip (Grab)
        buttonA      = Input.GetKeyDown(KeyCode.E); // E = Primary A
        buttonB      = Input.GetKeyDown(KeyCode.R); // R = Secondary B

        // --- Left Controller Inputs ---
        leftTrigger  = Input.GetMouseButton(1); // Right Click = Left Trigger
        leftGrip     = Input.GetKey(KeyCode.F); // F = Left Grip (Grab)
        buttonX      = Input.GetKeyDown(KeyCode.Q); // Q = Primary X
        buttonY      = Input.GetKeyDown(KeyCode.Z); // Z = Secondary Y

        // Physics Grab simulation for Right Hand
        if (Input.GetKeyDown(KeyCode.G)) TryGrab(rightHand, ref heldObjectRight, ref originalParentRight);
        if (Input.GetKeyUp(KeyCode.G))   ReleaseGrab(ref heldObjectRight, ref originalParentRight);

        // Physics Grab simulation for Left Hand
        if (Input.GetKeyDown(KeyCode.F)) TryGrab(leftHand, ref heldObjectLeft, ref originalParentLeft);
        if (Input.GetKeyUp(KeyCode.F))   ReleaseGrab(ref heldObjectLeft, ref originalParentLeft);
    }

    void TryGrab(Transform hand, ref Rigidbody heldObj, ref Transform origParent)
    {
        Collider[] hits = Physics.OverlapSphere(hand.position, grabRadius, interactionLayers);
        foreach (var hit in hits)
        {
            Rigidbody rb = hit.attachedRigidbody;
            if (rb != null && !rb.isKinematic)
            {
                heldObj = rb;
                origParent = rb.transform.parent;

                rb.isKinematic = true;
                rb.transform.SetParent(hand);
                break;
            }
        }
    }

    void ReleaseGrab(ref Rigidbody heldObj, ref Transform origParent)
    {
        if (heldObj != null)
        {
            heldObj.isKinematic = false;
            heldObj.transform.SetParent(origParent);
            heldObj.linearVelocity = (playerCamera.transform.forward * 4f) + (velocity * 0.5f); // Throw force
            heldObj = null;
        }
    }

    void HandleCursorLock()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            LockCursor(Cursor.lockState != CursorLockMode.Locked);
        }
    }

    void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void OnDrawGizmosSelected()
    {
        if (rightHand != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(rightHand.position, grabRadius);
        }
        if (leftHand != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(leftHand.position, grabRadius);
        }
    }
}