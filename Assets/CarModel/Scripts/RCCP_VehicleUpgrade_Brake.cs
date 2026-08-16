//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright © 2014 - 2023 BoneCracker Games
// https://www.bonecrackergames.com
// Buğra Özdoğanlar
//
//----------------------------------------------

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Upgrades brake torque of the car controller.
/// </summary>
[AddComponentMenu("BoneCracker Games/Realistic Car Controller Pro/Customization/RCCP Vehicle Upgrade Brake")]
public class RCCP_VehicleUpgrade_Brake : MonoBehaviour {

    private RCCP_CarController _carController;
    public RCCP_CarController CarController {

        get {

            if (_carController == null)
                _carController = GetComponentInParent<RCCP_CarController>();

            return _carController;

        }

    }

    private RCCP_CustomizationApplier modApplier;
    public RCCP_CustomizationApplier ModApplier {

        get {

            if (modApplier == null)
                modApplier = GetComponentInParent<RCCP_CustomizationApplier>();

            return modApplier;

        }

    }

    private int _brakeLevel = 0;
    public int BrakeLevel {
        get {
            return _brakeLevel;
        }
        set {
            if (value <= 5)
                _brakeLevel = value;
        }
    }

    [HideInInspector] public float defBrake = 0f;
    [Range(2000, 10000)] public float maxBrake = 6000f;

    /// <summary>
    /// Updates brake torque and initializes it.
    /// </summary>
    public void Initialize() {

        CarController.FrontAxle.maxBrakeTorque = Mathf.Lerp(defBrake, maxBrake, BrakeLevel / 5f);

    }

    /// <summary>
    /// Updates brake torque and save it.
    /// </summary>
    public void UpdateStats() {

        CarController.FrontAxle.maxBrakeTorque = Mathf.Lerp(defBrake, maxBrake, BrakeLevel / 5f);
        ModApplier.loadout.brakeLevel = BrakeLevel;
        ModApplier.SaveLoadout();

    }

    private void Update() {

        //  Make sure max brake is not smaller.
        if (maxBrake < CarController.FrontAxle.maxBrakeTorque)
            maxBrake = CarController.FrontAxle.maxBrakeTorque;

    }

}
