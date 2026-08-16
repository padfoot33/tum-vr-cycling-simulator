//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright © 2014 - 2023 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;

public class RCCP_EditorWindows : Editor {

    #region Edit Settings
#if RCCP_SHORTCUTS
    [MenuItem("Tools/BoneCracker Games/Realistic Car Controller Pro/Edit RCCP Settings #s", false, -100)]
    public static void OpenRCCSettings() {
        Selection.activeObject = RCCP_Settings.Instance;
    }
#else
    [MenuItem("Tools/BoneCracker Games/Realistic Car Controller Pro/Edit RCCP Settings", false, -100)]
    public static void OpenRCCSettings() {
        Selection.activeObject = RCCP_Settings.Instance;
    }
#endif
    #endregion

    #region Configure
    [MenuItem("Tools/BoneCracker Games/Realistic Car Controller Pro/Configure Demo Vehicles", false, -65)]
    public static void OpenDemoVehiclesSettings() {
        Selection.activeObject = RCCP_DemoVehicles.Instance;
    }

#if RCCP_PHOTON && PHOTON_UNITY_NETWORKING
    [MenuItem("Tools/BoneCracker Games/Realistic Car Controller Pro/Configure Photon Demo Vehicles", false, -65)]
    public static void OpenPhotonDemoVehiclesSettings() {
        Selection.activeObject = RCCP_DemoVehicles_Photon.Instance;
    }
#endif

    [MenuItem("Tools/BoneCracker Games/Realistic Car Controller Pro/Configure Ground Materials", false, -65)]
    public static void OpenGroundMaterialsSettings() {
        Selection.activeObject = RCCP_GroundMaterials.Instance;
    }

    [MenuItem("Tools/BoneCracker Games/Realistic Car Controller Pro/Configure Changable Wheels", false, -65)]
    public static void OpenChangableWheelSettings() {
        Selection.activeObject = RCCP_ChangableWheels.Instance;
    }

    [MenuItem("Tools/BoneCracker Games/Realistic Car Controller Pro/Configure Recorded Clips", false, -65)]
    public static void OpenRecordSettings() {
        Selection.activeObject = RCCP_Records.Instance;
    }
    #endregion

    #region Managers

    [MenuItem("Tools/BoneCracker Games/Realistic Car Controller Pro/Create/Managers/Add RCCP Scene Manager To Scene", false, -50)]
    public static void CreateRCCPSceneManager() {
        Selection.activeObject = RCCP_SceneManager.Instance;
    }

    [MenuItem("GameObject/BoneCracker Games/Realistic Car Controller Pro/Create/Managers/Add RCCP Scene Manager To Scene", false, -50)]
    public static void CreateRCCPSceneManager2() {
        Selection.activeObject = RCCP_SceneManager.Instance;
    }

    [MenuItem("Tools/BoneCracker Games/Realistic Car Controller Pro/Create/Managers/Add RCCP Skidmarks Manager To Scene", false, -50)]
    public static void CreateRCCPSkidmarksManager() {
        Selection.activeObject = RCCP_SkidmarksManager.Instance;
    }

    [MenuItem("GameObject/BoneCracker Games/Realistic Car Controller Pro/Create/Managers/Add RCCP Skidmarks Manager To Scene", false, -50)]
    public static void CreateRCCPSkidmarksManager2() {
        Selection.activeObject = RCCP_SkidmarksManager.Instance;
    }

    [MenuItem("Tools/BoneCracker Games/Realistic Car Controller Pro/Create/Managers/Add RCCP Customization Manager To Scene", false, -50)]
    public static void CreateRCCPCustomizationManager() {
        Selection.activeObject = RCCP_CustomizationManager.Instance;
    }

    [MenuItem("GameObject/BoneCracker Games/Realistic Car Controller Pro/Create/Managers/Add RCCP Customization Manager To Scene", false, -50)]
    public static void CreateRCCPCustomizationManager2() {
        Selection.activeObject = RCCP_CustomizationManager.Instance;
    }

    #endregion

    [MenuItem("Tools/BoneCracker Games/Realistic Car Controller Pro/Create/Scene/Add RCCP Camera To Scene", false, -50)]
    public static void CreateRCCCamera() {

        if (FindObjectOfType<RCCP_Camera>(true)) {

            EditorUtility.DisplayDialog("Scene has RCCP Camera already!", "Scene has RCCP Camera already!", "Close");
            Selection.activeGameObject = FindObjectOfType<RCCP_Camera>().gameObject;

        } else {

            GameObject cam = Instantiate(RCCP_Settings.Instance.RCCPMainCamera.gameObject);
            cam.name = RCCP_Settings.Instance.RCCPMainCamera.name;
            Selection.activeGameObject = cam.gameObject;

        }

    }

    [MenuItem("GameObject/BoneCracker Games/Realistic Car Controller Pro/Create/Scene/Add RCCP Camera To Scene", false, -50)]
    public static void CreateRCCCamera2() {

        if (FindObjectOfType<RCCP_Camera>(true)) {

            EditorUtility.DisplayDialog("Scene has RCCP Camera already!", "Scene has RCCP Camera already!", "Close");
            Selection.activeGameObject = FindObjectOfType<RCCP_Camera>().gameObject;

        } else {

            GameObject cam = Instantiate(RCCP_Settings.Instance.RCCPMainCamera.gameObject);
            cam.name = RCCP_Settings.Instance.RCCPMainCamera.name;
            Selection.activeGameObject = cam.gameObject;

        }

    }

    [MenuItem("Tools/BoneCracker Games/Realistic Car Controller Pro/Create/Scene/Add RCCP UI Canvas To Scene", false, -50)]
    public static void CreateRCCUICanvas() {

        if (FindObjectOfType<RCCP_UIManager>(true)) {

            EditorUtility.DisplayDialog("Scene has RCCP UI Canvas already!", "Scene has RCCP UI Canvas already!", "Close");
            Selection.activeGameObject = FindObjectOfType<RCCP_UIManager>(true).gameObject;

        } else {

            GameObject cam = Instantiate(RCCP_Settings.Instance.RCCPCanvas.gameObject);
            cam.name = RCCP_Settings.Instance.RCCPCanvas.name;
            Selection.activeGameObject = cam.gameObject;

        }

    }

    [MenuItem("GameObject/BoneCracker Games/Realistic Car Controller Pro/Create/Scene/Add RCCP UI Canvas To Scene", false, -50)]
    public static void CreateRCCUICanvas2() {

        if (FindObjectOfType<RCCP_UIManager>(true)) {

            EditorUtility.DisplayDialog("Scene has RCCP UI Canvas already!", "Scene has RCCP UI Canvas already!", "Close");
            Selection.activeGameObject = FindObjectOfType<RCCP_UIManager>(true).gameObject;

        } else {

            GameObject cam = Instantiate(RCCP_Settings.Instance.RCCPCanvas.gameObject);
            cam.name = RCCP_Settings.Instance.RCCPCanvas.name;
            Selection.activeGameObject = cam.gameObject;

        }

    }

    [MenuItem("Tools/BoneCracker Games/Realistic Car Controller Pro/Create/Scene/Add RCCP UI Customization Canvas To Scene", false, -50)]
    public static void CreateRCCCustomizationUICanvas() {

        if (FindObjectOfType<RCCP_UI_Canvas_Customization>(true)) {

            EditorUtility.DisplayDialog("Scene has RCCP UI Customization Canvas already!", "Scene has RCCP UI Customization Canvas already!", "Close");
            Selection.activeGameObject = FindObjectOfType<RCCP_UI_Canvas_Customization>(true).gameObject;

        } else {

            GameObject cam = Instantiate(RCCP_Settings.Instance.RCCPCustomizationCanvas.gameObject);
            cam.name = RCCP_Settings.Instance.RCCPCustomizationCanvas.name;
            Selection.activeGameObject = cam.gameObject;

        }

    }

    [MenuItem("GameObject/BoneCracker Games/Realistic Car Controller Pro/Create/Scene/Add RCCP UI Customization Canvas To Scene", false, -50)]
    public static void CreateRCCCustomizationUICanvas2() {

        if (FindObjectOfType<RCCP_UI_Canvas_Customization>(true)) {

            EditorUtility.DisplayDialog("Scene has RCCP UI Customization Canvas already!", "Scene has RCCP UI Customization Canvas already!", "Close");
            Selection.activeGameObject = FindObjectOfType<RCCP_UI_Canvas_Customization>(true).gameObject;

        } else {

            GameObject cam = Instantiate(RCCP_Settings.Instance.RCCPCustomizationCanvas.gameObject);
            cam.name = RCCP_Settings.Instance.RCCPCustomizationCanvas.name;
            Selection.activeGameObject = cam.gameObject;

        }

    }

    [MenuItem("Tools/BoneCracker Games/Realistic Car Controller Pro/Create/Scene/Add AI Waypoints Container To Scene", false, -50)]
    public static void CreateRCCAIWaypointManager() {

        GameObject wpContainer = new GameObject("RCCP_AI_WaypointsContainer");
        wpContainer.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        wpContainer.AddComponent<RCCP_AIWaypointsContainer>();
        Selection.activeGameObject = wpContainer;

    }

    [MenuItem("GameObject/BoneCracker Games/Realistic Car Controller Pro/Create/Scene/Add AI Waypoints Container To Scene", false, -50)]
    public static void CreateRCCAIWaypointManager2() {

        GameObject wpContainer = new GameObject("RCCP_AI_WaypointsContainer");
        wpContainer.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        wpContainer.AddComponent<RCCP_AIWaypointsContainer>();
        Selection.activeGameObject = wpContainer;

    }

    [MenuItem("Tools/BoneCracker Games/Realistic Car Controller Pro/Create/Scene/Add AI Brake Zones Container To Scene", false, -50)]
    public static void CreateRCCAIBrakeManager() {

        GameObject bzContainer = new GameObject("RCCP_AI_BrakeZonesContainer");
        bzContainer.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        bzContainer.AddComponent<RCCP_AIBrakeZonesContainer>();
        Selection.activeGameObject = bzContainer;

    }

    [MenuItem("GameObject/BoneCracker Games/Realistic Car Controller Pro/Create/Scene/Add AI Brake Zones Container To Scene", false, -50)]
    public static void CreateRCCAIBrakeManager2() {

        GameObject bzContainer = new GameObject("RCCP_AI_BrakeZonesContainer");
        bzContainer.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        bzContainer.AddComponent<RCCP_AIBrakeZonesContainer>();
        Selection.activeGameObject = bzContainer;

    }

    [MenuItem("Tools/BoneCracker Games/Realistic Car Controller Pro/Convert All Materials To URP", false, 0)]
    public static void URP() {

        EditorUtility.DisplayDialog("Converting All Demo Materials To URP", "All demo materials will be selected in your project now. After that, you'll need to convert them to URP shaders while they have been selected. You can convert them from the Edit --> Render Pipeline --> Universal Render Pipeline --> Convert Selected Materials.", "Close");

        UnityEngine.Object[] objects = new UnityEngine.Object[RCCP_DemoMaterials.Instance.demoMaterials.Length];

        for (int i = 0; i < objects.Length; i++) {
            objects[i] = RCCP_DemoMaterials.Instance.demoMaterials[i];
        }

        Selection.objects = objects;

    }

    #region Help
    [MenuItem("Tools/BoneCracker Games/Realistic Car Controller Pro/Help", false, 0)]
    public static void Help() {

        EditorUtility.DisplayDialog("Contact", "Please include your invoice number while sending a contact form.", "Close");

        string url = "http://www.bonecrackergames.com/contact/";
        Application.OpenURL(url);

    }
    #endregion Help

    //    #region Logitech
    //#if RCC_LOGITECH
    //	[MenuItem("Tools/BoneCracker Games/Realistic Car Controller/Create/Logitech/Logitech Manager", false, -50)]
    //	public static void CreateLogitech() {

    //		RCC_LogitechSteeringWheel logi = RCC_LogitechSteeringWheel.Instance;
    //		Selection.activeGameObject = logi.gameObject;

    //	}
    //#endif
    //    #endregion

    [MenuItem("Tools/BoneCracker Games/Realistic Car Controller Pro/Export Project Settings", false, 10)]
    public static void ExportProjectSettings() {

        string[] projectContent = new string[] { "ProjectSettings/InputManager.asset" };
        AssetDatabase.ExportPackage(projectContent, "RCCP_ProjectSettings.unitypackage", ExportPackageOptions.Interactive | ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies);
        Debug.Log("Project Exported");

    }

}
