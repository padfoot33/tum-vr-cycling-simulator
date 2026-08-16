using UnityEngine;

public class EditorTriggerTestMove : MonoBehaviour
{
    public float speed = 2f;
    public float steeringSpeed = 5f;
    public float maxSteerAngle = 30f;
    
    Rigidbody rb;
    float currentSteerAngle = 0f;
    Transform frontWheelTransform;

    void Awake() => rb = GetComponent<Rigidbody>();

    void Start()
    {
        // Try to find the front wheel or fork for steering
        frontWheelTransform = transform.Find("FrontWheel") ?? transform.Find("lowerFork") ?? transform.Find("Handles");
    }

    void FixedUpdate()
    {
        // Hold T key to move forward
        if (!Input.GetKey(KeyCode.T)) return;

        HandleSteering();
        MoveForward();
    }

    void HandleSteering()
    {
        // Arrow keys or A/D for steering
        float steerInput = 0f;
        
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A))
            steerInput = -1f;
        else if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D))
            steerInput = 1f;

        // Smoothly interpolate steering angle
        currentSteerAngle = Mathf.Lerp(currentSteerAngle, steerInput * maxSteerAngle, Time.fixedDeltaTime * steeringSpeed);

        // Apply steering rotation
        transform.Rotate(0f, currentSteerAngle * Time.fixedDeltaTime, 0f);
    }

    void MoveForward()
    {
        if (rb != null)
            rb.MovePosition(rb.position + transform.forward * speed * Time.fixedDeltaTime);
        else
            transform.position += transform.forward * speed * Time.deltaTime;
    }
}