# Mac Setup and Verification Guide for Sumonity

This guide describes how to run and verify the **Sumonity** project on **macOS**.

---

## 1. Project Verification Status

| Component | Status on this Machine | Notes |
| :--- | :--- | :--- |
| **Unity Project Structure** | ✅ Verified | Complete with Assets, Packages, ProjectSettings |
| **3D Model (`tum_main_campus.fbx`)** | ✅ Present (181.99 MB) | Located at `Assets/3d_model/tum_main_campus.fbx` |
| **Python Virtual Environment** | ✅ Ready | `Assets/Sumonity/SumoTraCI/.venv` (Python 3.9/3.11 with `traci`, `numpy`, `sumolib`) |
| **SumoStarter macOS Compatibility** | ✅ Integrated | `SumoStarter.cs` automatically detects macOS path: `Assets/Sumonity/SumoTraCI/.venv/bin/python` |
| **SUMO Engine (TraCI co-simulation)** | ⚠️ Optional / System dependent | Requires local SUMO binary if live SUMO co-simulation is enabled; or pure Unity mode |

---

## 2. Setting Up Python on macOS

The Python virtual environment is located inside `Assets/Sumonity/SumoTraCI/.venv`.

To verify or reinstall dependencies:

```bash
cd Assets/Sumonity/SumoTraCI
python3 -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
```

Verify installed packages:
```bash
./.venv/bin/python -c "import traci, numpy, sumolib; print('Python dependencies verified successfully!')"
```

---

## 3. Running in Unity

1. Open **Unity Hub** on macOS.
2. Click **Add project from disk** and select `/Users/admin/Documents/GitHub/Sumonity-UnityBaseProject`.
3. Open the project using **Unity 2022.3.62f3** (the checked-in project version).
4. In Unity Project window, navigate to `Assets/Scenes/MainScene.unity` and double-click to open.
5. Press **Play** ▶️.

---

## 4. Working Purely Inside Unity (Without Windows APIs)

Since the simulation logic runs directly in Unity using C# scripts:
- `SumoSocketClient.cs` connects over local TCP socket (`127.0.0.1:25001`).
- `VehicleManager.cs` dynamically manages 3D vehicle assets (Bus, Taxi, Passenger Cars, Pedestrians, Bicycles).
- `Pure Pursuit Control` executes natively inside Unity without any Windows-specific DLLs.
