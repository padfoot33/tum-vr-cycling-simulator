using UnityEngine;


using tumvt.sumounity;

namespace tum_shuttle_bus_controller
{


    public class ShuttleBusController : MonoBehaviour, IVehicleController {

        public string id { get; set; } // SUMO Identifiert in Vehicle Dictionary
        private Rigidbody rb;
        private bool inputAccelerate = false;
        private bool inputBrake = false;
        private float toruqeInputAccelerate = 0f;
        private float toruqeInputBrake = 0f;
        private bool inputLeft = false;
        private bool inputRight = false;

        [Header("Vehicle Kinematics")]
        public float currentSpeed = 0.0f;
        public float maxSpeed = 20f; // m/s
        public float acceleration = 10f;
        public float brakingForce = 20f;
        public float maxReverseSpeed = 10f;
        public float deceleration = 5f;
        public float maxSteeringAngle = 25f;
        public float wheelBase; // Distance between front and rear axle
        public float wheelRadius = 0.52f; // [m]
        private float steeringInput = 0f;

        [Header("Wheel Configuration")]
        public GameObject wheelJointFL;
        public GameObject wheelFL;
        public GameObject wheelJointFR;
        public GameObject wheelFR;
        public GameObject wheelRL;
        public GameObject wheelRR;


        // for sumo integration
        SumoSocketClient sock;
        private PIDController pidControllerSpeed;
        private PIDController pidControllerDist;

        private bool bDrawGizmo;
        private Vector2 lookAheadMarker;
        [Header("Sumo Integration")]
        public bool isSumoVehicle = true;
        private Vector2 rbMarker;

        private float stopState;

        Animator animator;

        private bool isHMIAvailable = false;


        void Start()
        {
            rb = GetComponent<Rigidbody>();
            wheelBase = 1.9f; // Adjust based on your bus model


            // get the socketclient with the step info
            sock = GameObject.FindObjectOfType<SumoSocketClient>();

            // velocity controller
            pidControllerDist = new PIDController(15.0f, 0.0f, 0.0f);
            pidControllerSpeed = new PIDController(1.0f, 0.0f, 0.0f);
            bDrawGizmo = true;

            animator = GetComponent<Animator>();

            // check if we have HMI available
            if (GameObject.Find("HMIParent") != null)
            {
                // Debug.Log("HMI found");
                isHMIAvailable = true;
            }
            else
            {
                Debug.LogWarning("HMI not found, no HMI available for this bus model");
            }

        }


        void RotateWheels()
        {

            // Convert linear velocity to angular velocity (in radians per second)
            float angularVelocity = currentSpeed / wheelRadius;

            // Convert to degrees per second
            float degreesPerSecond = angularVelocity * Mathf.Rad2Deg;

            // Calculate rotation amount per frame
            float rotationAmount = degreesPerSecond * Time.deltaTime;


            wheelFL.transform.Rotate(Vector3.right, -1*rotationAmount);
            wheelFR.transform.Rotate(Vector3.right, -1*rotationAmount);
            wheelRL.transform.Rotate(Vector3.right, -1*rotationAmount);
            wheelRR.transform.Rotate(Vector3.right, -1*rotationAmount);

        }


        void ApplySteeringWheelRotation()
        {
            // Calculate the current steering angle based on the input 

            float visualSteeringMultiplicatorGain = 3;
            float visualSteeringAngle = maxSteeringAngle * steeringInput * visualSteeringMultiplicatorGain;

            Mathf.Clamp(visualSteeringAngle,-maxSteeringAngle,maxSteeringAngle);
            
            // Apply the steering angle to the front wheels
            Quaternion steeringRotation = Quaternion.Euler(0, 0, visualSteeringAngle);
            wheelJointFL.transform.localRotation = steeringRotation;
            wheelJointFR.transform.localRotation = steeringRotation;
        }



        void Update()
        {
            // sumo input
            if(isSumoVehicle){

                bool isInsidePhsyicsArea = Vehicle.SumoVehicleDetect(ref sock, id);

                // Debug.Log("isInsidePhsyicsArea: " + isInsidePhsyicsArea);
                
                float steeringGain = (float)0.01;

                if (!isInsidePhsyicsArea){
                    // Debug.LogError("Teleporting Vehicle");
                    rb = Vehicle.SumoTaxiTeleport(ref sock, id, rb, steeringGain, ref pidControllerSpeed, ref pidControllerDist, ref lookAheadMarker);           
                    rb.isKinematic = true; // this makes the vehicle not move in the physics world
                    // Debug.LogError("rb.position: " +rb.position);
                } 
                else
                {
                    // Debug.LogError("Vehicle is inside Physics Area");
                    rb.isKinematic = false;
                    rbMarker.x = rb.position.x;
                    rbMarker.y = rb.position.z;

                    var (steeringValue, torqueInput, desiredVelocity) = Vehicle.SumoVehicleControl(ref sock, id, rb, steeringGain, ref pidControllerSpeed, ref pidControllerDist, ref lookAheadMarker);

                    maxSpeed = desiredVelocity;

                    if (torqueInput>0){
                        toruqeInputAccelerate = torqueInput;
                        toruqeInputBrake = 0f;
                    }

                    else{
                        toruqeInputBrake = torqueInput;
                        toruqeInputAccelerate = 0f;
                    }

                    steeringInput = steeringValue;

                    stopState = Vehicle.getVehicleStopState(ref sock, id);
                }
            }
            // manual input
            else{
                inputBrake = Input.GetKey(KeyCode.UpArrow);
                inputAccelerate = Input.GetKey(KeyCode.DownArrow);
                inputLeft = Input.GetKey(KeyCode.LeftArrow);
                inputRight = Input.GetKey(KeyCode.RightArrow);

                steeringInput = 0f;
                if (inputLeft)
                {
                    steeringInput = -0.1f;
                }
                else if (inputRight)
                {
                    steeringInput = 0.1f;
                }
                
            }

        }

        void OnDrawGizmos(){
            if (bDrawGizmo){
                Gizmos.color = Color.red;
                Vector3 LadPoint = new Vector3(lookAheadMarker.x, 0.1f, lookAheadMarker.y);
                Gizmos.DrawSphere(LadPoint, 1.0f);

                Gizmos.color = Color.blue;
                Vector3 rbMarkerPoint = new Vector3(rbMarker.x, 0.1f, rbMarker.y);
                Gizmos.DrawSphere(rbMarkerPoint, 1.0f);
            }
        }


        void FixedUpdate()
        {
            RotateWheels();
            ApplySteeringWheelRotation();
            
            HandleAcceleration();

            if (currentSpeed != 0)  // Only steer if the bus is moving
            {
                HandleSteering();
            }
            ApplyPhysics();
            // AnimationController();

        }

        void HandleAcceleration()
        {
            //sumo
            if(isSumoVehicle){
                if (toruqeInputAccelerate > 0 && currentSpeed < maxSpeed)
                {
                    currentSpeed += acceleration * Time.fixedDeltaTime;
                }
                else if (toruqeInputBrake > 0 && currentSpeed > -maxReverseSpeed)
                {
                    currentSpeed -= brakingForce * Time.fixedDeltaTime;
                }
                else
                {
                    currentSpeed = Mathf.MoveTowards(currentSpeed, 0, deceleration * Time.fixedDeltaTime);
                }
            }
            // manual
            else{
                if (inputBrake && currentSpeed < maxSpeed)
                {
                    currentSpeed += acceleration * Time.fixedDeltaTime;
                }
                else if (inputAccelerate && currentSpeed > -maxReverseSpeed)
                {
                    currentSpeed -= brakingForce * Time.fixedDeltaTime;
                }
                else
                {
                    currentSpeed = Mathf.MoveTowards(currentSpeed, 0, deceleration * Time.fixedDeltaTime);
                }
            }

        }

        void HandleSteering()
        {
            float speedGain = 0.01f; // make steering at low speeds more realistic
            Mathf.Clamp(speedGain*currentSpeed,-1f,1f);

            float steeringAngleCurrent = maxSteeringAngle * steeringInput;
            // Adjust the pivot point to the rear axle
            Vector3 pivotPoint = transform.position - transform.forward * wheelBase;
            Quaternion rotation = Quaternion.AngleAxis(steeringAngleCurrent, Vector3.up);
            rb.MovePosition(pivotPoint + rotation * (transform.position - pivotPoint));
            rb.MoveRotation(rb.rotation * rotation);
        }

        void ApplyPhysics()
        {
            Vector3 velocity = transform.forward * currentSpeed;
            rb.linearVelocity = new Vector3(velocity.x, rb.linearVelocity.y, velocity.z);
        }

        private void AnimationController()
        {
            stopState = Vehicle.getVehicleStopState(ref sock, id);
            if(stopState == 17)
            {
                animator.SetBool("isDoorOpen", true);
            }
            else
            {
                animator.SetBool("isDoorOpen", false);
            }

        }

    }
}
