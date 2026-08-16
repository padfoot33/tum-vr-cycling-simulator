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
/// Upgradable paint.
/// </summary>
[AddComponentMenu("BoneCracker Games/Realistic Car Controller Pro/Customization/RCCP Vehicle Upgrade Paint")]
public class RCCP_VehicleUpgrade_Paint : MonoBehaviour {

    private RCCP_CustomizationApplier modApplier;
    public RCCP_CustomizationApplier ModApplier {

        get {

            if (modApplier == null)
                modApplier = GetComponentInParent<RCCP_CustomizationApplier>();

            return modApplier;

        }

    }

    public MeshRenderer bodyRenderer;       //  Target renderer for painting.
    public string id = "_Color";
    public int index = 0;       //  Index of the target material.

    /// <summary>
    /// Paint the material with target color and save it.
    /// </summary>
    /// <param name="newColor"></param>
    public void UpdatePaint(Color newColor) {

        if (!bodyRenderer) {

            Debug.LogError("Body renderer is not selected for this painter!");
            return;

        }

        bodyRenderer.materials[index].SetColor(id, newColor);
        ModApplier.loadout.paint = newColor;
        ModApplier.SaveLoadout();

    }

}
