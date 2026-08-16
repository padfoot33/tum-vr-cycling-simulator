# TUM VR Cycling Stress Response Experiment
## Project History, System Architecture & Comprehensive Changelog

---

## 1. Project Overview & Experimental Goals

- **Institution**: Technical University of Munich (TUM) | Chair of Traffic Engineering and Control
- **Study Title**: *VR Cycling Stress Response using Biometrics and Behavioural Data*
- **Platform**: Unity 6 / Universal Render Pipeline (URP) on macOS & Windows
- **Core Objective**: Measure cyclist stress, physiological reactions (Electrodermal Activity - EDA, Heart Rate - ECG/HRV, Temperature), and behavioral responses (speed deceleration, steering variability, lateral position deviation) across distinct urban infrastructure and traffic conflict scenarios in a realistic virtual model of the TUM Munich campus.

---

## 2. Experimental Route & Scenario Design

### Route 1: Gabelsbergerstraße Combined Event Sequence (Continuous Flow)
A continuous experimental route linking two high-workload event zones separated by a physiological recovery corridor:
1. **Starting Point**: Cyclist starts at the approach to Gabelsbergerstraße.
2. **Event Zone 1 (`Point 5` in TUM Specification - Bus Stop Interaction)**:
   - 70–80m before the bus stop, passing `Trigger_Scenario1_BusStop` logs `BUS_EVENT_START`.
   - A public transit bus spawns behind the cyclist on the adjacent left lane at **20 m/s (~72 km/h)**.
   - The bus overtakes the cyclist, signals, brakes, and pulls into the Gabelsbergerstraße bus stop bay.
   - The bus **parks permanently** at the bus stand (no vanishing/spinning).
   - The cyclist slows down, steers left around the parked bus, and passes it (logging `BUS_EVENT_END`).
3. **Recovery Corridor**:
   - Cyclist rides straight down Gabelsbergerstraße along the **red-orange designated bike lane** past the TUM Main Entrance (~15–20 seconds).
   - Physiological sensor signals are recorded during this window to measure baseline recovery.
4. **Event Zone 2 (`Point 6` in TUM Specification - Right-Turn Mixed Traffic)**:
   - Approaching the Arcisstraße junction on the red-orange strip, cyclist crosses `Trigger_Scenario1_RightTurn` (logging `RIGHT_TURN_START`).
   - An aggressive vehicle spawns behind on the cyclist's left track, overtakes the cyclist, and executes a right turn across the cyclist's trajectory.
   - Surrounding multi-directional city traffic crosses the junction.
   - Cyclist completes the turn and stabilizes (logging `RIGHT_TURN_END`).

### Route 2: Construction Narrowing & Optional Parked Pull-Out
A separate infrastructure-stress evaluation route:
1. **Scenario 3 (Construction Narrowing)**:
   - Road width is artificially constricted using construction scaffolding, warning fences, and barrier props.
   - As the cyclist enters the narrow corridor (`CONSTRUCTION_START`), an adjacent passing vehicle drives through the bottleneck, squeezing the lateral clearance buffer.
   - Cyclist clears the barriers (`CONSTRUCTION_END`) and recovers.
2. **Optional Extension (Parked Vehicle Pull-Out)**:
   - A vehicle parked at the curb activates its turn signal and pulls out into the cyclist's bike lane as the cyclist approaches (`PARKED_PULLOUT_START`).

---

## 3. Scene Hierarchy Architecture (`Scenarios` Container)

```
MainScene
├── ☀️ Lighting & Environment
│   ├── Directional Light (Realtime Mode, Soft Shadows)
│   └── Camera (MainCamera with SmoothFollowBicycleCamera)
│
├── 🏛️ World Geometry
│   ├── TUM_Campus_Container (Active Polished 3D Model with MeshColliders on all tiles)
│   └── tum_main_campus (Deactivated legacy dataset)
│
├── 🚲 Cyclist Player
│   └── bicyle_animated_human
│       ├── BoxCollider (Root trigger detector: Center 0, 0.8, 0 | Size 0.8, 1.6, 1.8)
│       ├── BicyclePhysicsController (Single-track longitudinal & yaw dynamics)
│       ├── BicycleInput (WASD keyboard throttle, brake, steer)
│       ├── BicycleLeanAnimator (Dynamic chassis lean into turns)
│       └── SmartBicycleSafetyAssistant (Emergency Auto-Brake + Proximity Nudge)
│
├── 🚦 Scenarios (Master Container)
│   ├── Scenario_1 (Combined Route 1: Bus Stop & Right-Turn)
│   │   ├── Bus_Overtake_Path (WP_0 ➔ WP_4, User Preserved Coordinates)
│   │   ├── Trigger_Scenario1_BusStop (Preserved at: 436.1, 1.2, -21.54)
│   │   ├── Trigger_Scenario1_RightTurn (Located before the red-orange bike lane)
│   │   ├── RightTurn_Overtaking_Car_Path (WP_0 ➔ WP_3)
│   │   └── Scenario1_CombinedController (Master state machine)
│   │
│   └── Scenario_2 (Construction Narrowing)
│       ├── Trigger_Scenario2_Construction
│       └── Scenario3_ConstructionNarrowing
│
├── 🚗 Ambient City Traffic System
│   ├── Global_City_Traffic_Manager (Multi-model spawner with collision avoidance & 'T' toggle)
│   └── City_Traffic_Paths
│       ├── Path_Gabelsberger_Eastbound
│       ├── Path_Gabelsberger_Westbound
│       ├── Path_Arcis_Northbound
│       ├── Path_Arcis_Southbound
│       ├── Path_Luisen_Southbound
│       └── Path_Theresien_Eastbound
│
└── 🖥️ UI & Logging Management
    ├── Scenario_System (ScenarioManager & EventMarkerLogger)
    ├── HUD_Controller (Realtime speed, scenario status indicator, message prompts)
    ├── Scenario_Selection_UI (Pixel-perfect interactive modal + Traffic toggle)
    └── EventSystem (UI mouse raycasting & click processing)
```

---

## 4. Vehicle Assets & Pool (9 Vehicle Models)

The traffic system pools 9 diverse vehicle models for realistic city traffic:
1. `Assets/Sumonity-PassengerCars/prefabs/sedanCar.prefab`
2. `Assets/Sumonity-PassengerCars/prefabs/suvCar.prefab`
3. `Assets/Sumonity-PassengerCars/prefabs/hatchbackCar.prefab`
4. `Assets/Sumonity-PassengerCars/prefabs/wagonCar.prefab`
5. `Assets/Sumonity-PassengerCars/prefabs/coupeCar.prefab`
6. `Assets/Sumonity-PassengerCars/prefabs/multivanCar.prefab`
7. `Assets/Sumonity-PassengerCars/prefabs/offroadCar.prefab`
8. `Assets/TaxiModel/Prefabs/TaxiOpenSource.prefab`
9. `Assets/BusModel/Prefabs/BusOpenSource.prefab`

*All spawned traffic vehicles automatically have conflicting SUMO scripts disabled, rigidbodies set to kinematic, and run autonomous `SmartVehicleAI` with forward vehicle-to-vehicle collision avoidance.*

---

## 5. Summary of Scripts & Components Created/Updated

| Script File | Purpose & Functionality |
|---|---|
| [`SmartBicycleSafetyAssistant.cs`](file:///Users/admin/Documents/GitHub/Sumonity-UnityBaseProject/Assets/Scripts/AI/SmartBicycleSafetyAssistant.cs) | **Emergency Auto-Braking**: Casts forward spherecast; automatically engages brakes if approaching stopped bus/cars to prevent VR head-on clipping.<br>**Lateral Proximity Nudge**: Applies gentle lateral offset when vehicles pass within 1.2m. |
| [`SmartVehicleAI.cs`](file:///Users/admin/Documents/GitHub/Sumonity-UnityBaseProject/Assets/Scripts/AI/SmartVehicleAI.cs) | Follows waypoint routes with **Forward Vehicle-to-Vehicle Collision Avoidance** (maintains 3.5m safety buffer in traffic queues). Supports **Stress Vehicle Bypass Mode** for experiment overtakers. |
| [`GlobalCityTrafficManager.cs`](file:///Users/admin/Documents/GitHub/Sumonity-UnityBaseProject/Assets/Scripts/AI/GlobalCityTrafficManager.cs) | Spawns continuous traffic across 6 campus arteries using the 9 vehicle models pool. Includes runtime **`T`** hotkey and HUD ON/OFF toggle. |
| [`Scenario1_CombinedController.cs`](file:///Users/admin/Documents/GitHub/Sumonity-UnityBaseProject/Assets/Scripts/Scenarios/Scenario1_CombinedController.cs) | Master state machine for Route 1: orchestrates Bus Overtake & Permanent Stop ➔ Recovery Corridor ➔ Red-Strip Right-Turn Car Overtake ➔ Scenario Completion. |
| [`SmoothFollowBicycleCamera.cs`](file:///Users/admin/Documents/GitHub/Sumonity-UnityBaseProject/Assets/Scripts/Camera/SmoothFollowBicycleCamera.cs) | Smooth 3rd-person vehicle chase camera that follows cyclist heading and steering with zero jitter. Press **`V`** to toggle into 1st-person cockpit/handlebar view. |
| [`ScenarioSelectionUI.cs`](file:///Users/admin/Documents/GitHub/Sumonity-UnityBaseProject/Assets/Scripts/UI/ScenarioSelectionUI.cs) | Crystal-clear in-game scenario selector canvas (`pixelPerfect = true`, `dynamicPixelsPerUnit = 2.0f`). Quick teleportation with keys **`1`**, **`2`**, **`3`**, **`4`**, **`M`**, and **`T`**. |
| [`EventMarkerLogger.cs`](file:///Users/admin/Documents/GitHub/Sumonity-UnityBaseProject/Assets/Scripts/Scenarios/EventMarkerLogger.cs) | Singleton CSV logger generating timestamped biometric event markers (`experiment_log_{datetime}.csv`). |
| [`ScenarioSetupMenu.cs`](file:///Users/admin/Documents/GitHub/Sumonity-UnityBaseProject/Assets/Scripts/Editor/ScenarioSetupMenu.cs) | Editor tooling providing 1-click hierarchy structuring, campus collider generation, traffic route building, and non-destructive position preservation. |

---

## 6. Complete History of Issues Resolved & Changelog

### Milestone 1: Submodule Restoration & Project Health
- Restored missing bicycle mesh and animations at `Assets/Sumonity-BicycleModel/prefabs/bicyle_animated_human.prefab` (19.6 MB FBX).
- Restored missing textures and materials across `Sumonity-PassengerCars`, `PedestrianModel`, and `BusModel`.

### Milestone 2: DirectInput & Platform Compatibility (macOS)
- **Issue**: `DllNotFoundException: DirectInputForceFeedback.dll` on macOS from `com.mrtimcakes.directinput`.
- **Fix**: Removed `com.mrtimcakes.directinput` from `Packages/manifest.json` and wrapped `DirectInputManager` calls in `FFB_Bike.cs` with `#if ENABLE_DIRECTINPUT` while keeping all public properties (`steeringInputCorrected`, `steeringInput`) accessible for external controllers.

### Milestone 3: SUMO NullReferenceException Suppression
- **Issue**: 999+ `NullReferenceException` errors thrown by `TaxiController`, `CarController`, `BusController` calling `SumoVehicleControl(ref sock)` in pure Unity simulation.
- **Fix**: Added global null safety checks (`if (sock == null) return;`) across all vehicle controllers and disabled legacy SUMO scripts on all spawned AI vehicles.

### Milestone 4: Camera Tracking & View Toggle
- **Issue**: Main Camera was untagged and remained static when pressing Play.
- **Fix**: Implemented `SmoothFollowBicycleCamera.cs` with automatic target discovery, smooth yaw/height damping, and instant **`V` key** toggling between 3rd-person chase and 1st-person handlebar view.

### Milestone 5: Bus Overtaking & Permanent Bus Stand Parking
- **Issue**: Bus was previously disappearing or spinning when reaching the end of its waypoint route.
- **Fix**: Updated `WaypointFollower.cs` with `DestroyAtEnd = false` and speed configuration (**20 m/s**). When the bus reaches the final waypoint at the Gabelsbergerstraße bus stop, it smoothly halts and **remains permanently parked in the bus bay**.

### Milestone 6: Light Baking Failure Fix
- **Issue**: `Light baking failed with error code 2 (Failed to deserialize bake input)` when Unity attempted static GI light baking on the 182 MB campus dataset.
- **Fix**: Automated lighting configuration in `ScenarioSetupMenu.cs`: disabled baked GI, cleared bake cache, and configured Directional Light to **Realtime Mode with Soft Shadows**.

### Milestone 7: Interactive Scenario UI & Blurry Text Fix
- **Issue**: UI buttons were unclickable (due to missing `EventSystem`) and text was blurry (low `dynamicPixelsPerUnit`).
- **Fix**: Ensured `EventSystem` + `StandaloneInputModule` auto-creation, set `raycastTarget = false` on child labels, enabled `pixelPerfect = true`, increased `dynamicPixelsPerUnit = 2.0f`, and added number key shortcuts (`1`, `2`, `3`, `4`).

### Milestone 8: New 3D Campus Model & Ground Collision Fix
- **Issue**: Bicycle fell endlessly through `TUM_Campus_Container` because root `MeshCollider` lacked mesh references.
- **Fix**: Implemented `AddCollidersToCampusModel()` which scans all child tiles and attaches `MeshCollider` components with assigned `sharedMesh` to every ground and road tile.

### Milestone 9: Citywide 9-Model Traffic & Combined Route 1
- Created `GlobalCityTrafficManager.cs` with multi-axis campus traffic routes and runtime toggle (**`T`** key).
- Integrated `SmartBicycleSafetyAssistant.cs` with emergency auto-braking and proximity lateral nudging.
- Built `Scenario1_CombinedController.cs` linking Bus Stop Overtake ➔ Recovery ➔ Red-Strip Right-Turn Car Overtake.

---

## 7. Controls & Testing Quick Reference

| Action | Control / Key |
|---|---|
| **Pedal / Accelerate** | **`W`** |
| **Brake / Reverse** | **`S`** |
| **Steer Left / Right** | **`A` / `D`** |
| **Handbrake** | **`Space`** |
| **Toggle Camera View (1st / 3rd Person)** | **`V`** |
| **Toggle Ambient City Traffic (ON / OFF)** | **`T`** |
| **Open Scenario Selector Menu** | **`M`** or **`Tab`** |
| **Quick Scenario Selection** | **`1`** (Route 1), **`2`** (Route 2), **`3`** (Free Roam) |
| **Unity Setup Menu Command** | **`Cycling Experiment` ➔ `Build Combined Scenario 1 & Smart Traffic System`** |
| **Preserve-Only Setup Command** | **`Cycling Experiment` ➔ `Fix UI & Scripts ONLY (Preserve 100% User Placements)`** |

---

## 8. Biometric Data Output Specification

Biometric and behavioral event markers are exported directly to CSV at `Application.persistentDataPath` with the filename format:  
`experiment_log_YYYY-MM-DD_HH-MM-SS.csv`

### Recorded Columns:
1. `Timestamp`: System time in ISO 8601 format.
2. `EventName`: Marker tag (`ROUTE1_START`, `BUS_EVENT_START`, `BUS_OVERTAKE_COMPLETE`, `BUS_EVENT_END`, `RIGHT_TURN_START`, `RIGHT_TURN_END`, `CONSTRUCTION_START`, `CONSTRUCTION_END`).
3. `PlayerPosX`, `PlayerPosY`, `PlayerPosZ`: World coordinates in meters.
4. `PlayerSpeedKph`: Speed in km/h.
5. `PlayerHeading`: Compass/yaw angle in degrees.
