# Session notes

Working notes for current development. The project map lives in [PROJECT_MINDMAP.md](PROJECT_MINDMAP.md).

## 20 Aug 2026 — Client play HUD, bus timing, sign, run log

Participant Play has no speed HUD, scenario banner, toasts, or persistent M/T buttons. Press **M** for the operator modal only when the participant lock is off.

Route 1 bus trigger is ~75 m before the bay; the Bogdan spawns behind the cyclist and is sped so it overtakes then parks before the rider arrives. StVO 214-10 sign at the right-turn. CSV at `Logs/<participant>/` from [`ExperimentRunLogger`](../Assets/Scripts/Logging/ExperimentRunLogger.cs) (client columns, no LOD, 20 Hz + vehicle markers).

**Editor:** **Cycling Experiment → Place Route 1 Right-Turn Sign**, **Retune Route 1 Bus Timing**, **Ensure Experiment Run Logger**. Save MainScene after Play so runtime-created sign/logger persist.

## 20 Aug 2026 — SimBike replaces keyboard cyclist

`MainScene` now uses [`SimBike.prefab`](../Assets/BicycleSimulatorModel/Prefabs/SimBike.prefab) as the rider. The old `bicyle_animated_human` stays in the scene **disabled** for revert.

**Editor:** **Cycling Experiment → Swap Cyclist to SimBike** (already applied if you ran it). View is SimBike first-person (`Main Camera` on the rider). Chase cam `V` toggle is off. WASD / `S` / Space work until hardware connects.

**Lab hardware (independent axes):**
- Speed: Wahoo Kickr TCP `192.168.0.2:36866`
- Steer: Fanatec DirectInput + FFB
- Brake: Arduino serial `COM3` @ 9600 (auto-detect)
- Unplug one device and that axis falls back to keys.

**Play check:** Play MainScene without hardware → WASD, HUD km/h, `[1]` Route 1 bus, `[2]` Route 2, cars yield, play-area clamp. Save the scene after the swap menu.

## 19 Aug 2026 — Inspector-locked MainScene builds

One scene: `Assets/Scenes/MainScene.unity`. On **Experiment_Scene_Refs**, tick **Lock Participant Run**, set route (1 or 2) and traffic, save, then File → Build Settings → Build. The player starts that run only (no M / T / 1 / 2). Leave the lock off for editor Play with the menu.

**Cycling Experiment → Lock Run → …** writes those Inspector fields for you. **Unlock for editor Play** turns the lock off.

Route 1 / Route 2 play-area boxes (`Route1_PlayArea`, `Route2_PlayArea`): cyan gizmos; rider cannot leave the union. **Cycling Experiment → Create Route Play Areas** seeds them; resize `Box_*` children in the Scene view.

Ambient car `Prewarm` + first three spawns wait two frames after Play starts.

## 19 Aug 2026 — Ambient car pool and click-drag waypoints

Campus cars are rented from `AmbientVehiclePool` on `GlobalCityTrafficManager` (prewarm `maxVehicles`, cap +8 for intersection). Path end and distance cull `Release` (deactivate) instead of `Destroy`. Route 1 bus / right-turn still instantiate.

Select a `WaypointPath` (or a `WP_n` child) and click a yellow sphere in the Scene view to drag it. Shift-click empty road still appends a waypoint.

## 19 Aug 2026 — Spawn cars only near the cyclist

Ambient cars spawn in a horizontal ring around the rider (default **10–200 m**) on `GlobalCityTrafficManager` (Proximity spawn). Cars farther than max + 20 m padding are destroyed. Scene gizmos: orange inner circle, cyan outer. Route 1 bus / right-turn are not culled.

## 19 Aug 2026 — Manual campus traffic paths (bus-style)

Ambient cars follow authored `WaypointPath` lists under `Campus_Traffic_Paths`, the same way the Route 1 bus follows `Bus_Overtake_Path`. The auto-seeded `Campus_Road_Network` graph is no longer used for driving.

- Editor: **Cycling Experiment → Create Campus Traffic Path**. Select the path, enable **Edit Path**, Shift-click the road to append `WP_n`. Hierarchy child order is travel order. **Create Path From Selection** copies selected `Node_*` positions into a new path without destroying the originals.
- Two-way street = two paths with opposite waypoint order. Tick **Loop** only for a closed circuit. Default cars despawn at the end of a one-way path.
- `GlobalCityTrafficManager` / `IntersectionTrafficFlowManager` spawn with `SmartVehicleAI` (`VehicleRuntimeFactory.SpawnAmbientOnWaypointPath`). `T` toggle, 14 m spawn-clear, follow gap, and cyclist yield stay. Route 1 bus / right-turn stay on `WaypointFollower`.
- Play no longer rebuilds the campus graph. **Build Campus Road Graph** is obsolete.

## 19 Aug 2026 — Node-edge ambient traffic

Ambient and intersection cars no longer use NavMesh. They travel on `Campus_Road_Network`: junction `RoadNode`s and directed `RoadEdge`s, driven by `GraphVehicleAI`.

- Editor: **Cycling Experiment → Build Campus Road Graph** seeds Gabelsberger, Arcis, Luisen, Theresien (two-way) and Route 2 (one-way, right lane). Vertices snap to nearby `Dest_*` and merge at junctions. Move nodes in the Scene view; re-run the menu to rebuild from seeds.
- Play without a saved graph still builds one at runtime if the edge list is empty.
- `GlobalCityTrafficManager` / `IntersectionTrafficFlowManager` spawn on edges (`VehicleRuntimeFactory.SpawnOnGraph`). `T` toggle, 14 m spawn-clear, ~11 m follow gap, and cyclist yield are unchanged.
- Route 1 bus and right-turn car stay on waypoint paths.
- Route 2 cones stay visual; cars use the right-lane graph edge instead of NavMesh carve. Bike `NavMeshObstacle` removed. Bake Road NavMesh / Auto Place Traffic Destinations menus are obsolete for cars.
- `Campus_Road_NavMesh` is deactivated. Chevron/dumpster/cone `NavMeshObstacle` components are stripped campus-wide on Play or via **Cycling Experiment → Strip Construction NavMesh Obstacles**. Pedestrian / SUMO / bus-boarding NavMesh is untouched.

## 17 Aug 2026 — Route 2 cyclist spawn

`Cyclist_Spawn_Route2` (same cyan gizmo as Route 1). `[2]` teleports to that empty. Move it in the Scene view; default is `(721.5, 70)` heading north.

## 17 Aug 2026 — Traffic spawn redesign

No burst pile: Play starts 3 spaced cars, then one per interval. Spawn refuses a spot within 14 m of another car. Route 2 uses 16 m northbound slots (south-most empty). Cars hold ~11 m behind the leader; only the rear car unsticks. Inspector max is a cap, not a stack size.

## 17 Aug 2026 — Inspector traffic caps

`GlobalCityTrafficManager.Awake` no longer forces `maxVehicles` to 12 or `spawnInterval` to 3. Route 2 now seeds along the northbound street (not only a tight south cluster that often failed the corridor check).

## 17 Aug 2026 — Route 2 one-way, lanes, reverse

Skybridge street (`x ≈ 723`) is northbound only: cars spawn at the south end facing `+Z`. Turns use a right-lane via so they do not cut onto the cyclist lane. Deadlocked cars reverse, check right, then repath. Auto-brake no longer blocks `S` reverse.

## 17 Aug 2026 — Cars stop for the cyclist

City / intersection cars now yield: they track the bike from `ExperimentSceneRefs`, box-cast the lane, and halt the NavMesh agent instead of driving through. The bike also auto-brakes for a car closing from the front or side. Scripted Route 1 stress vehicles (bus / right-turn car) still complete their manoeuvre.

## 17 Aug 2026 — Don't ride through the Route 1 bus

The bike is kinematic, so Unity never physically blocks the Bogdan. Auto-brake was also skipping it: 0.5 s checks, a 0.1 m probe, ignore-if-closer-than-1.2 m, and a 5.2 m-wide bus box. Safety assist now box-casts the bike's lane every physics tick, hard-stops and pushes out on overlap. Bus collider is ~2.55 × 3.15 × 11.2 m.

## 17 Aug 2026 — Route 1 bus audio

Clips in `Assets/Audio/Bus/`. `ScenarioBusAudio` on the spawned Bogdan: departure + engine loop, then brake and idle when the path ends. Quiet station loop on `Trigger_Scenario1_BusStop`. Clips auto-assign in the Editor from that folder.

## 17 Aug 2026 — City cars only, Route 2 traffic, carve props

- Ambient / intersection pools no longer include `BusOpenSource` or Bogdan. Route 1 still uses Bogdan on the trigger path.
- City traffic cap 12, 6 cars at start, ~40% of spawns use skybridge seeds around `(723, 128)`.
- **Cycling Experiment → Add Construction NavMesh Obstacles** (also runs on Play) carves `ChevronSign`, dumpsters, and Route 2 cones.

## 16 Aug 2026 — Bus spawn debug

Route 1 now spawns the Bogdan bus immediately on `[1]` / Play (not only after the roadside trigger). Ground snap ignores the bus's own collider so it cannot climb into the sky. `busSpeed` is clamped before spawn (scene had 45 m/s).

## 16 Aug 2026 — Bogdan A092 bus

Route 1 bus model is now the imported Bogdan A092 FBX (`Assets/BusModel/BogdanA092/`). Editor: **Cycling Experiment → Install Bogdan A092 Bus** builds `Assets/BusModel/Prefabs/BogdanA092.prefab` (collider + kinematic RB, scaled to ~11 m) and assigns it on `Scenario1_CombinedController`. Falls back to `BusOpenSource` until that prefab exists.

## 16 Aug 2026 — 20 km/h, pedaling ramp, lean-turn

- Play cap is **20 km/h** (`5.56` m/s) in `ScenarioSelectionUI`.
- Pedaling clip speed ramps with bike speed (`Animator.speed` 0.4 → 1.2).
- A/D steer is 18° with less speed attenuation; Armature lean sensitivity 8, max 30°.

## 16 Aug 2026 — Cyclist speed / auto-brake

- Play was forcing **12 km/h** in `ScenarioSelectionUI.ApplyCyclistSpeedLimit`, which overwrote the Inspector `Max Speed`. Cap is now **14.4 km/h** (+20%). `Max Speed` is **m/s** (4.0 ≈ 14.4 km/h), not km/h.
- Auto-brake ignored a bus on the left only if it missed the spherecast. It now requires the vehicle to be ahead *and* within 0.9 m laterally, so an overtake from the left no longer cuts throttle.

## 16 Aug 2026 — Road NavMesh traffic

Ambient cars no longer follow the wrong yellow `City_Traffic_Paths` lines.

- Editor: **Cycling Experiment → Bake Road NavMesh** collects `Roads` / `Road` / `Asphalt` meshes under `TUM_Campus_Container`, bakes `Assets/Settings/Campus_Road_NavMesh.asset`, hides city waypoint gizmos. Route 1 bus/right-turn paths are kept.
- Editor: **Cycling Experiment → Auto Place Traffic Destinations** samples that NavMesh and drops yellow `Dest_*` empties under `Traffic_Destinations` (default 48 m spacing, max 72). No hand-clicking required.
- `NavMeshVehicleAI` + `VehicleRuntimeFactory.SpawnOnNavMesh`: cars stay on the mesh, prefer the right side, queue, yield to the cyclist.
- `[1]` spawn is computed behind `Trigger_Scenario1_BusStop` (not a hardcoded world point).
- Route 2 cones/barriers carve the NavMesh. `Path_Route2_*` is no longer created.

## 16 Aug 2026 — Route 1 / city traffic / Route 2

Improved the existing `CyclingExperiment` stack (no rewrite). Logging, SUMO, hardware, and RCCP were not touched. Parked pull-out skipped.

### Shared
- New `Assets/Scripts/AI/VehicleRuntimeFactory.cs` — disable SUMO controllers, kinematic RB, attach `SmartVehicleAI`.
- Used by Route 1, `GlobalCityTrafficManager`, and `IntersectionTrafficFlowManager`.

### Traffic AI
- `SmartVehicleAI`: 12 m lookahead, corner slowdown, ambient cars yield to the cyclist, stress vehicles do not.
- City cars now spawn along their path (not only at WP_0).
- Intersection flow starts when Route 1 right-turn triggers; falls back to Gabelsberger / Arcis city paths.

### Route 1
- Bus spawns behind and to the left of the cyclist, then follows `Bus_Overtake_Path` and parks at the bay.
- Right-turn car still uses `RightTurn_Overtaking_Car_Path` (stress, no yield).

### Route 2
- No construction trigger. Demo `BusOpenSource` at ~(723, 128) is removed if present.
- Skybridge street: `Path_Route2_Northbound` / `Path_Route2_Southbound` plus cone/barrier chute under `Scenario_2/Route2_Construction_Props`.
- `[2]` teleports to approach `(721.5, 0.2, 70)` heading north into the chute.
- Created at Play by `Scenario3_ConstructionNarrowing` (execution order -100), or via **Cycling Experiment** editor menus.

### How to test
1. Play `MainScene` → `[1]` → ride north → bus overtakes from the left and parks → orange lane → car overtakes and turns right; junction traffic should appear.
2. `T` toggles ambient traffic.
3. `[2]` → ride through the cone chute with city cars on the same street.
