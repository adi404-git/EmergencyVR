using UnityEngine;

public class VRDesktopMovement : MonoBehaviour
{
    public float speed = 4f;
    private Rigidbody rb;
    private Transform camTransform;

    void Start()
    {
        // Grabs the physics body of the AutoHandPlayer
        rb = GetComponent<Rigidbody>();
        // Grabs the Main Camera so you walk in the direction you look
        camTransform = Camera.main.transform; 
    }

    void FixedUpdate()
    {
        // Listens for standard W, A, S, D or Arrow Keys
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        // Calculate directions based on where you are looking
        Vector3 forward = camTransform.forward;
        Vector3 right = camTransform.right;

        // Keep movement flat so you don't fly into the air
        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();

        // Apply the movement to the physics body
        Vector3 move = (forward * z + right * x) * speed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + move);
    }
}