//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright © 2014 - 2023 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.Events;
using System;

[CustomEditor(typeof(RCCP_Limiter))]
public class RCCP_LimiterEditor : Editor {

    RCCP_Limiter prop;
    GUISkin skin;

    private void OnEnable() {

        skin = Resources.Load<GUISkin>("RCCP_Gui");

    }

    public override void OnInspectorGUI() {

        prop = (RCCP_Limiter)target;
        serializedObject.Update();
        GUI.skin = skin;

        EditorGUILayout.HelpBox("Limits the maximum speed of the vehicle per each gear. Be sure length of the float array is same with the length of the gearbox gears.", MessageType.Info, true);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("limitSpeedAtGear"), new GUIContent("Limit Speed At Gear", "Limits the speed of the vehicle at this gear."), true);

        GUI.enabled = false;
        EditorGUILayout.PropertyField(serializedObject.FindProperty("limitingNow"), new GUIContent("Limiting Now", "Speed of the vehicle exceeds the limit now?"), true);
        GUI.enabled = true;

        if (!EditorUtility.IsPersistent(prop)) {

            if (GUILayout.Button("Back"))
                Selection.activeObject = prop.GetComponentInParent<RCCP_OtherAddons>(true).gameObject;

        }

        prop.transform.localPosition = Vector3.zero;
        prop.transform.localRotation = Quaternion.identity;

        if (GUI.changed)
            EditorUtility.SetDirty(prop);

        serializedObject.ApplyModifiedProperties();

    }

}
