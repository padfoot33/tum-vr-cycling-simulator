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
/// Upgrades engine of the car controller.
/// </summary>
[AddComponentMenu("BoneCracker Games/Realistic Car Controller Pro/Customization/RCCP Vehicle Upgrade Engine")]
public class RCCP_VehicleUpgrade_Engine : MonoBehaviour {

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

    private int _engineLevel = 0;
    public int EngineLevel {
        get {
            return _engineLevel;
        }
        set {
            if (value <= 5)
                _engineLevel = value;
        }
    }

    [HideInInspector] public float defEngine = 0f;
    [Range(200, 1000)] public float maxEngine = 750f;

    /// <summary>
    /// Updates engine torque and initializes it.
    /// </summary>
    public void Initialize() {

        CarController.Engine.maximumTorqueAsNM = Mathf.Lerp(defEngine, maxEngine, EngineLevel / 5f);

    }

    /// <summary>
    /// Updates engine torque and save it.
    /// </summary>
    public void UpdateStats() {

        CarController.Engine.maximumTorqueAsNM = Mathf.Lerp(defEngine, maxEngine, EngineLevel / 5f);
        ModApplier.loadout.engineLevel = EngineLevel;
        ModApplier.SaveLoadout();

    }

    private void Update() {

        //  Make sure max torque is not smaller.
        if (maxEngine < CarController.Engine.maximumTorqueAsNM)
            maxEngine = CarController.Engine.maximumTorqueAsNM;

    }

    private void Reset() {

        maxEngine = GetComponentInParent<RCCP_CarController>().GetComponentInChildren<RCCP_Engine>().maximumTorqueAsNM + 200f;

    }

}
