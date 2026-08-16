using UnityEngine;
using ExperimentRefs = CyclingExperiment.ExperimentSceneRefs;

namespace CyclingExperiment.Camera
{
    /// <summary>
    /// Smooth follow camera for the bicycle (similar to standard racing / vehicle chase camera).
    /// Naturally follows behind the bicycle and smoothly looks forward in the direction of riding.
    /// Press 'V' to toggle between 3rd-Person Chase Cam and 1st-Person Cockpit View.
    /// </summary>
    public class SmoothFollowBicycleCamera : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField, Tooltip("Transform of the bicycle to follow")]
        private Transform target;

        [Header("3rd Person Chase Settings")]
        [SerializeField] private float distance = 3.5f;
        [SerializeField] private float height = 1.8f;
        [SerializeField] private float heightDamping = 4.0f;
        [SerializeField] private float rotationDamping = 4.0f;
        [SerializeField] private float lookAheadDistance = 3.0f;
        [SerializeField] private float lookAtHeightOffset = 1.2f;

        [Header("1st Person Cockpit Settings")]
        [SerializeField] private bool isFirstPerson = false;
        [SerializeField] private Vector3 firstPersonOffset = new Vector3(0f, 1.35f, 0.2f);

        [Header("Controls")]
        [SerializeField] private KeyCode viewToggleKey = KeyCode.V;

        private void Start()
        {
            BindTargetIfNeeded();
            SnapToTarget();
        }

        private void BindTargetIfNeeded()
        {
            if (target != null) return;
            var refs = ExperimentRefs.Instance;
            if (refs != null) target = refs.bicycleTransform;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // Toggle 1st / 3rd person with V key
            if (Input.GetKeyDown(viewToggleKey))
            {
                isFirstPerson = !isFirstPerson;
            }

            if (isFirstPerson)
            {
                UpdateFirstPerson();
            }
            else
            {
                UpdateThirdPerson();
            }
        }

        private void UpdateThirdPerson()
        {
            // Calculate desired rotation and height based on target's forward
            float wantedRotationAngle = target.eulerAngles.y;
            float wantedHeight = target.position.y + height;

            float currentRotationAngle = transform.eulerAngles.y;
            float currentHeight = transform.position.y;

            // Damp the rotation and height
            currentRotationAngle = Mathf.LerpAngle(currentRotationAngle, wantedRotationAngle, rotationDamping * Time.deltaTime);
            currentHeight = Mathf.Lerp(currentHeight, wantedHeight, heightDamping * Time.deltaTime);

            // Convert angle into a rotation
            Quaternion currentRotation = Quaternion.Euler(0, currentRotationAngle, 0);

            // Set the position of the camera on the x-z plane to distance meters behind the target
            Vector3 pos = target.position - (currentRotation * Vector3.forward * distance);
            pos.y = currentHeight;
            transform.position = pos;

            // Look forward towards a point ahead of the bicycle
            Vector3 lookTarget = target.position + (target.forward * lookAheadDistance) + (Vector3.up * lookAtHeightOffset);
            transform.LookAt(lookTarget);
        }

        private void UpdateFirstPerson()
        {
            Vector3 targetPos = target.position + target.TransformDirection(firstPersonOffset);
            transform.position = Vector3.Lerp(transform.position, targetPos, 25f * Time.deltaTime);

            // Look forward along bicycle orientation
            Vector3 lookTarget = targetPos + target.forward * 10f;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(target.forward, Vector3.up), 20f * Time.deltaTime);
        }

        public void SnapToTarget()
        {
            if (target == null) return;

            if (isFirstPerson)
            {
                transform.position = target.position + target.TransformDirection(firstPersonOffset);
                transform.rotation = target.rotation;
            }
            else
            {
                transform.position = target.position - (target.forward * distance) + (Vector3.up * height);
                Vector3 lookTarget = target.position + (target.forward * lookAheadDistance) + (Vector3.up * lookAtHeightOffset);
                transform.LookAt(lookTarget);
            }
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            SnapToTarget();
        }
    }
}
