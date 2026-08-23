using UnityEngine;

namespace CyclingExperiment
{
    /// <summary>
    /// Experiment-facing cyclist API. Keyboard and hardware bikes both implement this
    /// so scenarios, HUD, and play-area clamp do not depend on a specific physics type.
    /// </summary>
    public interface ICyclistMotion
    {
        Transform Transform { get; }
        float GetSpeedKph();
        float GetSpeedMps();
        float GetSteeringAngleDeg();
        float GetLeftBrake();
        float GetRightBrake();
        bool IsBrakeActive();
        float MaxSpeedMps { get; set; }
        void Teleport(Vector3 worldPosition, float yawDegrees);
        void SetWorldPositionKeepYaw(Vector3 worldPosition);
        void StopLongitudinalSpeed();
    }
}
