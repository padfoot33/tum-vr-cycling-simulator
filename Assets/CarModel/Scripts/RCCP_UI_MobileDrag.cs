//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright © 2014 - 2023 BoneCracker Games
// https://www.bonecrackergames.com
// Buğra Özdoğanlar
//
//----------------------------------------------

#pragma warning disable 0414

using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;

/// <summary>
/// Mobile UI Drag used for orbiting Showroom Camera.
/// </summary>
public class RCCP_UI_MobileDrag : MonoBehaviour, IDragHandler, IEndDragHandler {

    private RCCP_ShowroomCamera showroomCamera;

    private void Awake() {

        showroomCamera = FindObjectOfType<RCCP_ShowroomCamera>(true);

    }

    public void OnDrag(PointerEventData data) {

        if (showroomCamera)
            showroomCamera.OnDrag(data);

    }

    public void OnEndDrag(PointerEventData data) {



    }

}
