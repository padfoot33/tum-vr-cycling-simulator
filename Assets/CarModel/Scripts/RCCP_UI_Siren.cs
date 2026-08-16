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

/// <summary>
/// UI siren button.
/// </summary>
[AddComponentMenu("BoneCracker Games/Realistic Car Controller Pro/UI/Modification/RCCP UI Siren Button")]
public class RCCP_UI_Siren : MonoBehaviour {

    public int index = 0;

    public void Upgrade() {

        RCCP_CustomizationManager handler = RCCP_CustomizationManager.Instance;
        handler.Siren(index);

    }

}
