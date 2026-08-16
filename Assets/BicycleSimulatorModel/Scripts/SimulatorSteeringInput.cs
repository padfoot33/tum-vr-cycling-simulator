using UnityEngine;
using UnityEngine.InputSystem;

public class SimulatorSteeringInput : MonoBehaviour
{
    public InputActionAsset inputActions; // Assign in Inspector
    private InputAction steerAction;
    public float steeringInput;

    private void Awake()
    {
        var vehicleControls = inputActions.FindActionMap("VehicleControls");
        steerAction = vehicleControls.FindAction("Steer");
    }

    private void OnEnable()
    {
        steerAction.Enable();
    }

    private void OnDisable()
    {
        steerAction.Disable();
    }

    private void Update()
    {
        // Vector2 steeringInput = steerAction.ReadValue<Vector2>();
        float actualSteeringInput = steerAction.ReadValue<float>();
        float SteerMiddle = 0.6901489f;
        float maxLeftRight = 0.22f;
        float steeringInputNormalized = (actualSteeringInput - SteerMiddle)/maxLeftRight;
        steeringInput = Mathf.Clamp(steeringInputNormalized, -1f, 1f);
        
        // Use steeringInput to control your vehicle
        // Debug.Log($"Steering: {steeringInput}|{actualSteeringInput}");
    }
}
