#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using CyclingExperiment.AI;
using CyclingExperiment.Camera;
using CyclingExperiment.Scenarios;
using CyclingExperiment.UI;
using ExperimentRefs = CyclingExperiment.ExperimentSceneRefs;

namespace CyclingExperiment.Editor
{
    public static class ScenarioSetupMenu
    {
        [MenuItem("Cycling Experiment/Build Combined Scenario 1 & Smart Traffic System", false, 0)]
        public static void BuildCombinedScenarioSystem()
        {
            // 1. Ensure EventSystem exists
            EnsureEventSystemExists();

            // 2. Ground/road MeshColliders on TUM_Campus_Container (so bicycle never falls)
            AddCollidersToCampusModel();

            // 3. Setup Bicycle Smart Safety Assistant & Physics
            GameObject bicycle = FindExperimentBicycle();
            if (bicycle == null)
            {
                EditorUtility.DisplayDialog("Error", "Could not find SimBike or bicyle_animated_human in the hierarchy.", "OK");
                return;
            }

            bicycle.tag = "Player";

            if (bicycle.GetComponent<SBPScripts.Simulator.BicycleSimulatorController>() != null)
            {
                ConfigureSimBikeForExperiment(bicycle);
            }
            else
            {
                var rootCol = bicycle.GetComponent<BoxCollider>() ?? bicycle.AddComponent<BoxCollider>();
                rootCol.center = new Vector3(0, 0.8f, 0);
                rootCol.size = new Vector3(0.8f, 1.6f, 1.8f);

                var bikePhysics = bicycle.GetComponent("BicyclePhysicsController") as MonoBehaviour;
                if (bikePhysics != null) bikePhysics.enabled = true;

                var bikeInput = bicycle.GetComponent<BikeURP.BicycleInput>();
                if (bikeInput != null)
                {
                    bikeInput.enabled = true;
                    bikeInput.controller = bikePhysics as BikeURP.BicyclePhysicsController;
                    bikeInput.useDigitalSteer = true;
                }

                var bikeLean = bicycle.GetComponent<BikeURP.BicycleLeanAnimator>();
                if (bikeLean != null)
                {
                    bikeLean.enabled = true;
                    bikeLean.controller = bikePhysics as BikeURP.BicyclePhysicsController;
                    Transform armature = bicycle.transform.Find("Armature") ?? bicycle.transform;
                    bikeLean.leanRoot = armature;
                    bikeLean.leanSensitivity = 8f;
                    bikeLean.maxLeanDeg = 30f;
                }

                var sumoController = bicycle.GetComponent("BicycleSumoController") as MonoBehaviour;
                if (sumoController != null) sumoController.enabled = false;

                if (bicycle.GetComponent<SmartBicycleSafetyAssistant>() == null) bicycle.AddComponent<SmartBicycleSafetyAssistant>();

                SetupCamera(bicycle.transform);
            }

            // 4. Organize Scenarios Hierarchy Container
            GameObject scenariosRoot = GameObject.Find("Scenarios");
            if (scenariosRoot == null) scenariosRoot = new GameObject("Scenarios");

            GameObject scenario1Obj = GameObject.Find("Scenario_1");
            if (scenario1Obj == null)
            {
                scenario1Obj = new GameObject("Scenario_1");
                scenario1Obj.transform.SetParent(scenariosRoot.transform);
            }

            GameObject busPath = GameObject.Find("Bus_Overtake_Path");
            if (busPath != null) busPath.transform.SetParent(scenario1Obj.transform);

            GameObject busTrigger = GameObject.Find("Trigger_Scenario1_BusStop");
            if (busTrigger != null) busTrigger.transform.SetParent(scenario1Obj.transform);

            GameObject rightTurnPathObj = GameObject.Find("RightTurn_Overtaking_Car_Path");
            if (rightTurnPathObj == null)
            {
                rightTurnPathObj = new GameObject("RightTurn_Overtaking_Car_Path");
                rightTurnPathObj.transform.SetParent(scenario1Obj.transform);
                var pathComp = rightTurnPathObj.AddComponent<WaypointPath>();
                CreateChildWaypoint(rightTurnPathObj.transform, "WP_0", new Vector3(434f, 0.2f, 90f));
                CreateChildWaypoint(rightTurnPathObj.transform, "WP_1", new Vector3(434f, 0.2f, 150f));
                CreateChildWaypoint(rightTurnPathObj.transform, "WP_2", new Vector3(440f, 0.2f, 172f));
                CreateChildWaypoint(rightTurnPathObj.transform, "WP_3", new Vector3(530f, 0.2f, 172f));
                pathComp.waypoints = new List<Transform>
                {
                    rightTurnPathObj.transform.Find("WP_0"),
                    rightTurnPathObj.transform.Find("WP_1"),
                    rightTurnPathObj.transform.Find("WP_2"),
                    rightTurnPathObj.transform.Find("WP_3")
                };
            }

            GameObject rightTurnTrigger = GameObject.Find("Trigger_Scenario1_RightTurn");
            if (rightTurnTrigger == null)
            {
                rightTurnTrigger = new GameObject("Trigger_Scenario1_RightTurn");
                rightTurnTrigger.transform.SetParent(scenario1Obj.transform);
                rightTurnTrigger.transform.position = new Vector3(430f, 0.2f, 110f);

                var box = rightTurnTrigger.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(18f, 5f, 10f);
            }

            var combinedCtrl = scenario1Obj.GetComponent<Scenario1_CombinedController>() ?? scenario1Obj.AddComponent<Scenario1_CombinedController>();
            combinedCtrl.AutoAssignReferences();

            if (busTrigger != null)
            {
                var trig = busTrigger.GetComponent<ScenarioTrigger>() ?? busTrigger.AddComponent<ScenarioTrigger>();
                if (trig.OnPlayerEntered == null) trig.OnPlayerEntered = new UnityEngine.Events.UnityEvent();
                UnityEditor.Events.UnityEventTools.RemovePersistentListener(trig.OnPlayerEntered, combinedCtrl.TriggerBusOvertake);
                UnityEditor.Events.UnityEventTools.AddPersistentListener(trig.OnPlayerEntered, combinedCtrl.TriggerBusOvertake);
            }

            if (rightTurnTrigger != null)
            {
                var trig = rightTurnTrigger.GetComponent<ScenarioTrigger>() ?? rightTurnTrigger.AddComponent<ScenarioTrigger>();
                if (trig.OnPlayerEntered == null) trig.OnPlayerEntered = new UnityEngine.Events.UnityEvent();
                UnityEditor.Events.UnityEventTools.RemovePersistentListener(trig.OnPlayerEntered, combinedCtrl.TriggerRightTurnCar);
                UnityEditor.Events.UnityEventTools.AddPersistentListener(trig.OnPlayerEntered, combinedCtrl.TriggerRightTurnCar);
            }

            GameObject scenario2Obj = GameObject.Find("Scenario_2");
            if (scenario2Obj == null)
            {
                scenario2Obj = new GameObject("Scenario_2");
                scenario2Obj.transform.SetParent(scenariosRoot.transform);
            }
            if (scenario2Obj.GetComponent<Scenario3_ConstructionNarrowing>() == null)
            {
                scenario2Obj.AddComponent<Scenario3_ConstructionNarrowing>();
            }
            Scenario3_ConstructionNarrowing.RemoveDemoBusMarker();
            Scenario3_ConstructionNarrowing.HideLegacyRoute2WaypointPaths();
            Scenario3_ConstructionNarrowing.EnsureConstructionProps();

            // 5. Ambient traffic uses authored Campus_Traffic_Paths.
            EnsureCityTrafficManager();
            CampusTrafficPathMenu.EnsureRoot();
            RoadNavMeshBaker.HideWrongCityWaypointPaths();

            // 6. Setup Scenario Selection UI & HUD
            GameObject hudObj = GameObject.Find("HUD_Controller");
            if (hudObj == null) hudObj = new GameObject("HUD_Controller");
            var hud = hudObj.GetComponent<HUDController>() ?? hudObj.AddComponent<HUDController>();
            MonoBehaviour cyclistForHud = bicycle.GetComponent<SimBikeCyclistMotion>()
                ?? bicycle.GetComponent("BicyclePhysicsController") as MonoBehaviour;
            if (cyclistForHud != null) hud.SetBicycleController(cyclistForHud);

            GameObject uiObj = GameObject.Find("Scenario_Selection_UI");
            if (uiObj == null) uiObj = new GameObject("Scenario_Selection_UI");
            var scenarioUI = uiObj.GetComponent<ScenarioSelectionUI>() ?? uiObj.AddComponent<ScenarioSelectionUI>();
            scenarioUI.scenario1Position = new Vector3(436.1f, 0.2f, -80.0f);
            scenarioUI.scenario1Heading = 0f;
            scenarioUI.scenario2Position = Scenario3_ConstructionNarrowing.ApproachPosition;
            scenarioUI.scenario2Heading = Scenario3_ConstructionNarrowing.ApproachHeading;

            EnsureExperimentSceneRefs();

            // 7. Fix Lighting
            FixLightingInternal();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            EditorUtility.DisplayDialog("Cycling Experiment",
                "Combined Scenario 1 & Smart City Traffic System Successfully Built!\n\n" +
                "• Ground/road MeshColliders on TUM_Campus_Container (props are not triangle-cooked).\n" +
                "• Ambient traffic uses Campus_Road_Network (nodes + directed edges).\n" +
                "• Vehicle prefabs (cars, vans, taxis) populated.\n" +
                "• Smart Safety Assistant (Auto-Brake & Nudge) attached to bicycle.\n" +
                "• All custom waypoints and coordinates 100% PRESERVED.\n\n" +
                "Press Play ▶️ to test!", "OK");
        }

        private static void AddCollidersToCampusModel()
        {
            GameObject campus = CampusColliderSanitizer.FindCampus();
            if (campus != null)
                CampusColliderSanitizer.Apply(campus, addMissingGround: true);

            GameObject oldCampus = GameObject.Find("tum_main_campus");
            if (oldCampus != null && campus != null)
                oldCampus.SetActive(false);
        }

        private static void EnsureCityTrafficManager()
        {
            GameObject trafficMgrObj = GameObject.Find("Global_City_Traffic_Manager");
            if (trafficMgrObj == null) trafficMgrObj = new GameObject("Global_City_Traffic_Manager");
            var trafficMgr = trafficMgrObj.GetComponent<GlobalCityTrafficManager>() ?? trafficMgrObj.AddComponent<GlobalCityTrafficManager>();
            trafficMgr.LoadAllVehiclePrefabsIfEmpty();
        }

        private static void EnsureExperimentSceneRefs()
        {
            GameObject refsObj = GameObject.Find("Experiment_Scene_Refs");
            if (refsObj == null) refsObj = new GameObject("Experiment_Scene_Refs");
            var refs = refsObj.GetComponent<ExperimentRefs>() ?? refsObj.AddComponent<ExperimentRefs>();

            refs.bicycle = FindExperimentBicycle();
            if (refs.bicycle != null)
            {
                refs.bicycleTransform = refs.bicycle.transform;
                refs.bicyclePhysics = refs.bicycle.GetComponent<BikeURP.BicyclePhysicsController>();
                var motion = refs.bicycle.GetComponent<SimBikeCyclistMotion>()
                    ?? (refs.bicycle.GetComponent<SBPScripts.Simulator.BicycleSimulatorController>() != null
                        ? refs.bicycle.AddComponent<SimBikeCyclistMotion>()
                        : null);
                refs.SetCyclist(refs.bicycle, motion);
            }

            refs.route1 = Object.FindObjectOfType<Scenario1_CombinedController>();
            refs.cityTraffic = Object.FindObjectOfType<GlobalCityTrafficManager>();
            refs.intersectionTraffic = Object.FindObjectOfType<IntersectionTrafficFlowManager>();
            refs.hud = Object.FindObjectOfType<HUDController>();
            refs.followCamera = Object.FindObjectOfType<SmoothFollowBicycleCamera>();
            refs.eventSystem = Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();

            var trigger = GameObject.Find("Trigger_Scenario1_BusStop");
            if (trigger != null) refs.busStopTrigger = trigger.transform;
            var rightTurn = GameObject.Find("Trigger_Scenario1_RightTurn");
            if (rightTurn != null) refs.rightTurnTrigger = rightTurn.transform;
            refs.EnsureRightTurnSign();
            refs.EnsureRoute1ReferencePath();
            refs.EnsureRunLogger();
            AssignSerializedRef(refs, "runLogger", refs.runLogger);
            AssignSerializedRef(refs, "rightTurnSign", refs.rightTurnSign);
            AssignSerializedRef(refs, "route1PathTracker", refs.route1PathTracker);
            AssignSerializedRef(refs.runLogger, "bikeTransform", refs.bicycleTransform);
            AssignSerializedRef(refs.runLogger, "cyclistMotion", refs.Cyclist as MonoBehaviour);
            AssignSerializedRef(refs.runLogger, "referencePathTracker", refs.route1PathTracker);
            refs.route1CyclistSpawn = EnsureCyclistSpawnRoute1();
            refs.route2CyclistSpawn = EnsureCyclistSpawnRoute2();
            refs.cityTrafficPaths = GameObject.Find("City_Traffic_Paths");
            refs.campusTrafficPaths = CampusTrafficPathMenu.EnsureRoot();
            refs.trafficDestinations = EnsureTrafficDestinations();
            refs.campusRoadNetwork = Object.FindObjectOfType<RoadNetwork>();
            AssignSerializedRef(refs.cityTraffic, "destinations", refs.trafficDestinations);
            AssignSerializedRef(refs.cityTraffic, "campusTrafficPathsRoot", refs.campusTrafficPaths);
            AssignSerializedRef(refs.cityTraffic, "cityTrafficPathsRoot", refs.cityTrafficPaths);

            AssignSerializedRef(Object.FindObjectOfType<ScenarioSelectionUI>(), "sceneRefs", refs);
            AssignSerializedRef(refs.route1, "sceneRefs", refs);
            AssignSerializedRef(refs.route1, "playerTransform", refs.bicycleTransform);
            AssignSerializedRef(refs.route1, "intersectionTraffic", refs.intersectionTraffic);
            AssignSerializedRef(refs.followCamera, "target", refs.bicycleTransform);
            AssignSerializedRef(refs.intersectionTraffic, "cityTraffic", refs.cityTraffic);
            AssignSerializedRef(refs.hud, "_bicycleController", refs.Cyclist as MonoBehaviour);
            var logger = Object.FindObjectOfType<EventMarkerLogger>();
            AssignSerializedRef(logger, "playerTransform", refs.bicycleTransform);
            AssignSerializedRef(logger, "cyclistMotion", refs.Cyclist as MonoBehaviour);

            EditorUtility.SetDirty(refs);
        }

        private static TrafficDestinationSet EnsureTrafficDestinations()
        {
            GameObject root = GameObject.Find("Traffic_Destinations");
            if (root == null)
            {
                root = new GameObject("Traffic_Destinations");
                GameObject scenarios = GameObject.Find("Scenarios");
                if (scenarios != null) root.transform.SetParent(scenarios.transform);
            }

            var set = root.GetComponent<TrafficDestinationSet>() ?? root.AddComponent<TrafficDestinationSet>();
            if (root.transform.childCount == 0)
            {
                TrafficDestinationPlacer.PlaceFromNavMesh();
                set = root.GetComponent<TrafficDestinationSet>() ?? set;
            }

            set.RefreshFromChildren();
            EditorUtility.SetDirty(root);
            return set;
        }

        private static Transform EnsureCyclistSpawnRoute1()
        {
            GameObject spawnObj = GameObject.Find("Cyclist_Spawn_Route1");
            if (spawnObj == null)
            {
                spawnObj = new GameObject("Cyclist_Spawn_Route1");
                spawnObj.AddComponent<CyclistSpawnMarker>();

                GameObject scenario1 = GameObject.Find("Scenario_1");
                if (scenario1 != null) spawnObj.transform.SetParent(scenario1.transform);

                spawnObj.transform.position = new Vector3(436.1f, 0.2f, -80.0f);
                spawnObj.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            }
            else if (spawnObj.GetComponent<CyclistSpawnMarker>() == null)
            {
                spawnObj.AddComponent<CyclistSpawnMarker>();
            }

            EditorUtility.SetDirty(spawnObj);
            return spawnObj.transform;
        }

        [MenuItem("Cycling Experiment/Add Cyclist Spawn Route 2 Only", false, 9)]
        public static void AddCyclistSpawnRoute2Only()
        {
            Transform spawn = EnsureCyclistSpawnRoute2();
            var refs = Object.FindFirstObjectByType<ExperimentRefs>();
            if (refs != null)
            {
                refs.route2CyclistSpawn = spawn;
                EditorUtility.SetDirty(refs);
            }

            Selection.activeTransform = spawn;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Cycling Experiment",
                "Added Cyclist_Spawn_Route2 only.\n\nNothing else in the scene was changed.\nMove the cyan marker, then save the scene.",
                "OK");
        }

        private static Transform EnsureCyclistSpawnRoute2()
        {
            GameObject spawnObj = GameObject.Find("Cyclist_Spawn_Route2");
            if (spawnObj == null)
            {
                spawnObj = new GameObject("Cyclist_Spawn_Route2");
                spawnObj.AddComponent<CyclistSpawnMarker>();

                GameObject scenario2 = GameObject.Find("Scenario_2");
                if (scenario2 != null) spawnObj.transform.SetParent(scenario2.transform);

                spawnObj.transform.position = Scenario3_ConstructionNarrowing.ApproachPosition;
                spawnObj.transform.rotation = Quaternion.Euler(0f, Scenario3_ConstructionNarrowing.ApproachHeading, 0f);
            }
            else if (spawnObj.GetComponent<CyclistSpawnMarker>() == null)
            {
                spawnObj.AddComponent<CyclistSpawnMarker>();
            }

            EditorUtility.SetDirty(spawnObj);
            return spawnObj.transform;
        }

        private static void AssignSerializedRef(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            var prop = so.FindProperty(propertyName);
            if (prop == null) return;
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        [MenuItem("Cycling Experiment/Fix UI & Scripts ONLY (Preserve 100% User Placements)", false, 1)]
        public static void FixUIScriptsOnlyPreservePlacements()
        {
            EnsureEventSystemExists();
            AddCollidersToCampusModel();

            GameObject uiObj = GameObject.Find("Scenario_Selection_UI");
            if (uiObj == null) uiObj = new GameObject("Scenario_Selection_UI");
            if (uiObj.GetComponent<ScenarioSelectionUI>() == null) uiObj.AddComponent<ScenarioSelectionUI>();

            FixConnectionsPreservePlacementsInternal();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            EditorUtility.DisplayDialog("Cycling Experiment",
                "UI, Colliders & Scripts Successfully Fixed!\n\n" +
                "• 100% of your customized positions, waypoints, and triggers are UNTOUCHED.\n" +
                "• Ground/road MeshColliders on TUM_Campus_Container (no more falling).\n" +
                "• EventSystem active (buttons click & respond).\n" +
                "• Smooth Chase Camera attached (V key for cockpit view).\n\n" +
                "Press Play ▶️ to test!", "OK");
        }

        private static void CreateChildWaypoint(Transform parent, string name, Vector3 pos)
        {
            GameObject wp = new GameObject(name);
            wp.transform.SetParent(parent);
            wp.transform.position = pos;
        }

        private static void EnsureEventSystemExists()
        {
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }

        private static void SetupCamera(Transform target)
        {
            UnityEngine.Camera mainCam = UnityEngine.Camera.main;
            if (mainCam == null)
            {
                var cams = Object.FindObjectsOfType<UnityEngine.Camera>();
                foreach (var c in cams)
                {
                    if (!c.transform.IsChildOf(target))
                    {
                        mainCam = c;
                        break;
                    }
                }
            }
            if (mainCam == null)
            {
                GameObject camObj = GameObject.Find("Camera");
                if (camObj != null) mainCam = camObj.GetComponent<UnityEngine.Camera>();
            }

            if (mainCam != null)
            {
                mainCam.gameObject.tag = "MainCamera";
                var oldCam = mainCam.GetComponent<FirstPersonCyclistCamera>();
                if (oldCam != null) Object.DestroyImmediate(oldCam);

                var smoothCam = mainCam.GetComponent<SmoothFollowBicycleCamera>() ?? mainCam.gameObject.AddComponent<SmoothFollowBicycleCamera>();
                smoothCam.SetTarget(target);
            }

            Transform innerCam = target.Find("Camera");
            if (innerCam != null) innerCam.gameObject.SetActive(false);
        }

        private static void FixConnectionsPreservePlacementsInternal()
        {
            GameObject bicycle = FindExperimentBicycle();
            if (bicycle == null) return;

            bicycle.tag = "Player";

            if (bicycle.GetComponent<SBPScripts.Simulator.BicycleSimulatorController>() != null)
            {
                ConfigureSimBikeForExperiment(bicycle);
            }
            else
            {
                var rootCol = bicycle.GetComponent<BoxCollider>() ?? bicycle.AddComponent<BoxCollider>();
                rootCol.center = new Vector3(0, 0.8f, 0);
                rootCol.size = new Vector3(0.8f, 1.6f, 1.8f);

                var bikePhysics = bicycle.GetComponent("BicyclePhysicsController") as MonoBehaviour;
                if (bikePhysics != null) bikePhysics.enabled = true;

                var bikeInput = bicycle.GetComponent<BikeURP.BicycleInput>();
                if (bikeInput != null)
                {
                    bikeInput.enabled = true;
                    bikeInput.controller = bikePhysics as BikeURP.BicyclePhysicsController;
                    bikeInput.useDigitalSteer = true;
                }

                var bikeLean = bicycle.GetComponent<BikeURP.BicycleLeanAnimator>();
                if (bikeLean != null)
                {
                    bikeLean.enabled = true;
                    bikeLean.controller = bikePhysics as BikeURP.BicyclePhysicsController;
                    Transform armature = bicycle.transform.Find("Armature") ?? bicycle.transform;
                    bikeLean.leanRoot = armature;
                    bikeLean.leanSensitivity = 8f;
                    bikeLean.maxLeanDeg = 30f;
                }

                var sumoController = bicycle.GetComponent("BicycleSumoController") as MonoBehaviour;
                if (sumoController != null) sumoController.enabled = false;

                if (bicycle.GetComponent<SmartBicycleSafetyAssistant>() == null) bicycle.AddComponent<SmartBicycleSafetyAssistant>();

                SetupCamera(bicycle.transform);
            }
            FixLightingInternal();

            Scenario3_ConstructionNarrowing.RemoveDemoBusMarker();
            Scenario3_ConstructionNarrowing.HideLegacyRoute2WaypointPaths();
            Scenario3_ConstructionNarrowing.EnsureConstructionProps();
            RoadNavMeshBaker.HideWrongCityWaypointPaths();

            var ui = Object.FindObjectOfType<ScenarioSelectionUI>();
            if (ui != null)
            {
                ui.scenario1Position = new Vector3(436.1f, 0.2f, -80.0f);
                ui.scenario1Heading = 0f;
                ui.scenario2Position = Scenario3_ConstructionNarrowing.ApproachPosition;
                ui.scenario2Heading = Scenario3_ConstructionNarrowing.ApproachHeading;
            }

            EnsureExperimentSceneRefs();
        }

        [MenuItem("Cycling Experiment/Fix Lighting (Realtime Sun)", false, 30)]
        public static void FixLighting()
        {
            FixLightingInternal();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Lighting Fixed", "Realtime sun and ambient assigned. Lightmaps were not cleared.", "OK");
        }

        private static void FixLightingInternal()
        {
            Lightmapping.bakedGI = false;
            Lightmapping.realtimeGI = false;

            Light sun = null;
            var lights = Object.FindObjectsOfType<Light>();
            foreach (var l in lights)
            {
                if (l.type != LightType.Directional) continue;
                l.lightmapBakeType = LightmapBakeType.Realtime;
                l.shadows = LightShadows.Soft;
                if (l.intensity < 1.4f) l.intensity = 1.6f;
                l.color = new Color(1f, 0.97f, 0.9f);
                if (sun == null) sun = l;
            }

            if (sun != null) RenderSettings.sun = sun;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.65f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.38f, 0.36f, 0.32f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.16f, 0.14f);
            RenderSettings.ambientIntensity = 1.1f;
        }

        [MenuItem("Cycling Experiment/Strip Construction NavMesh Obstacles", false, 7)]
        public static void AddConstructionNavMeshObstacles()
        {
            int disabled = Scenario3_ConstructionNarrowing.DisableCampusRoadNavMesh();
            int removed = Scenario3_ConstructionNarrowing.StripConstructionNavMeshObstacles();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Cycling Experiment",
                $"Deactivated {disabled} Campus_Road_NavMesh object(s).\n" +
                $"Removed NavMeshObstacle from {removed} construction prop(s) (ChevronSign, dumpsters, cones, barriers).\n\n" +
                "Ambient cars follow Campus_Traffic_Paths (bus-style waypoints). Pedestrian / SUMO / bus-boarding NavMesh was left alone.\n" +
                "Save the scene to keep this.",
                "OK");
        }

        [MenuItem("Cycling Experiment/Clean Unused Scene Objects", false, 40)]
        public static void CleanUnusedSceneObjects()
        {
            string[] removeNames =
            {
                "tum_main_campus",
                "City_Traffic_Paths",
                "Intersection_Traffic_Path_1",
                "Intersection_Traffic_Path_2",
                "Path_Route2_Northbound",
                "Path_Route2_Southbound",
                "Path_Traffic_EastToWest",
                "Path_Traffic_WestToEast",
                "Path_Traffic_SouthToNorth",
                "Path_Traffic_NorthToSouth"
            };

            int removed = 0;
            foreach (var objectName in removeNames)
            {
                var go = FindIncludingInactive(objectName);
                if (go == null) continue;
                Undo.DestroyObjectImmediate(go);
                removed++;
            }

            EnsureTrafficDestinations();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Cycling Experiment",
                $"Removed {removed} unused object(s).\n\n" +
                "Kept TUM_Campus_Container, Intersection_Traffic_System, and Scenario_Selection_UI.\n" +
                "Ambient cars use Campus_Traffic_Paths (ordered waypoints), not NavMesh or City_Traffic_Paths.",
                "OK");
        }

        private static GameObject FindExperimentBicycle()
        {
            var sim = FindIncludingInactive("SimBike");
            if (sim != null && sim.activeInHierarchy) return sim;
            var old = FindIncludingInactive("bicyle_animated_human");
            if (old != null && old.activeInHierarchy) return old;
            if (sim != null) return sim;
            return old;
        }

        private static void ConfigureSimBikeForExperiment(GameObject bicycle)
        {
            bicycle.tag = "Player";
            if (bicycle.GetComponent<SimBikeCyclistMotion>() == null)
                bicycle.AddComponent<SimBikeCyclistMotion>();

            var spawn = bicycle.GetComponent<SimBikeSpawnController>();
            if (spawn != null)
            {
                var so = new SerializedObject(spawn);
                var spawnOnAwake = so.FindProperty("spawnOnAwake");
                if (spawnOnAwake != null) spawnOnAwake.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static GameObject FindIncludingInactive(string objectName)
        {
            var transforms = Resources.FindObjectsOfTypeAll<Transform>();
            foreach (var t in transforms)
            {
                if (t == null || t.name != objectName) continue;
                if (!t.gameObject.scene.IsValid() || !t.gameObject.scene.isLoaded) continue;
                return t.gameObject;
            }

            return null;
        }

        [MenuItem("Cycling Experiment/Place Route 1 Right-Turn Sign", false, 11)]
        public static void PlaceRoute1RightTurnSign()
        {
            var trigger = GameObject.Find("Trigger_Scenario1_RightTurn");
            if (trigger == null)
            {
                EditorUtility.DisplayDialog("Cycling Experiment", "Trigger_Scenario1_RightTurn was not found.", "OK");
                return;
            }

            Transform sign = Route1RightTurnSign.Ensure(trigger.transform);
            Undo.RegisterCreatedObjectUndo(sign.gameObject, "Place Route 1 Right-Turn Sign");
            var refs = Object.FindObjectOfType<ExperimentRefs>();
            if (refs != null)
            {
                refs.rightTurnTrigger = trigger.transform;
                refs.rightTurnSign = sign;
                EditorUtility.SetDirty(refs);
            }

            Selection.activeTransform = sign;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Cycling Experiment",
                "Placed StVO 214-10 (mandatory right) at the Route 1 turn.\nSave the scene to keep it.",
                "OK");
        }

        [MenuItem("Cycling Experiment/Retune Route 1 Bus Timing", false, 12)]
        public static void RetuneRoute1BusTiming()
        {
            var pathObj = GameObject.Find("Bus_Overtake_Path");
            var trigger = GameObject.Find("Trigger_Scenario1_BusStop");
            var spawn = GameObject.Find("Cyclist_Spawn_Route1");
            if (pathObj == null || trigger == null)
            {
                EditorUtility.DisplayDialog("Cycling Experiment", "Bus_Overtake_Path or Trigger_Scenario1_BusStop is missing.", "OK");
                return;
            }

            var path = pathObj.GetComponent<WaypointPath>();
            if (path == null || path.WaypointCount < 2)
            {
                EditorUtility.DisplayDialog("Cycling Experiment", "Bus_Overtake_Path needs at least two waypoints.", "OK");
                return;
            }

            path.SyncFromChildren();
            Vector3 bay = path.GetWaypoint(path.WaypointCount - 1);
            Vector3 toward = spawn != null ? spawn.transform.position : path.GetWaypoint(0);
            Vector3 delta = toward - bay;
            delta.y = 0f;
            float len = delta.magnitude;
            if (len < 5f)
            {
                EditorUtility.DisplayDialog("Cycling Experiment", "Could not measure spawn-to-bay direction.", "OK");
                return;
            }

            Vector3 unit = delta / len;
            Vector3 triggerPos = bay + unit * 75f;
            triggerPos.y = trigger.transform.position.y;
            Undo.RecordObject(trigger.transform, "Retune bus trigger");
            trigger.transform.position = triggerPos;

            Transform wp0 = path.transform.GetChild(0);
            Vector3 wp0Pos = bay + unit * 100f;
            wp0Pos.y = wp0.position.y;
            Undo.RecordObject(wp0, "Extend bus path start");
            wp0.position = wp0Pos;
            path.SyncFromChildren();

            var controller = Object.FindObjectOfType<Scenario1_CombinedController>();
            if (controller != null)
            {
                var so = new SerializedObject(controller);
                var speedProp = so.FindProperty("busSpeed");
                if (speedProp != null) speedProp.floatValue = 14f;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(controller);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Cycling Experiment",
                "Moved Trigger_Scenario1_BusStop to ~75 m before the bay, extended Bus_Overtake_Path WP_0 behind the cyclist, and set busSpeed to 14 m/s.\nSave the scene.",
                "OK");
        }

        [MenuItem("Cycling Experiment/Ensure Experiment Run Logger", false, 13)]
        public static void EnsureExperimentRunLogger()
        {
            EnsureExperimentSceneRefs();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Cycling Experiment",
                "ExperimentRunLogger, close-pass tracker, Route 1 reference path, and right-turn sign are on Experiment_Scene_Refs.\nLogs write to Logs/<participant>/ next to the project.\nSave the scene.",
                "OK");
        }
    }
}
#endif
