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
/// Customization demo used in the demo scene. Enables disables cameras and canvases.
/// </summary>
[AddComponentMenu("BoneCracker Games/Realistic Car Controller Pro/Customization/RCCP Customization Demo")]
public class RCCP_CustomizationDemo : MonoBehaviour {

    private static RCCP_CustomizationDemo instance;
    public static RCCP_CustomizationDemo Instance {

        get {

            if (instance == null)
                instance = FindObjectOfType<RCCP_CustomizationDemo>();

            return instance;

        }

    }

    private RCCP_CarController vehicle;
    public RCCP_ShowroomCamera showroomCamera;
    public RCCP_Camera RCCCamera;
    public GameObject RCCCanvas;
    public GameObject modificationCanvas;
    public Transform location;

    public void EnableCustomization(RCCP_CarController carController) {

        vehicle = carController;
        RCCCamera.gameObject.SetActive(false);
        showroomCamera.gameObject.SetActive(true);
        RCCCanvas.SetActive(false);
        modificationCanvas.SetActive(true);
        RCCP.Transport(vehicle, location.position, location.rotation);
        RCCP.SetControl(vehicle, false);

    }

    public void DisableCustomization() {

        RCCCamera.gameObject.SetActive(true);
        showroomCamera.gameObject.SetActive(false);
        RCCCanvas.SetActive(true);
        modificationCanvas.SetActive(false);
        RCCP.SetControl(vehicle, true);
        vehicle = null;

    }

}
