//----------------------------------------------
//         Realistic Car Controller Pro
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
/// Used to slow down the vehicle by increasing drag.
/// </summary>
[AddComponentMenu("BoneCracker Games/Realistic Car Controller Pro/Misc/RCCP Speed Limiter")]
public class RCCP_SpeedLimiter : MonoBehaviour {

    private float defaultDrag = -1f;

    private void OnTriggerStay(Collider other) {

        RCCP_CarController carController = other.GetComponentInParent<RCCP_CarController>();

        if (!carController)
            return;

        if (defaultDrag == -1)
            defaultDrag = carController.Rigid.linearDamping;

        carController.Rigid.linearDamping = .02f * carController.speed;

    }

    private void OnTriggerExit(Collider other) {

        RCCP_CarController carController = other.GetComponentInParent<RCCP_CarController>();

        if (!carController)
            return;

        carController.Rigid.linearDamping = defaultDrag;

    }

}
