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
using UnityEditor;

[CustomEditor(typeof(RCCP_VehicleUpgrade_PaintManager))]
public class RCCP_VehicleUpgrade_PaintEditor : Editor {

    RCCP_VehicleUpgrade_PaintManager prop;

    public override void OnInspectorGUI() {

        prop = (RCCP_VehicleUpgrade_PaintManager)target;
        serializedObject.Update();

        EditorGUILayout.HelpBox("All painters have target renderers and material index. If your vehicle has multiple paintable renderers, create new painter for each renderer and set their target material indexes. Click 'Get All Paints' after editing painters.", MessageType.None);

        DrawDefaultInspector();

        if (GUILayout.Button("Get All Painters"))
            prop.paints = prop.GetComponentsInChildren<RCCP_VehicleUpgrade_Paint>(true);

        if (GUILayout.Button("Create New Painter")) {

            GameObject newPainter = new GameObject("Painter");
            newPainter.transform.SetParent(prop.transform);
            newPainter.transform.localPosition = Vector3.zero;
            newPainter.transform.localRotation = Quaternion.identity;
            newPainter.AddComponent<RCCP_VehicleUpgrade_Paint>();

        }

        serializedObject.ApplyModifiedProperties();

    }

}
