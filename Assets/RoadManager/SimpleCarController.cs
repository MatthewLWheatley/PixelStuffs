using UnityEngine;

public class SimpleCarController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Forward/Backward speed of the car")]
    public float driveSpeed = 10f;

    [Tooltip("Turning speed of the car")]
    public float turnSpeed = 50f;

    void Update()
    {
        // Get input values (default keys: W/S for vertical, A/D for horizontal)
        float verticalInput = Input.GetAxis("Vertical");
        float horizontalInput = Input.GetAxis("Horizontal");

        // Move the car forward/backward
        // Use Time.deltaTime so movement is frame rate independent
        transform.Translate(Vector3.forward * verticalInput * driveSpeed * Time.deltaTime);

        // Only turn if there's forward or backward movement
        // i.e., if verticalInput != 0
        if (Mathf.Abs(verticalInput) > 0.01f)
        {
            transform.Rotate(Vector3.up, horizontalInput * turnSpeed * Time.deltaTime);
        }
    }
}
