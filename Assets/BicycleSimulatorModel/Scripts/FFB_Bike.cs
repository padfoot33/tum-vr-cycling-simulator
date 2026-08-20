using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using System;

#if ENABLE_DIRECTINPUT
using DirectInputManager;
#endif

public class FFBInspectorBike : MonoBehaviour {
	public InputActionAsset ControlScheme;
	InputActionMap Actions;

#if ENABLE_DIRECTINPUT
	public DirectInputDevice ISDevice;
#endif

	public bool useCommandLineInput = false;
	public bool EnableFFB = true;
	public string FFBDeviceName = "Waiting for Play Mode";
	[Range(0,1)] public float FFBAxisValue = 0;

	[Header("Bicycle FFB Settings")]
	public float steeringFeedbackControlP;
	public float steeringFeedbackControlI;
	public float steeringFeedbackControlD;
	public bool EnableBicycleFeedback = true;
	public float targetPostionOffset = 0.0f;
    public float steeringInput;
	public float steeringInputCorrected;
	public bool HasSteeringDevice
	{
		get
		{
#if ENABLE_DIRECTINPUT
			return ISDevice != null;
#else
			return false;
#endif
		}
	}
	public int dynamicForceMagnitude;
	public bool activateManualControl = false;
	public float manualControlValue = 0;


	[Header("FFB Constant Force")]
	public bool ConstantForceEnabled = false;
	[Range(-10000f, 10000f)]public int ConstantForceMagnitude;

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
		if (ControlScheme == null) return;
		Actions = ControlScheme.FindActionMap("DirectInputDemo");
		if (Actions != null) Actions.Enable();
	}

	void Update(){
#if ENABLE_DIRECTINPUT
		if(!EnableFFB){ return; }
		if (ISDevice == null) {
			if (Actions == null) return;
			FFBDeviceName = "Waiting for Steering Device";
			var ffbAxis = Actions.FindAction("FFBAxis");
			if (ffbAxis == null) return;
			ISDevice = ffbAxis.controls
				.Select(x => x.device)
				.OfType<DirectInputDevice>()
				.Where(d => d.description.capabilities.Contains("\"FFBCapable\":true"))
				.Where(d => DIManager.Attach(d.description.serial))
				.FirstOrDefault();
			if (ISDevice == null) { return; }
			
			FFBDeviceName = ISDevice.name + " : " + ISDevice.description.serial;
			Debug.Log($"FFB Device: {ISDevice.description.serial}, Acquired: {DIManager.Attach(ISDevice.description.serial)}");

			DIManager.EnableFFBEffect(ISDevice.description.serial, FFBEffects.ConstantForce);
		}

		if (ISDevice is not null) {
			FFBAxisValue = Actions.FindAction("FFBAxis").ReadValue<float>();
		    steeringInput = ((float)FFBAxisValue-0.5f)*2.0f;
			steeringInputCorrected = steeringInput + targetPostionOffset;
			if (steeringInput == -1)
			{
				steeringInput = 0;
			}

			if (EnableBicycleFeedback){
				float targetPosition = 0.0f;


				bool controlTuning = false;
				if(controlTuning){
					if (counter%200==0)
					{
						toogleTest = !toogleTest;
					}
					
					if (toogleTest)
					{
						targetPosition = 0.15f;
					}
					else
					{
						targetPosition = 0.0f;
					}
				}


				if (activateManualControl)
				{
					targetPosition = manualControlValue;
				}

				targetPosition = Mathf.Clamp(targetPosition, -0.12f, 0.12f);


				double tanhInput = (double)(steeringInputCorrected*steeringFeedbackControlP);

				dynamicForceMagnitude = (int)(Math.Tanh(tanhInput) * 10000);
						
				if (Mathf.Abs(steeringInputCorrected) > 0.12f)
				{
					dynamicForceMagnitude = 0;
				}
				DIManager.UpdateConstantForceSimple(ISDevice.description.serial, dynamicForceMagnitude);

			}
			if (ConstantForceEnabled) { DIManager.UpdateConstantForceSimple(ISDevice.description.serial, ConstantForceMagnitude); }
			if (DamperForceEnabled) 	{ DIManager.UpdateDamperSimple(ISDevice.description.serial, DamperMagnitude); }
			if (FrictionForceEnabled) { DIManager.UpdateFrictionSimple(ISDevice.description.serial, FrictionMagnitude); }
			if (InertiaForceEnabled) 	{ DIManager.UpdateInertiaSimple(ISDevice.description.serial, InertiaMagnitude); }
			if (SpringForceEnabled) 	{ DIManager.UpdateSpringSimple(ISDevice.description.serial, SpringDeadband, SpringOffset, SpringCoefficient, SpringCoefficient, SpringSaturation, SpringSaturation); }
		}
		counter++;
#endif
	}

#if ENABLE_DIRECTINPUT
	void OnApplicationQuit(){
		try
		{
			if (ISDevice != null && ISDevice.description != null && !string.IsNullOrEmpty(ISDevice.description.serial))
			{
				try
				{
					DIManager.UpdateConstantForceSimple(ISDevice.description.serial, 0);
				}
				catch (Exception ex)
				{
					Debug.LogWarning($"FFBInspectorBike: Error during OnApplicationQuit: {ex.Message}");
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"FFBInspectorBike: Unexpected error in OnApplicationQuit: {ex.Message}");
		}
	}

	void OnApplicationPause()
	{
		try
		{
			if (ISDevice != null && ISDevice.description != null && !string.IsNullOrEmpty(ISDevice.description.serial))
			{
				try
				{
					DIManager.UpdateConstantForceSimple(ISDevice.description.serial, 0);
				}
				catch (Exception ex)
				{
					Debug.LogWarning($"FFBInspectorBike: Error during OnApplicationPause: {ex.Message}");
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"FFBInspectorBike: Unexpected error in OnApplicationPause: {ex.Message}");
		}
	}

	void OnDestroy(){
		try
		{
			if(ISDevice != null && ISDevice.description != null && !string.IsNullOrEmpty(ISDevice.description.serial))
			{
				try
				{
					DIManager.UpdateConstantForceSimple(ISDevice.description.serial, 0);
					DIManager.StopAllFFBEffects(ISDevice.description.serial);
				}
				catch (Exception ex)
				{
					Debug.LogWarning($"FFBInspectorBike: Error during OnDestroy: {ex.Message}");
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"FFBInspectorBike: Unexpected error in OnDestroy: {ex.Message}");
		}
	}
#endif
       
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
