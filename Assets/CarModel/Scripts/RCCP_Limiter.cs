//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright © 2014 - 2023 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Limits the maximum speed of the vehicle per each gear.
/// </summary>
[AddComponentMenu("BoneCracker Games/Realistic Car Controller Pro/Other Addons/RCCP Limiter")]
public class RCCP_Limiter : MonoBehaviour {

    //  Main car controller.
    private RCCP_CarController _carController;
    private RCCP_CarController CarController {

        get {

            if (_carController == null)
                _carController = GetComponentInParent<RCCP_CarController>(true);

            return _carController;

        }

    }

    public float[] limitSpeedAtGear = new float[0];
    public bool limitingNow = false;

    private void Update() {

        if (!CarController.Gearbox)
            return;

        int currentGear = CarController.Gearbox.currentGear;

        if (Mathf.Abs(CarController.speed) > limitSpeedAtGear[currentGear])
            limitingNow = true;
        else
            limitingNow = false;

        CarController.Engine.cutFuel = limitingNow;

    }

    private void Reset() {

        limitSpeedAtGear = new float[GetComponentInParent<RCCP_CarController>().GetComponentInChildren<RCCP_Gearbox>().gearRatios.Length];

        for (int i = 0; i < limitSpeedAtGear.Length; i++) {

            limitSpeedAtGear[i] = 999f;

        }

    }

}
