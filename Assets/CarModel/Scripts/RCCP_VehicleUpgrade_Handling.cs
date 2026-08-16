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
/// Upgrades traction strength of the car controller.
/// </summary>
[AddComponentMenu("BoneCracker Games/Realistic Car Controller Pro/Customization/RCCP Vehicle Upgrade Handling")]
public class RCCP_VehicleUpgrade_Handling : MonoBehaviour {

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

    private int _handlingLevel = 0;
    public int HandlingLevel {
        get {
            return _handlingLevel;
        }
        set {
            if (value <= 5)
                _handlingLevel = value;
        }
    }

    [HideInInspector] public float defHandling = 0f;
    [Range(.1f, .6f)] public float maxHandling = .4f;

    /// <summary>
    /// Updates handling and initializes it.
    /// </summary>
    public void Initialize() {

        CarController.Stability.tractionHelperStrength = Mathf.Lerp(defHandling, maxHandling, HandlingLevel / 5f);

    }

    /// <summary>
    /// Updates handling strength and save it.
    /// </summary>
    public void UpdateStats() {

        CarController.Stability.tractionHelperStrength = Mathf.Lerp(defHandling, maxHandling, HandlingLevel / 5f);
        ModApplier.loadout.handlingLevel = HandlingLevel;
        ModApplier.SaveLoadout();

    }

    private void Update() {

        //  Make sure max handling is not smaller.
        if (maxHandling < CarController.Stability.tractionHelperStrength)
            maxHandling = CarController.Stability.tractionHelperStrength;

    }

    private void Reset() {

        maxHandling = GetComponentInParent<RCCP_CarController>().GetComponentInChildren<RCCP_Stability>().tractionHelperStrength + .3f;

    }

}
