//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright © 2014 - 2023 BoneCracker Games
// https://www.bonecrackergames.com
// Buğra Özdoğanlar
//
//----------------------------------------------


using UnityEngine;
using System.Collections;
using System.Collections.Generic;


/// <summary>
/// Customization applier for vehicles. Needs to be attached to the vehicle.
/// 5 Upgrade managers for paints, wheels, upgrades, spoilers, and sirens.
/// </summary>
[RequireComponent(typeof(RCCP_CarController))]
[AddComponentMenu("BoneCracker Games/Realistic Car Controller Pro/Customization/RCCP Customization Applier")]
public class RCCP_CustomizationApplier : MonoBehaviour {

    //  Car controller.
    private RCCP_CarController _carController;
    public RCCP_CarController CarController {

        get {

            if (_carController == null)
                _carController = GetComponentInChildren<RCCP_CarController>();

            return _carController;

        }

    }

    #region All upgrade managers

    private RCCP_VehicleUpgrade_PaintManager _paintManager;
    public RCCP_VehicleUpgrade_PaintManager PaintManager {

        get {

            if (_paintManager == null)
                _paintManager = GetComponentInChildren<RCCP_VehicleUpgrade_PaintManager>();

            return _paintManager;

        }

    }

    private RCCP_VehicleUpgrade_WheelManager _wheelManager;
    public RCCP_VehicleUpgrade_WheelManager WheelManager {

        get {

            if (_wheelManager == null)
                _wheelManager = GetComponentInChildren<RCCP_VehicleUpgrade_WheelManager>();

            return _wheelManager;

        }

    }

    private RCCP_VehicleUpgrade_UpgradeManager _upgradeManager;
    public RCCP_VehicleUpgrade_UpgradeManager UpgradeManager {

        get {

            if (_upgradeManager == null)
                _upgradeManager = GetComponentInChildren<RCCP_VehicleUpgrade_UpgradeManager>();

            return _upgradeManager;

        }

    }

    private RCCP_VehicleUpgrade_SpoilerManager _spoilerManager;
    public RCCP_VehicleUpgrade_SpoilerManager SpoilerManager {

        get {

            if (_spoilerManager == null)
                _spoilerManager = GetComponentInChildren<RCCP_VehicleUpgrade_SpoilerManager>();

            return _spoilerManager;

        }

    }

    private RCCP_VehicleUpgrade_SirenManager _sirenManager;
    public RCCP_VehicleUpgrade_SirenManager SirenManager {

        get {

            if (_sirenManager == null)
                _sirenManager = GetComponentInChildren<RCCP_VehicleUpgrade_SirenManager>();

            return _sirenManager;

        }

    }

    #endregion

    public string saveFileName = "";        //  Save file name of the vehicle.
    public bool autoLoadLoadout = true;     //  Loads the latest loadout.
    public RCCP_CustomizationLoadout loadout = new RCCP_CustomizationLoadout();       //  Loadout class.

    private void OnEnable() {

        //  Loads the latest loadout.
        if (autoLoadLoadout)
            LoadLoadout();

        //  Initializes paint manager.
        if (PaintManager)
            PaintManager.Initialize();

        //  Initializes wheel manager.
        if (WheelManager)
            WheelManager.Initialize();

        //  Initializes upgrade manager.
        if (UpgradeManager)
            UpgradeManager.Initialize();

        //  Initializes spoiler manager.
        if (SpoilerManager)
            SpoilerManager.Initialize();

        //  Initializes siren manager.
        if (SirenManager)
            SirenManager.Initialize();

    }

    /// <summary>
    /// Saves the current loadout with Json.
    /// </summary>
    public void SaveLoadout() {

        PlayerPrefs.SetString(saveFileName, JsonUtility.ToJson(loadout));

    }

    /// <summary>
    /// Loads the latest saved loadout with Json.
    /// </summary>
    public void LoadLoadout() {

        loadout = new RCCP_CustomizationLoadout();

        if (PlayerPrefs.HasKey(saveFileName))
            loadout = (RCCP_CustomizationLoadout)JsonUtility.FromJson(PlayerPrefs.GetString(saveFileName), typeof(RCCP_CustomizationLoadout));

    }

    private void Reset() {

        saveFileName = transform.name;

    }

}
