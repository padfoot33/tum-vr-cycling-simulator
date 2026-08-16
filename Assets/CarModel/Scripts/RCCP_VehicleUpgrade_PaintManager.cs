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
/// Manager for painters.
/// </summary>
[AddComponentMenu("BoneCracker Games/Realistic Car Controller Pro/Customization/RCCP Vehicle Upgrade Paint Manager")]
public class RCCP_VehicleUpgrade_PaintManager : MonoBehaviour {

    //  Mod applier.
    private RCCP_CustomizationApplier modApplier;
    public RCCP_CustomizationApplier ModApplier {

        get {

            if (modApplier == null)
                modApplier = GetComponentInParent<RCCP_CustomizationApplier>();

            return modApplier;

        }

    }

    public RCCP_VehicleUpgrade_Paint[] paints;       //  All painters.

    /// <summary>
    /// Initializes all painters.
    /// </summary>
    public void Initialize() {

        if (paints == null)
            return;

        //  Getting last saved color for this vehicle.
        if (ModApplier.loadout.paint != new Color(1f, 1f, 1f, 0f))
            Paint(ModApplier.loadout.paint);

    }

    /// <summary>
    /// Runs all painters with the target color.
    /// </summary>
    /// <param name="newColor"></param>
    public void Paint(Color newColor) {

        for (int i = 0; i < paints.Length; i++)
            paints[i].UpdatePaint(newColor);

    }

    private void Reset() {

        if (transform.Find("Paint_1")) {

            paints = new RCCP_VehicleUpgrade_Paint[1];
            paints[0] = transform.Find("Paint_1").gameObject.GetComponent<RCCP_VehicleUpgrade_Paint>();
            return;

        }

        paints = new RCCP_VehicleUpgrade_Paint[1];
        GameObject newPaint = new GameObject("Paint_1");
        newPaint.transform.SetParent(transform);
        newPaint.transform.localPosition = Vector3.zero;
        newPaint.transform.localRotation = Quaternion.identity;
        paints[0] = newPaint.AddComponent<RCCP_VehicleUpgrade_Paint>();

    }

}
