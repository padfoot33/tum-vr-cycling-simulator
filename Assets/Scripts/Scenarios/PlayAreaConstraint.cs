using UnityEngine;
using ExperimentRefs = CyclingExperiment.ExperimentSceneRefs;

namespace CyclingExperiment.Scenarios
{
    /// <summary>
    /// After bike physics, keep the kinematic rider inside the active route's play-area boxes.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class PlayAreaConstraint : MonoBehaviour
    {
        [SerializeField] private ExperimentRefs sceneRefs;

        private PlayAreaBounds _active;

        public void Bind(ExperimentRefs refs)
        {
            sceneRefs = refs;
        }

        public void SetActiveArea(PlayAreaBounds area)
        {
            _active = area;
        }

        private void Awake()
        {
            if (sceneRefs == null) sceneRefs = ExperimentRefs.Instance;
        }

        private void FixedUpdate()
        {
            if (_active == null || !_active.isActiveAndEnabled) return;

            var refs = sceneRefs != null ? sceneRefs : ExperimentRefs.Instance;
            if (refs == null) return;

            Transform bike = refs.bicycleTransform;
            var physics = refs.bicyclePhysics;
            if (bike == null) return;

            Vector3 pos = bike.position;
            if (_active.Contains(pos)) return;

            Vector3 closest = _active.ClosestPointOnUnion(pos);
            closest.y = pos.y;
            if ((closest - pos).sqrMagnitude < 0.0004f) return;

            if (physics != null)
            {
                physics.StopLongitudinalSpeed();
                physics.SetWorldPositionKeepYaw(closest);
            }
            else
            {
                bike.position = closest;
            }
        }
    }
}
