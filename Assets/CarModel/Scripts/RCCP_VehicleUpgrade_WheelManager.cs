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
/// Manager for upgradable wheels.
/// </summary>
[AddComponentMenu("BoneCracker Games/Realistic Car Controller Pro/Customization/RCCP Vehicle Upgrade Wheel Manager")]
public class RCCP_VehicleUpgrade_WheelManager : MonoBehaviour {

    //  Mod applier.
    private RCCP_CustomizationApplier modApplier;
    public RCCP_CustomizationApplier ModApplier {

        get {

            if (modApplier == null)
                modApplier = GetComponentInParent<RCCP_CustomizationApplier>();

            return modApplier;

        }

    }

    /// <summary>
    /// Initializing.
    /// </summary>
    public void Initialize() {

        // If last selected wheel found, change the wheels.
        int wheelIndex = ModApplier.loadout.wheel;

        if (wheelIndex != -1)
            RCCP_Customization.ChangeWheels(ModApplier.CarController, RCCP_ChangableWheels.Instance.wheels[wheelIndex].wheel, true);

    }

    /// <summary>
    /// Changes the wheel with target wheel index.
    /// </summary>
    /// <param name="wheelIndex"></param>
    public void UpdateWheel(int wheelIndex) {

        ModApplier.loadout.wheel = wheelIndex;
        ModApplier.SaveLoadout();
        RCCP_Customization.ChangeWheels(ModApplier.CarController, RCCP_ChangableWheels.Instance.wheels[wheelIndex].wheel, true);

    }

}
