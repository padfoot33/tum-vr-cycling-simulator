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
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// UI paint button. 
/// </summary>
[AddComponentMenu("BoneCracker Games/Realistic Car Controller Pro/UI/Modification/RCCP UI Color Button")]
public class RCCP_UI_Color : MonoBehaviour {

    public PickedColor _pickedColor = PickedColor.Orange;
    public enum PickedColor { Orange, Red, Green, Blue, Black, White, Cyan, Magenta, Pink }

    public void OnClick() {

        RCCP_CustomizationManager handler = RCCP_CustomizationManager.Instance;
        Color selectedColor = new Color();

        switch (_pickedColor) {

            case PickedColor.Orange:
                selectedColor = Color.red + (Color.green / 2f);
                break;

            case PickedColor.Red:
                selectedColor = Color.red;
                break;

            case PickedColor.Green:
                selectedColor = Color.green;
                break;

            case PickedColor.Blue:
                selectedColor = Color.blue;
                break;

            case PickedColor.Black:
                selectedColor = Color.black;
                break;

            case PickedColor.White:
                selectedColor = Color.white;
                break;

            case PickedColor.Cyan:
                selectedColor = Color.cyan;
                break;

            case PickedColor.Magenta:
                selectedColor = Color.magenta;
                break;

            case PickedColor.Pink:
                selectedColor = new Color(1, 0f, .5f);
                break;

        }

        handler.Paint(selectedColor);

    }

}
