using UnityEngine;

namespace BikeURP
{
    /// <summary>
    /// Minimal single-track (kinematic bicycle) model with torque (longitudinal) and steering inputs.
    /// Integrates speed and yaw; advances position along the bike's forward.
    /// Optional wheel mesh visuals; no ground contacts or colliders required.
    /// Public API: SetThrottle, SetBrake, SetSteer/SetSteerAnalog/SetSteerDigital; GetSpeedMps/Kph
    /// </summary>
    public class BicyclePhysicsController : MonoBehaviour
    {
    [Header("References (optional visuals)")]
    public Transform frameRoot;      // optional: visual frame/root to apply lean roll to
    [Tooltip("Optional: parent pivot that yaws for steering. The front wheel mesh will only spin around its X (axle) and won't yaw itself.")]
    public Transform frontSteerPivot;
    public Transform frontWheelMesh;
    public Transform rearWheelMesh;
    [Tooltip("Optional: handlebar visual that yaws with steering (use if not already under frontSteerPivot).")]
    public Transform handlebarTransform;
    [Header("Pedal/Crank Visuals (optional)")]
    [Tooltip("Optional single transform for both pedals/crank; rotates as a unit.")]
    public Transform crankPair;
    public Transform crankLeft;
    public Transform crankRight;

        [Header("Animator (optional)")]
        [Tooltip("Animator with an isPedaling bool parameter.")]
        public Animator pedalingAnimator;
        [Tooltip("Animator bool parameter toggled by throttle input.")]
        public string pedalingBoolName = "isPedaling";
    [Tooltip("Animator float parameter updated with steering angle in degrees.")]
    public string steeringFloatName = "floatSteerAngle";

        [Header("Vehicle")]
        public float mass = 80f;        // kg
        public float wheelbase = 1.02f; // m
        public float wheelRadius = 0.34f; // m (for visual spin & torque->force)

        [Header("Steering")]
        public float maxSteerDeg = 30f;
        public float digitalSteerDeg = 18f;
        public float steerResponse = 10f;          // 1/s smoothing toward target
        public float steerSpeedAttenuation = 0.04f; // delta_eff = delta/(1+k|v|)
        public float zeroSpeedYawRateDegPerDeg = 6f; // deg/s per deg at near-zero speed
        public float minSteerSpeedForYaw = 0.5f;     // m/s threshold
    [Tooltip("If false, the bike cannot rotate in place at zero speed (strict single-track behavior).")]
    public bool allowYawInPlace = false;

        [Header("Longitudinal (torque model)")]
        public float maxDriveTorque = 28f; // Nm (rear) — sized for a 12 km/h climb
        public float maxBrakeTorque = 150f; // Nm (both oppose motion)
        public float rollingResistance = 5f; // N per m/s
        public float airDrag = 0.5f;         // N per m/s (linear)
        [Tooltip("Max speed in m/s. Experiment default is 20 km/h (5.56 m/s).")]
        public float maxSpeed = 5.56f;
        [Tooltip("Seconds of full W to reach max speed from rest.")]
        public float timeToMaxSpeed = 3.75f;
        [Tooltip("Seconds to coast from max speed to a stop after releasing W.")]
        public float coastTimeToStop = 5f;

        // Inputs
        [System.NonSerialized] public float throttle01; // -1..1
        [System.NonSerialized] public float brake01;    // 0..1
        [System.NonSerialized] public float steer01;    // -1..1 (for analog)
        [System.NonSerialized] public float safetyBrake01; // 0..1, set by VR assist; Input cannot clear this

        // Internal state
        private float _speed;            // m/s
        private float _yawDeg;           // world yaw angle in degrees
        private float _steerTargetRad;   // commanded steering angle (radians)
        private float _steerAngleRad;    // filtered steering angle (radians)
        private float _frontSpinAngle;   // deg
        private float _rearSpinAngle;    // deg
    private Quaternion _frameBaseLocalRot; // store initial local rotation for frameRoot
    private Quaternion _frontWheelBaseLocalRot; // base local rot to preserve orientation; spin only around X
    private Quaternion _rearWheelBaseLocalRot;  // base local rot to preserve orientation; spin only around X
    private Quaternion _frontSteerBaseLocalRot; // base local rot for the steer pivot; yaw only around Y
    private Quaternion _handlebarBaseLocalRot;  // base local rot for handlebar visual
    private Quaternion _crankPairBaseLocalRot;  // base local rot for pedal pair
    private Quaternion _crankLeftBaseLocalRot;  // base local rot for left crank
    private Quaternion _crankRightBaseLocalRot; // base local rot for right crank
    private float _leanDeg;          // current visual lean
    private float _crankAngle;       // degrees
        private Rigidbody _rigidbody;

        void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
                _rigidbody.useGravity = false;
                _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            }

            // Initialize yaw from current transform
            Vector3 fwd = transform.forward;
            _yawDeg = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            if (frameRoot)
                _frameBaseLocalRot = frameRoot.localRotation;
            if (frontWheelMesh)
                _frontWheelBaseLocalRot = frontWheelMesh.localRotation;
            if (rearWheelMesh)
                _rearWheelBaseLocalRot = rearWheelMesh.localRotation;
            if (frontSteerPivot)
                _frontSteerBaseLocalRot = frontSteerPivot.localRotation;
            if (handlebarTransform)
                _handlebarBaseLocalRot = handlebarTransform.localRotation;
            if (crankPair)
                _crankPairBaseLocalRot = crankPair.localRotation;
            if (crankLeft)
                _crankLeftBaseLocalRot = crankLeft.localRotation;
            if (crankRight)
                _crankRightBaseLocalRot = crankRight.localRotation;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            // Filter steer angle toward target, attenuated by speed
            float targetDelta = _steerTargetRad;
            float vAtt = 1f + steerSpeedAttenuation * Mathf.Abs(_speed);
            targetDelta /= Mathf.Max(0.001f, vAtt);
            float alpha = 1f - Mathf.Exp(-steerResponse * dt);
            _steerAngleRad = Mathf.Lerp(_steerAngleRad, targetDelta, alpha);

            float appliedThrottle = throttle01;
            if (safetyBrake01 > 0.01f && throttle01 > 0f) appliedThrottle = 0f;
            float appliedBrake = brake01;
            if (safetyBrake01 > 0.01f && throttle01 >= -0.05f)
                appliedBrake = Mathf.Max(brake01, safetyBrake01);

            float driveAccel = 0f;
            if (timeToMaxSpeed > 0.05f)
                driveAccel = (maxSpeed / timeToMaxSpeed) * appliedThrottle;

            float coastAccel = 0f;
            if (Mathf.Abs(appliedThrottle) < 0.05f && appliedBrake < 0.05f && Mathf.Abs(_speed) > 0.02f && coastTimeToStop > 0.05f)
            {
                coastAccel = Mathf.Sign(_speed) * (maxSpeed / coastTimeToStop);
            }

            float brakeAccel = 0f;
            if (appliedBrake > 0.01f && Mathf.Abs(_speed) > 0.01f)
            {
                float spaceStopTime = 1.6f;
                brakeAccel = Mathf.Sign(_speed) * appliedBrake * (maxSpeed / spaceStopTime);
            }

            float accel = driveAccel - coastAccel - brakeAccel;
            _speed += accel * dt;
            if (Mathf.Abs(_speed) < 0.02f && Mathf.Abs(appliedThrottle) < 0.05f) _speed = 0f;
            _speed = Mathf.Clamp(_speed, -maxSpeed, maxSpeed);

            // Yaw rate from kinematic bicycle; add near-zero-speed yaw-in-place
            float yawRateDeg;
            if (Mathf.Abs(_speed) < minSteerSpeedForYaw)
            {
                if (allowYawInPlace && Mathf.Abs(_speed) > 1e-3f)
                {
                    float angleDeg = _steerTargetRad * Mathf.Rad2Deg;
                    yawRateDeg = Mathf.Sign(angleDeg) * Mathf.Abs(angleDeg) * zeroSpeedYawRateDegPerDeg;
                }
                else
                {
                    yawRateDeg = 0f; // no rotation in place
                }
            }
            else
            {
                float yawRateRad = (wheelbase > 1e-3f) ? (_speed / wheelbase) * Mathf.Tan(_steerAngleRad) : 0f;
                yawRateDeg = yawRateRad * Mathf.Rad2Deg;
            }
            _yawDeg += yawRateDeg * dt;

            // Advance position along forward
            Quaternion yawRot = Quaternion.Euler(0f, _yawDeg, 0f);
            Vector3 forward = yawRot * Vector3.forward;
            Vector3 nextPos = transform.position + forward * (_speed * dt);
            ApplyPose(nextPos, yawRot);

            // Wheel visuals
            UpdateLean(forward);
            UpdateWheelVisuals(forward);
            UpdateHandlebar();
            UpdateCranks();
            UpdatePedalingAnimation();
        }

        [Header("Visual Lean")]
        [Tooltip("Max visual lean angle (deg)")]
        public float maxLeanDegVisual = 22f;
        [Tooltip("Sensitivity scaling for lean: lean = atan(leanSensitivity * v^2 * curvature / g)")]
        public float leanSensitivity = 3.5f;
    [Tooltip("Deprecated: wheel meshes follow chassis by hierarchy. Kept for compatibility.")]
    public bool wheelMeshesFollowChassisTilt = true;

        private void UpdateLean(Vector3 forward)
        {
            // curvature from current steering angle
            float curvature = (wheelbase > 1e-3f) ? Mathf.Tan(_steerAngleRad) / wheelbase : 0f;
            // lean (rad): atan( k * v^2 * curvature / g )
            const float g = 9.81f;
            float arg = leanSensitivity * _speed * _speed * curvature / g;
            float leanRad = Mathf.Atan(arg);
            // Right turn (positive curvature) should lean right (negative roll if using right-hand rule)
            _leanDeg = -Mathf.Clamp(leanRad * Mathf.Rad2Deg, -maxLeanDegVisual, maxLeanDegVisual);

            if (frameRoot)
            {
                // Apply roll around local forward on the visual frame, preserving its base local rotation
                frameRoot.localRotation = _frameBaseLocalRot * Quaternion.AngleAxis(_leanDeg, Vector3.forward);
            }
        }

        private void UpdateWheelVisuals(Vector3 forward)
        {
            // Apply steering yaw to pivot (if provided). Wheel meshes spin only around local X.
            float steerYawDeg = _steerAngleRad * Mathf.Rad2Deg;
            if (frontSteerPivot)
            {
                frontSteerPivot.localRotation = _frontSteerBaseLocalRot * Quaternion.Euler(0f, steerYawDeg, 0f);
            }

            // Spin wheels about their local X (axle). No Y/Z rotation on the wheel meshes themselves.
            if (frontWheelMesh)
            {
                SpinWheelLocalX(frontWheelMesh, ref _frontSpinAngle, _frontWheelBaseLocalRot);
            }
            if (rearWheelMesh)
            {
                SpinWheelLocalX(rearWheelMesh, ref _rearSpinAngle, _rearWheelBaseLocalRot);
            }
        }

        private void SpinWheelLocalX(Transform mesh, ref float spinAngle, Quaternion baseLocalRot)
        {
            if (!mesh || wheelRadius <= 1e-3f) return;
            float omega = _speed / wheelRadius; // rad/s
            spinAngle += omega * Mathf.Rad2Deg * Time.fixedDeltaTime;
            mesh.localRotation = baseLocalRot * Quaternion.AngleAxis(spinAngle, Vector3.right);
        }

        private void UpdateHandlebar()
        {
            if (!handlebarTransform) return;
            float steerYawDeg = _steerAngleRad * Mathf.Rad2Deg;
            handlebarTransform.localRotation = _handlebarBaseLocalRot * Quaternion.Euler(0f, steerYawDeg, 0f);
        }

    [Header("Pedal Speed (visual)")]
    [Tooltip("Deprecated (unused): pedals are driven by torque input, not speed")] public float pedalRefSpeed = 6f;
    [Tooltip("Crank RPM at full positive input (throttle01=+1)")] public float pedalRPMAtFullInput = 80f;
    [Tooltip("If true, negative input spins pedals backward; otherwise pedals stop on negative input")] public bool allowReversePedal = false;

        private void UpdateCranks()
        {
            if (!crankPair && !crankLeft && !crankRight) return;
            // Drive pedal cadence from torque input (throttle01)
            float input = Mathf.Clamp(throttle01, -1f, 1f);
            float magnitude = Mathf.Abs(input);
            float dir = 1f;
            if (input < 0f)
            {
                if (allowReversePedal)
                    dir = -1f;
                else
                    magnitude = 0f; // do not spin when negative input
            }
            float rpm = pedalRPMAtFullInput * magnitude * dir; // direction inversion handled by animation target script if needed
            _crankAngle += rpm * 360f / 60f * Time.deltaTime;
            _crankAngle = Mathf.Repeat(_crankAngle, 360f);
            if (crankPair)
            {
                crankPair.localRotation = _crankPairBaseLocalRot * Quaternion.AngleAxis(_crankAngle, Vector3.right);
                return;
            }
            if (crankLeft)
                crankLeft.localRotation = _crankLeftBaseLocalRot * Quaternion.AngleAxis(_crankAngle, Vector3.right);
            if (crankRight)
                crankRight.localRotation = _crankRightBaseLocalRot * Quaternion.AngleAxis(_crankAngle + 180f, Vector3.right);
        }

        private void UpdatePedalingAnimation()
        {
            if (!pedalingAnimator) return;

            bool pedaling = throttle01 > 0f && safetyBrake01 < 0.01f;
            if (!string.IsNullOrEmpty(pedalingBoolName))
            {
                pedalingAnimator.SetBool(pedalingBoolName, pedaling);
            }

            float targetSpeed = 1f;
            if (pedaling && maxSpeed > 0.05f)
            {
                float normalized = Mathf.Clamp01(Mathf.Abs(_speed) / maxSpeed);
                targetSpeed = Mathf.Lerp(0.4f, 1.2f, normalized);
            }

            pedalingAnimator.speed = Mathf.MoveTowards(pedalingAnimator.speed, targetSpeed, Time.fixedDeltaTime * 2.5f);
        }

        // Public API
        public void SetThrottle(float value01)
        {
            throttle01 = Mathf.Clamp(value01, -1f, 1f);
        }
        public void SetBrake(float value01) => brake01 = Mathf.Clamp01(value01);

        /// <summary>
        /// VR assist brake. BicycleInput cannot clear this; pass 0 when the path is clear.
        /// </summary>
        public void SetSafetyBrake(float value01) => safetyBrake01 = Mathf.Clamp01(value01);

        /// <summary>
        /// Immediate stop used when the bike is already inside a vehicle collider.
        /// </summary>
        public void HaltForwardMotion()
        {
            if (_speed > 0f) _speed = 0f;
            if (throttle01 > 0f) throttle01 = 0f;
            safetyBrake01 = 1f;
        }

        // Backwards-compatible normalized steer setter: maps -1..1 to ±maxSteerDeg
        public void SetSteer(float value01)
        {
            steer01 = Mathf.Clamp(value01, -1f, 1f);
            float angle = steer01 * maxSteerDeg;
            _steerTargetRad = Mathf.Deg2Rad * angle;
            UpdateAnimatorSteer(angle);
        }

        // Digital: left/right booleans map to ±digitalSteerDeg
        public void SetSteerDigital(bool left, bool right)
        {
            int dir = 0;
            if (left) dir -= 1;
            if (right) dir += 1;
            float angle = dir * digitalSteerDeg;
            _steerTargetRad = Mathf.Deg2Rad * angle;
            steer01 = Mathf.Approximately(maxSteerDeg, 0f) ? 0f : Mathf.Clamp(angle / maxSteerDeg, -1f, 1f);
            UpdateAnimatorSteer(angle);
        }

        // Analog: set absolute steering angle (deg)
        public void SetSteerAnalog(float angleDeg)
        {
            float clamped = Mathf.Clamp(angleDeg, -maxSteerDeg, maxSteerDeg);
            _steerTargetRad = Mathf.Deg2Rad * clamped;
            steer01 = Mathf.Approximately(maxSteerDeg, 0f) ? 0f : clamped / maxSteerDeg;
            UpdateAnimatorSteer(clamped);
        }

        /// <summary>
        /// Place the bike on the road and sync internal yaw so the next physics tick does not snap back.
        /// </summary>
        public void Teleport(Vector3 worldPosition, float yawDegrees)
        {
            _speed = 0f;
            throttle01 = 0f;
            brake01 = 0f;
            safetyBrake01 = 0f;
            steer01 = 0f;
            _steerTargetRad = 0f;
            _steerAngleRad = 0f;
            _yawDeg = yawDegrees;
            worldPosition = SnapToGround(worldPosition);
            ApplyPose(worldPosition, Quaternion.Euler(0f, yawDegrees, 0f));
            if (_rigidbody != null)
            {
                _rigidbody.position = worldPosition;
                _rigidbody.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            }
        }

        private static Vector3 SnapToGround(Vector3 position)
        {
            if (Physics.Raycast(position + Vector3.up * 8f, Vector3.down, out RaycastHit hit, 24f, ~0,
                    QueryTriggerInteraction.Ignore))
            {
                return new Vector3(position.x, hit.point.y + 0.05f, position.z);
            }

            return position;
        }

        private void ApplyPose(Vector3 worldPosition, Quaternion worldRotation)
        {
            if (_rigidbody != null)
            {
                _rigidbody.MovePosition(worldPosition);
                _rigidbody.MoveRotation(worldRotation);
                return;
            }

            transform.position = worldPosition;
            transform.rotation = worldRotation;
        }

        // Helpers
        public float GetSpeedMps() => _speed;
        public float GetSpeedKph() => _speed * 3.6f;
        public float GetLeanDegrees() => _leanDeg; // visual lean roll angle applied to frameRoot
        public float GetCrankAngleDeg() => _crankAngle; // current visual crank angle (0..360)
        public float GetSteerAngleDeg() => _steerAngleRad * Mathf.Rad2Deg; // filtered steer angle currently applied (deg)

        private void UpdateAnimatorSteer(float angleDeg)
        {
            if (pedalingAnimator && !string.IsNullOrEmpty(steeringFloatName))
            {
                // Keep animator informed of the commanded steering angle.
                pedalingAnimator.SetFloat(steeringFloatName, angleDeg);
            }
        }
    }
}
