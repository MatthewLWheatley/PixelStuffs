using UnityEngine;

public class CarController : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider frontLeftWheel;
    public WheelCollider frontRightWheel;
    public WheelCollider rearLeftWheel;
    public WheelCollider rearRightWheel;

    [Header("Wheel Transforms (Visuals)")]
    public Transform frontLeftTransform;
    public Transform frontRightTransform;
    public Transform rearLeftTransform;
    public Transform rearRightTransform;

    [Header("Car Settings")]
    public float maxMotorTorque = 1500f;   // Maximum torque applied to the front wheels
    public float maxSteeringAngle = 30f;   // Maximum steering angle for the front wheels
    public float maxBrakeTorque = 3000f;   // Maximum brake torque applied when braking

    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        // Get player inputs
        float motorInput = Input.GetAxis("Vertical");   // Forward/backward input
        float steerInput = Input.GetAxis("Horizontal");   // Left/right input

        // Calculate values
        float motorTorque = maxMotorTorque * motorInput;
        float steerAngle = maxSteeringAngle * steerInput;

        // Apply motor torque and steering to the front wheels (Front-Wheel Drive)
        frontLeftWheel.motorTorque = motorTorque;
        frontRightWheel.motorTorque = motorTorque;
        frontLeftWheel.steerAngle = steerAngle;
        frontRightWheel.steerAngle = steerAngle;

        // Apply braking torque when the brake key (Space) is pressed
        if (Input.GetKey(KeyCode.Space))
        {
            ApplyBrake(maxBrakeTorque);
        }
        else
        {
            ApplyBrake(0f);
        }

        // Update the visual wheel meshes to match the colliders
        UpdateWheelPoses();
    }

    // Helper method to apply brake torque to all wheels
    private void ApplyBrake(float brakeTorque)
    {
        frontLeftWheel.brakeTorque = brakeTorque;
        frontRightWheel.brakeTorque = brakeTorque;
        rearLeftWheel.brakeTorque = brakeTorque;
        rearRightWheel.brakeTorque = brakeTorque;
    }

    // Update the position and rotation of each wheel mesh based on its WheelCollider
    private void UpdateWheelPoses()
    {
        UpdateWheelPose(frontLeftWheel, frontLeftTransform);
        UpdateWheelPose(frontRightWheel, frontRightTransform);
        UpdateWheelPose(rearLeftWheel, rearLeftTransform);
        UpdateWheelPose(rearRightWheel, rearRightTransform);
    }

    private void UpdateWheelPose(WheelCollider collider, Transform trans)
    {
        Vector3 pos;
        Quaternion quat;
        collider.GetWorldPose(out pos, out quat);
        //trans.position = pos;
        trans.rotation = quat;
    }
}
