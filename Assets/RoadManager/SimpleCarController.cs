using UnityEngine;

public class SimpleCarController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Forward/Backward speed of the car")]
    public float driveSpeed = 10f;

    [Tooltip("Turning speed of the car")]
    public float turnSpeed = 50f;

    // Flag to track if the first input has been made
    private bool hasStarted = false;

    void Update()
    {
        // Check for the first input before unfreezing the car
        if (!hasStarted)
        {
            // Check if either vertical or horizontal input has been made
            if (Mathf.Abs(Input.GetAxis("Vertical")) > 0.01f || Mathf.Abs(Input.GetAxis("Horizontal")) > 0.01f)
            {
                hasStarted = true;
            }
            else
            {
                // No input yet: car remains frozen in the air
                return;
            }
        }

        // Get current input values
        float verticalInput = Input.GetAxis("Vertical");
        float horizontalInput = Input.GetAxis("Horizontal");

        // Move the car forward/backward (frame rate independent)
        transform.Translate(Vector3.forward * verticalInput * driveSpeed * Time.deltaTime);

        // Only turn if there's forward/backward movement
        if (Mathf.Abs(verticalInput) > 0.01f)
        {
            transform.Rotate(Vector3.up, horizontalInput * turnSpeed * Time.deltaTime);
        }
    }
}
