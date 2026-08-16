using UnityEngine;
using ExperimentRefs = CyclingExperiment.ExperimentSceneRefs;

namespace CyclingExperiment.Camera
{
    /// <summary>
    /// First-person (and switchable third-person) camera controller for the cyclist.
    /// Follows the cyclist smoothly with mouse-look and subtle cycling sway.
    /// Press 'V' to toggle between First-Person and Third-Person view.
    /// Press 'Esc' to toggle cursor lock.
    /// </summary>
    public class FirstPersonCyclistCamera : MonoBehaviour
    {
        [Header("Follow Target")]
        [SerializeField, Tooltip("The transform to follow (bicycle or cyclist head)")]
        private Transform _followTarget;

        [Header("View Mode")]
        [SerializeField] private bool _isThirdPerson = false;
        [SerializeField] private Vector3 _firstPersonOffset = new Vector3(0f, 1.35f, 0.15f);
        [SerializeField] private Vector3 _thirdPersonOffset = new Vector3(0f, 2.0f, -3.5f);

        [Header("Mouse Look")]
        [SerializeField, Tooltip("Mouse sensitivity for looking around")]
        private float _mouseSensitivity = 2f;

        [SerializeField, Tooltip("Maximum vertical look angle (degrees up/down)")]
        private float _maxVerticalAngle = 60f;

        [SerializeField, Tooltip("Smoothing factor for camera rotation")]
        private float _rotationSmoothing = 15f;

        [Header("Position Tracking")]
        [SerializeField, Tooltip("Smoothing factor for position following")]
        private float _positionSmoothing = 20f;

        [Header("Procedural Cycling Sway")]
        [SerializeField] private bool _enableSway = true;
        [SerializeField] private float _swayAmplitude = 0.02f;
        [SerializeField] private float _swayFrequency = 2.5f;

        [Header("Input Controls")]
        [SerializeField] private bool _lockCursor = true;
        [SerializeField] private KeyCode _cursorToggleKey = KeyCode.Escape;
        [SerializeField] private KeyCode _viewToggleKey = KeyCode.V;

        // Internal state
        private float _yaw;
        private float _pitch;
        private float _swayTimer;
        private bool _cursorLocked;

        private void Awake()
        {
            FindFollowTargetIfNull();
        }

        private void Start()
        {
            FindFollowTargetIfNull();

            if (_followTarget != null)
            {
                _yaw = _followTarget.eulerAngles.y;
                _pitch = 0f;
                SnapToTarget();
            }

            if (_lockCursor)
            {
                SetCursorLock(true);
            }
        }

        private void FindFollowTargetIfNull()
        {
            if (_followTarget != null) return;
            var refs = ExperimentRefs.Instance;
            if (refs != null) _followTarget = refs.bicycleTransform;
        }

        private void LateUpdate()
        {
            if (_followTarget == null) return;

            HandleInput();
            UpdatePosition();
            UpdateRotation();
        }

        private void HandleInput()
        {
            // Toggle cursor lock
            if (Input.GetKeyDown(_cursorToggleKey))
            {
                SetCursorLock(!_cursorLocked);
            }

            // Toggle 1st / 3rd person view
            if (Input.GetKeyDown(_viewToggleKey))
            {
                _isThirdPerson = !_isThirdPerson;
                Debug.Log($"[FirstPersonCyclistCamera] Switched to {(_isThirdPerson ? "Third-Person" : "First-Person")} view.");
            }

            // Mouse look
            if (_cursorLocked)
            {
                float mouseX = Input.GetAxis("Mouse X") * _mouseSensitivity;
                float mouseY = Input.GetAxis("Mouse Y") * _mouseSensitivity;

                _yaw += mouseX;
                _pitch -= mouseY;
                _pitch = Mathf.Clamp(_pitch, -_maxVerticalAngle, _maxVerticalAngle);
            }
        }

        private void UpdatePosition()
        {
            Vector3 offset = _isThirdPerson ? _thirdPersonOffset : _firstPersonOffset;
            Vector3 targetPosition = _followTarget.position + _followTarget.TransformDirection(offset);

            // Add procedural sway in first-person
            if (_enableSway && !_isThirdPerson)
            {
                _swayTimer += Time.deltaTime * _swayFrequency;
                float swayX = Mathf.Sin(_swayTimer) * _swayAmplitude;
                float swayY = Mathf.Cos(_swayTimer * 2f) * _swayAmplitude * 0.5f;
                targetPosition += _followTarget.right * swayX + _followTarget.up * swayY;
            }

            transform.position = Vector3.Lerp(transform.position, targetPosition, _positionSmoothing * Time.deltaTime);
        }

        private void UpdateRotation()
        {
            Quaternion targetRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _rotationSmoothing * Time.deltaTime);
        }

        private void SetCursorLock(bool locked)
        {
            _cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        public void SnapToTarget()
        {
            if (_followTarget == null) return;
            Vector3 offset = _isThirdPerson ? _thirdPersonOffset : _firstPersonOffset;
            transform.position = _followTarget.position + _followTarget.TransformDirection(offset);
            _yaw = _followTarget.eulerAngles.y;
            _pitch = 0f;
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        public void SetFollowTarget(Transform target)
        {
            _followTarget = target;
            if (target != null)
            {
                _yaw = target.eulerAngles.y;
                _pitch = 0f;
                SnapToTarget();
            }
        }
    }
}
