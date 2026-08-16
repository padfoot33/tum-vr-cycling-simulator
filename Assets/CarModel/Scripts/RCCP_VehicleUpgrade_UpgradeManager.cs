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
/// Manager for all upgradable scripts (Engine, Brake, Handling).
/// </summary>
[AddComponentMenu("BoneCracker Games/Realistic Car Controller Pro/Customization/RCCP Vehicle Upgrade Upgrade Manager")]
public class RCCP_VehicleUpgrade_UpgradeManager : MonoBehaviour {

    //  Mod applier.
    private RCCP_CustomizationApplier modApplier;
    public RCCP_CustomizationApplier ModApplier {

        get {

            if (modApplier == null)
                modApplier = GetComponentInParent<RCCP_CustomizationApplier>();

            return modApplier;

        }

    }

    private RCCP_VehicleUpgrade_Engine engine;        //  Upgradable engine component.
    private RCCP_VehicleUpgrade_Brake brake;      //  Upgradable brake component.
    private RCCP_VehicleUpgrade_Handling handling;        //  Upgradable handling component.

    internal int engineLevel = 0;       //  Current upgraded engine level.
    internal int brakeLevel = 0;        //  Current upgraded brake level.
    internal int handlingLevel = 0;     //  Current upgraded handling level.

    private void Awake() {

        //  Getting engine, brake, and handling upgrade components.
        engine = GetComponentInChildren<RCCP_VehicleUpgrade_Engine>();
        brake = GetComponentInChildren<RCCP_VehicleUpgrade_Brake>();
        handling = GetComponentInChildren<RCCP_VehicleUpgrade_Handling>();

        //  Getting defalut values of the car controller.
        engine.defEngine = ModApplier.CarController.Engine.maximumTorqueAsNM;
        brake.defBrake = ModApplier.CarController.FrontAxle.maxBrakeTorque;
        handling.defHandling = ModApplier.CarController.Stability.tractionHelperStrength;

    }

    public void Initialize() {

        //  If one of them is missing, return.
        if (!engine || !brake || !handling)
            return;

        //  Setting upgraded engine torque if saved.
        engine.EngineLevel = ModApplier.loadout.engineLevel;
        engine.Initialize();

        //  Setting upgraded brake torque if saved.
        brake.BrakeLevel = ModApplier.loadout.brakeLevel;
        brake.Initialize();

        //  Setting upgraded handling strength if saved.
        handling.HandlingLevel = ModApplier.loadout.handlingLevel;
        handling.Initialize();

    }

    private void Update() {

        //  If one of them is missing, return.
        if (!engine || !brake || !handling)
            return;

        //  Getting current upgrade levels
        engineLevel = engine.EngineLevel;
        brakeLevel = brake.BrakeLevel;
        handlingLevel = handling.HandlingLevel;

    }

    /// <summary>
    /// Upgrades the engine torque.
    /// </summary>
    public void UpgradeEngine() {

        engine.EngineLevel++;
        engine.UpdateStats();

    }

    /// <summary>
    /// Upgrades the brake torque.
    /// </summary>
    public void UpgradeBrake() {

        brake.BrakeLevel++;
        brake.UpdateStats();

    }

    /// <summary>
    /// Upgrades the traction helper (Handling).
    /// </summary>
    public void UpgradeHandling() {

        handling.HandlingLevel++;
        handling.UpdateStats();

    }

    private void Reset() {

        if (transform.Find("Engine")) {

            engine = transform.Find("Engine").gameObject.GetComponent<RCCP_VehicleUpgrade_Engine>();

        } else {

            GameObject newEngine = new GameObject("Engine");
            newEngine.transform.SetParent(transform);
            newEngine.transform.localPosition = Vector3.zero;
            newEngine.transform.localRotation = Quaternion.identity;
            engine = newEngine.AddComponent<RCCP_VehicleUpgrade_Engine>();

        }

        if (transform.Find("Brake")) {

            brake = transform.Find("Brake").gameObject.GetComponent<RCCP_VehicleUpgrade_Brake>();

        } else {

            GameObject newBrake = new GameObject("Brake");
            newBrake.transform.SetParent(transform);
            newBrake.transform.localPosition = Vector3.zero;
            newBrake.transform.localRotation = Quaternion.identity;
            brake = newBrake.AddComponent<RCCP_VehicleUpgrade_Brake>();

        }

        if (transform.Find("Handling")) {

            handling = transform.Find("Handling").gameObject.GetComponent<RCCP_VehicleUpgrade_Handling>();

        } else {

            GameObject newHandling = new GameObject("Handling");
            newHandling.transform.SetParent(transform);
            newHandling.transform.localPosition = Vector3.zero;
            newHandling.transform.localRotation = Quaternion.identity;
            handling = newHandling.AddComponent<RCCP_VehicleUpgrade_Handling>();

        }

    }

}
