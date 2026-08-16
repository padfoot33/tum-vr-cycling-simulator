//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright © 2014 - 2023 BoneCracker Games
// https://www.bonecrackergames.com
// Buğra Özdoğanlar
//
//----------------------------------------------

using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// UI change wheel button.
/// </summary>
[AddComponentMenu("BoneCracker Games/Realistic Car Controller Pro/UI/Modification/RCCP UI Wheel Button")]
public class RCCP_UI_Wheel : MonoBehaviour {

    public int wheelIndex = 0;

    public void OnClick() {

        RCCP_CustomizationManager handler = RCCP_CustomizationManager.Instance;
        handler.ChangeWheels(wheelIndex);

    }

}
