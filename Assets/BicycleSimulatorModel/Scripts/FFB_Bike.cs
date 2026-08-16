using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class FFBInspectorBike : MonoBehaviour {
	public InputActionAsset ControlScheme;																									// Input System control scheme
	InputActionMap Actions;

	public bool useCommandLineInput = false;
	public bool EnableFFB = false;
	public string FFBDeviceName = "Waiting for Play Mode";
	[Range(0,1)] public float FFBAxisValue = 0;

	[Header("Bicycle FFB Settings")]
	public float steeringFeedbackControlP;
	public float steeringFeedbackControlI;
	public float steeringFeedbackControlD;
	public bool EnableBicycleFeedback = true;
	public float targetPostionOffset = 0.0f;
	public float steeringInput = 0f;
	public float steeringInputCorrected = 0f;
	public int dynamicForceMagnitude = 0;
	public bool activateManualControl = false;
	public float manualControlValue = 0;

	[Header("FFB Constant Force")]
	public bool ConstantForceEnabled = false;
	[Range(-10000f, 10000f)] public int ConstantForceMagnitude;

	[Header("FFB Damper")]
	public bool DamperForceEnabled = false;
	[Range(-10000f, 10000f)] public int DamperMagnitude;

	[Header("FFB Friction")]
	public bool FrictionForceEnabled = false;
	[Range(-10000f, 10000f)] public int FrictionMagnitude;
	
	[Header("FFB Inertia")]
	public bool InertiaForceEnabled = false;
	[Range(-10000f, 10000f)] public int InertiaMagnitude;
	
	[Header("FFB Spring")]
	public bool SpringForceEnabled = false;
	[Range(0, 10000f)] public uint SpringDeadband;
	[Range(-10000f, 10000f)] public int SpringOffset;
	[Range(0, 100000f)] public int SpringCoefficient;
	[Range(0, 100000f)] public uint SpringSaturation;

	private bool toogleTest = false;
	private float errorSum = 0;
	private float lastError = 0;
	private int counter = 1;

	private float PIDController(float targetValue, float currentValue, float kp, float ki, float kd)
	{
		float error = targetValue - currentValue;
		errorSum += error;
		float errorDiff = error - lastError;
		lastError = error;

		float p = kp * error;
		float i = ki * errorSum;
		float d = kd * errorDiff;

		return p + i + d;
	}

	void OnEnable()
	{
		if (useCommandLineInput)
		{
			EnableBicycleFeedback = GetFFBUsageFromCommandLine();
		}
	}

	void Start() {
		if (ControlScheme != null)
		{
			Actions = ControlScheme.FindActionMap("DirectInputDemo");
			if (Actions != null) Actions.Enable();
		}
	}

	void Update() {
		// Pure Unity keyboard/mouse or standard input mode
	}

	bool GetFFBUsageFromCommandLine()
	{
		var args = Environment.GetCommandLineArgs();
		bool enableFFB = false;

		for (int i = 0; i < args.Length; i++)
		{
			if(args[i] == "--activeSteering" && i<args.Length-1)
			{
				int payload = int.Parse(args[i+1]);
				if (payload == 1)
				{
					enableFFB = true;
				}
			}
		}

		return enableFFB;
	}
}
