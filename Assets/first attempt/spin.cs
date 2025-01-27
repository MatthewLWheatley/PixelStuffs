using UnityEngine;

public class Spin : MonoBehaviour
{
    [Header("Rotation Axes")]
    [Tooltip("Enable rotation around X axis")]
    public bool rotateX = false;

    [Tooltip("Enable rotation around Y axis")]
    public bool rotateY = false;

    [Tooltip("Enable rotation around Z axis")]
    public bool rotateZ = false;

    [Header("Rotation Speed")]
    [Tooltip("Speed of rotation in degrees per second")]
    public float rotationSpeed = 50f;

    // Store the rotation values
    private Vector3 rotationVector;

    void Update()
    {
        // Reset rotation vector
        rotationVector = Vector3.zero;

        // Add rotation for each enabled axis
        if (rotateX)
            rotationVector.x = rotationSpeed;
        if (rotateY)
            rotationVector.y = rotationSpeed;
        if (rotateZ)
            rotationVector.z = rotationSpeed;

        // Apply rotation
        transform.Rotate(rotationVector * Time.deltaTime);
    }
}