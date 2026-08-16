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
using UnityEngine.UI;

public class RCCP_UI_OverrideVehicleExample : MonoBehaviour {

    public RCCP_CarController targetVehicle;
    public bool takePlayerVehicle = true;
    public RCCP_Inputs newInputs = new RCCP_Inputs();

    private bool overrideNow = false;

    public Slider throttle;
    public Slider brake;
    public Slider steering;
    public Slider handbrake;
    public Slider nos;

    private void Update() {

        newInputs.throttleInput = throttle.value;
        newInputs.brakeInput = brake.value;
        newInputs.steerInput = steering.value;
        newInputs.handbrakeInput = handbrake.value;
        newInputs.nosInput = nos.value;

        if (takePlayerVehicle)
            targetVehicle = RCCP_SceneManager.Instance.activePlayerVehicle;

        if (targetVehicle && overrideNow)
            targetVehicle.Inputs.OverrideInputs(newInputs);

    }

    public void EnableOverride() {

        overrideNow = true;

        if (targetVehicle)
            targetVehicle.Inputs.OverrideInputs(newInputs);

    }

    public void DisableOverride() {

        overrideNow = false;

        if (targetVehicle)
            targetVehicle.Inputs.DisableOverrideInputs();

    }

}
