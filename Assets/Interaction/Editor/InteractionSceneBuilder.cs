using System.Text;
using OfficeTraining.Environment;
using OfficeTraining.Interaction.Demo;
using OfficeTraining.Interaction.Integration;
using OfficeTraining.Scenario;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace OfficeTraining.Interaction.Editor
{
    /// <summary>
    /// Builds the interaction module's scene content so it can be re-created after merges:
    /// the stand-alone InteractionTestScene and the XR rig/props in OfficeBlockout.
    /// Menu: Office Training > Interaction.
    /// </summary>
    public static class InteractionSceneBuilder
    {
        private const string Root = "Assets/Interaction";
        private const string TestScenePath = Root + "/Scenes/InteractionTestScene.unity";
        private const string OfficeScenePath = "Assets/Scenes/OfficeBlockout.unity";
        private const string IntegratedScenePath = "Assets/Scenes/OfficeTraining.unity";
        private const string EventsPath = Root + "/InteractionEvents.asset";
        private const string RigPrefabPath = "Assets/VRTemplateAssets/Prefabs/Setup/Complete XR Origin Set Up Variant.prefab";
        private const string SprayMaterialPath = "Assets/EnvironmentBlockout/IncidentPresentation/PrototypeSmoke.mat";
        private const string RigName = "XR Origin (Student 1 rig)";
        private const string OfficeModuleName = "Interaction Module (Student 1)";

        [MenuItem("Office Training/Interaction/Build Interaction Test Scene")]
        public static void BuildTestScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EnsureFolder(Root + "/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            // Load after opening the scene: a Single-mode scene load unloads unreferenced assets.
            InteractionEvents events = LoadOrCreateEvents();
            Object.DestroyImmediate(GameObject.Find("Main Camera"));

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor (teleport area)";
            floor.GetComponent<Renderer>().sharedMaterial = Mat("Floor", new Color(0.45f, 0.47f, 0.5f));
            AddTeleportArea(floor);

            CreateRig(new Vector3(0f, 0f, -1.6f), 0f);
            new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();

            Box("Table", null, new Vector3(0f, 0.4f, -0.6f), new Vector3(1.6f, 0.8f, 0.6f), Mat("Table", new Color(0.55f, 0.42f, 0.3f)), true);
            CreateExtinguisher(null, new Vector3(0f, 0.8f, -0.6f), Quaternion.identity, events, armedOnStart: true);

            var target = CreatePracticeTarget(null, new Vector3(0f, 0f, 2.2f), new Vector3(0.6f, 0.8f, 0.6f));
            Box("Placeholder fire (visual only)", target.transform, new Vector3(0f, 0.3f, 2.2f), new Vector3(0.4f, 0.6f, 0.4f), Mat("PlaceholderFire", new Color(1f, 0.45f, 0.1f)), false);

            var inspect = Box("Inspect me", null, new Vector3(-0.55f, 0.9f, -0.6f), new Vector3(0.2f, 0.2f, 0.2f), Mat("Inspect", new Color(0.95f, 0.8f, 0.2f)), true);
            AddInspectTarget<InspectTarget>(inspect, events, "test_target");
            Label(inspect.transform, "INSPECT", new Vector3(0f, 0.2f, 0f));

            var reset = Box("Reset", null, new Vector3(0.55f, 0.9f, -0.6f), new Vector3(0.2f, 0.2f, 0.2f), Mat("Reset", new Color(0.2f, 0.45f, 0.95f)), true);
            AddInspectTarget<InspectTarget>(reset, events, "reset");
            Label(reset.transform, "RESET", new Vector3(0f, 0.2f, 0f));

            var zone = CreateSafeZone(null, new Vector3(3f, 0f, 0f), new Vector3(2f, 2.5f, 2f), events);
            var zoneVisual = GameObject.CreatePrimitive(PrimitiveType.Quad);
            zoneVisual.name = "Zone marker (visual only)";
            Object.DestroyImmediate(zoneVisual.GetComponent<Collider>());
            zoneVisual.transform.SetParent(zone.transform, false);
            zoneVisual.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            zoneVisual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            zoneVisual.transform.localScale = new Vector3(2f, 2f, 1f);
            zoneVisual.GetComponent<Renderer>().sharedMaterial = Mat("Zone", new Color(0.2f, 0.8f, 0.35f));
            Label(zone.transform, "TEST ZONE", new Vector3(0f, 1.6f, 0f));

            CreateInputSetup(null, events);

            var boardObject = new GameObject("Event board");
            boardObject.transform.SetPositionAndRotation(new Vector3(-1.8f, 1.5f, 0.4f), Quaternion.Euler(0f, -35f, 0f));
            var board = boardObject.AddComponent<TextMeshPro>();
            board.fontSize = 1.2f;
            board.rectTransform.sizeDelta = new Vector2(2.2f, 1.6f);
            board.alignment = TextAlignmentOptions.TopLeft;
            board.text = "Interaction test";
            var harness = new GameObject("Interaction Test Harness").AddComponent<InteractionTestHarness>();
            SetRef(harness, "events", events);
            SetRef(harness, "board", board);

            EditorSceneManager.SaveScene(scene, TestScenePath);
            Debug.Log("[Interaction] Built " + TestScenePath);
        }

        /// <summary>
        /// Copies Student 2's OfficeBlockout scene (left untouched, its tests expect no coordinator)
        /// to OfficeTraining.unity and adds the XR rig, props, Student 3's coordinator and the
        /// bridge. Re-run after the environment scene changes to pick up the new version.
        /// </summary>
        [MenuItem("Office Training/Interaction/Build Integrated Office Training Scene")]
        public static void BuildIntegratedOfficeScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(IntegratedScenePath) != null)
                AssetDatabase.DeleteAsset(IntegratedScenePath);
            if (!AssetDatabase.CopyAsset(OfficeScenePath, IntegratedScenePath))
            {
                Debug.LogError("[Interaction] Could not copy " + OfficeScenePath);
                return;
            }
            var scene = EditorSceneManager.OpenScene(IntegratedScenePath, OpenSceneMode.Single);
            InteractionEvents events = LoadOrCreateEvents();
            var report = new StringBuilder("[Interaction] Built " + IntegratedScenePath + " from " + OfficeScenePath + ":\n");

            var old = GameObject.Find(OfficeModuleName);
            if (old != null)
                Object.DestroyImmediate(old);
            var oldRig = GameObject.Find(RigName);
            if (oldRig != null)
                Object.DestroyImmediate(oldRig);

            var module = new GameObject(OfficeModuleName);

            // Rig at the environment's suggested start, replacing the desktop preview camera.
            Transform start = FindTransform("SuggestedPlayerStart");
            CreateRig(start != null ? start.position : new Vector3(2.5f, 0f, -2f), start != null ? start.eulerAngles.y : 0f);
            var preview = GameObject.Find("DesktopPreview_ReplaceWithStudent1Rig");
            if (preview != null)
            {
                preview.SetActive(false);
                report.AppendLine("- Disabled DesktopPreview_ReplaceWithStudent1Rig (kept for the environment debug panel).");
            }
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>().transform.SetParent(module.transform);

            foreach (string floorName in new[] { "OfficeFloor", "CorridorFloor", "AssemblyFloor" })
            {
                Transform floor = FindTransform(floorName);
                if (floor == null) { report.AppendLine("- MISSING floor " + floorName); continue; }
                AddTeleportArea(floor.gameObject);
                report.AppendLine("- Teleport area on " + floorName);
            }

            // Hazard selection volumes (the hazard props have no colliders of their own).
            foreach (HazardObject hazard in Object.FindObjectsByType<HazardObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Transform existing = hazard.transform.Find("Inspect volume (Student 1)");
                if (existing != null)
                    Object.DestroyImmediate(existing.gameObject);
                Bounds bounds = HazardSelectionBounds(hazard);
                var volume = new GameObject("Inspect volume (Student 1)");
                volume.transform.SetParent(hazard.transform, false);
                volume.transform.position = bounds.center;
                volume.transform.rotation = Quaternion.identity;
                var box = volume.AddComponent<BoxCollider>();
                box.size = bounds.size;
                AddInspectTarget<HazardInspectable>(volume, events, null);
                report.AppendLine("- Inspect volume for '" + hazard.HazardId + "' at " + bounds.center.ToString("F2") + " size " + bounds.size.ToString("F2"));
            }

            // Extinguisher on a small stand inside the office, next to the doorway.
            Vector3 standPosition = new Vector3(3.55f, 0f, 2.55f);
            Box("Extinguisher stand", module.transform, standPosition + new Vector3(0f, 0.3f, 0f), new Vector3(0.3f, 0.6f, 0.3f), Mat("Stand", new Color(0.3f, 0.3f, 0.32f)), true);
            CreateExtinguisher(module.transform, standPosition + new Vector3(0f, 0.6f, 0f), Quaternion.Euler(0f, 180f, 0f), events, armedOnStart: false);
            Collider[] overlaps = Physics.OverlapBox(standPosition + new Vector3(0f, 0.6f, 0f), new Vector3(0.2f, 0.6f, 0.2f));
            foreach (Collider overlap in overlaps)
                if (!overlap.transform.IsChildOf(module.transform))
                    report.AppendLine("- NOTE extinguisher area overlaps " + overlap.name);

            // Practice target around the incident presentation (heater).
            Transform fire = FindTransform("Incident Presentation");
            Vector3 firePosition = fire != null ? fire.position : new Vector3(-3.29f, 1.13f, -0.7f);
            CreatePracticeTarget(module.transform, firePosition + new Vector3(0f, -0.15f, 0f), new Vector3(0.5f, 0.6f, 0.5f));

            // Assembly point arrival.
            Transform assembly = FindTransform("AssemblyPoint");
            CreateSafeZone(module.transform, assembly != null ? assembly.position : new Vector3(2.5f, 0f, 12f), new Vector3(2f, 2.5f, 2f), events);

            CreateInputSetup(module.transform, events);

            // Scenario integration (Student 3's coordinator, environment references from Student 2).
            var coordinator = Object.FindAnyObjectByType<ScenarioCoordinator>();
            if (coordinator == null)
            {
                coordinator = new GameObject("Scenario Coordinator").AddComponent<ScenarioCoordinator>();
                SetRef(coordinator, "hazardRegistry", Object.FindAnyObjectByType<HazardRegistry>());
                SetRef(coordinator, "incidentController", Object.FindAnyObjectByType<IncidentController>());
                report.AppendLine("- Added Scenario Coordinator wired to HazardRegistry and IncidentController");
            }
            var bridge = module.AddComponent<ScenarioInteractionBridge>();
            SetRef(bridge, "coordinator", coordinator);
            SetRef(bridge, "events", events);
            var serializedBridge = new SerializedObject(bridge);
            serializedBridge.FindProperty("startTrainingOnPlay").boolValue = true;
            serializedBridge.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(report.ToString());
        }

        // ------------------------------------------------------------ builders

        private static GameObject CreateRig(Vector3 position, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefabPath);
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            rig.name = RigName;
            rig.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            // Comfort: teleport + snap turn only (no smooth motion or smooth turn on either hand).
            foreach (MonoBehaviour behaviour in rig.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour.GetType().Name != "ControllerInputActionManager")
                    continue;
                var serialized = new SerializedObject(behaviour);
                SetBool(serialized, "m_SmoothMotionEnabled", false);
                SetBool(serialized, "m_SmoothTurnEnabled", false);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            return rig;
        }

        private static GameObject CreateExtinguisher(Transform parent, Vector3 position, Quaternion rotation, InteractionEvents events, bool armedOnStart)
        {
            var root = new GameObject("Extinguisher (grabbable)");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, rotation);

            Material red = Mat("ExtinguisherRed", new Color(0.8f, 0.08f, 0.06f));
            Material black = Mat("ExtinguisherBlack", new Color(0.08f, 0.08f, 0.08f));
            var body = Primitive(PrimitiveType.Cylinder, "Body", root.transform, new Vector3(0f, 0.2f, 0f), Quaternion.identity, new Vector3(0.13f, 0.2f, 0.13f), red, keepCollider: true);
            Primitive(PrimitiveType.Cylinder, "Valve", root.transform, new Vector3(0f, 0.425f, 0f), Quaternion.identity, new Vector3(0.05f, 0.025f, 0.05f), black, keepCollider: false);
            Primitive(PrimitiveType.Cube, "Handle", root.transform, new Vector3(0f, 0.47f, -0.02f), Quaternion.identity, new Vector3(0.03f, 0.025f, 0.13f), black, keepCollider: false);
            Primitive(PrimitiveType.Cylinder, "Hose", root.transform, new Vector3(0f, 0.44f, 0.09f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.022f, 0.06f, 0.022f), black, keepCollider: false);

            var grip = new GameObject("Grip (attach)").transform;
            grip.SetParent(root.transform, false);
            grip.localPosition = new Vector3(0f, 0.47f, -0.02f);
            var nozzle = new GameObject("Nozzle").transform;
            nozzle.SetParent(root.transform, false);
            nozzle.localPosition = new Vector3(0f, 0.44f, 0.15f);

            var line = nozzle.gameObject.AddComponent<LineRenderer>();
            line.enabled = false;
            line.widthMultiplier = 0.01f;
            line.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { name = "AimLine" };
            line.sharedMaterial = SaveMaterial(line.sharedMaterial, "AimLine");

            var sprayObject = new GameObject("Spray (visual only)");
            sprayObject.transform.SetParent(nozzle, false);
            var spray = sprayObject.AddComponent<ParticleSystem>();
            spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = spray.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = 0.5f;
            main.startSpeed = 5f;
            main.startSize = 0.07f;
            main.startColor = new Color(0.95f, 0.95f, 0.95f, 0.7f);
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = spray.emission;
            emission.rateOverTime = 80f;
            var shape = spray.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 7f;
            shape.radius = 0.01f;
            var sprayMaterial = AssetDatabase.LoadAssetAtPath<Material>(SprayMaterialPath);
            if (sprayMaterial != null)
                sprayObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = sprayMaterial;

            var bodyRb = root.AddComponent<Rigidbody>();
            bodyRb.mass = 2f;
            bodyRb.interpolation = RigidbodyInterpolation.Interpolate;
            var grab = root.AddComponent<XRGrabInteractable>();
            grab.attachTransform = grip;
            grab.throwOnDetach = false;
            grab.colliders.Clear();
            grab.colliders.Add(body.GetComponent<Collider>());

            var tool = root.AddComponent<ToolInteractionController>();
            SetString(tool, "itemId", ScenarioRules.DefaultExtinguisherItemId);
            SetRef(tool, "events", events);
            SetRef(tool, "usePoint", nozzle);

            var practice = root.AddComponent<ExtinguisherPractice>();
            SetRef(practice, "events", events);
            SetRef(practice, "nozzle", nozzle);
            SetRef(practice, "sprayEffect", spray);
            SetRef(practice, "aimLine", line);
            var serialized = new SerializedObject(practice);
            serialized.FindProperty("armedOnStart").boolValue = armedOnStart;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static PracticeTarget CreatePracticeTarget(Transform parent, Vector3 position, Vector3 size)
        {
            var root = new GameObject("Practice Target (aim volume)");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            box.center = new Vector3(0f, size.y * 0.5f, 0f);

            var aimed = Primitive(PrimitiveType.Cylinder, "Aimed ring", root.transform, new Vector3(0f, 0.01f, 0f), Quaternion.identity,
                new Vector3(size.x * 1.3f, 0.005f, size.z * 1.3f), Mat("AimedYellow", new Color(1f, 0.85f, 0.1f)), keepCollider: false);
            var completed = Primitive(PrimitiveType.Cylinder, "Completed ring", root.transform, new Vector3(0f, 0.012f, 0f), Quaternion.identity,
                new Vector3(size.x * 1.3f, 0.005f, size.z * 1.3f), Mat("CompletedGreen", new Color(0.15f, 0.85f, 0.3f)), keepCollider: false);
            var target = root.AddComponent<PracticeTarget>();
            SetRef(target, "aimedIndicator", aimed);
            SetRef(target, "completedIndicator", completed);
            aimed.SetActive(false);
            completed.SetActive(false);
            return target;
        }

        private static SafeZoneTrigger CreateSafeZone(Transform parent, Vector3 position, Vector3 size, InteractionEvents events)
        {
            var zone = new GameObject("Assembly Point Trigger").AddComponent<SafeZoneTrigger>();
            zone.transform.SetParent(parent, false);
            zone.transform.position = position;
            SetRef(zone, "events", events);
            var serialized = new SerializedObject(zone);
            serialized.FindProperty("size").vector3Value = size;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return zone;
        }

        private static void CreateInputSetup(Transform parent, InteractionEvents events)
        {
            var input = new GameObject("XR Input Setup").AddComponent<XRInputSetup>();
            input.transform.SetParent(parent, false);
            SetRef(input, "events", events);
        }

        private static T AddInspectTarget<T>(GameObject target, InteractionEvents events, string id) where T : InspectTarget
        {
            target.AddComponent<XRSimpleInteractable>();
            var inspect = target.AddComponent<T>();
            SetRef(inspect, "events", events);
            if (id != null)
                SetString(inspect, "targetId", id);
            return inspect;
        }

        private static void AddTeleportArea(GameObject floor)
        {
            var area = floor.GetComponent<TeleportationArea>();
            if (area == null)
                area = floor.AddComponent<TeleportationArea>();
            area.interactionLayers = InteractionLayerMask.GetMask("Teleport");
        }

        /// <summary>Selection volume from the hazard's confirmation outline (falls back to its renderers).</summary>
        private static Bounds HazardSelectionBounds(HazardObject hazard)
        {
            Transform outline = null;
            foreach (Transform child in hazard.transform)
                if (child.name.StartsWith("Confirmation outline"))
                    outline = child;

            bool wasActive = outline != null && outline.gameObject.activeSelf;
            if (outline != null)
                outline.gameObject.SetActive(true);
            Renderer[] renderers = outline != null ? outline.GetComponentsInChildren<Renderer>() : hazard.GetComponentsInChildren<Renderer>();
            var bounds = new Bounds(hazard.transform.position, Vector3.zero);
            bool first = true;
            foreach (Renderer renderer in renderers)
            {
                if (first) { bounds = renderer.bounds; first = false; }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (outline != null)
                outline.gameObject.SetActive(wasActive);

            // Outline lies flat around the prop: extend upwards to cover it and pad for easy pointing.
            float bottom = bounds.min.y - 0.03f;
            float top = bounds.max.y + 0.35f;
            Vector3 size = new Vector3(Mathf.Max(0.3f, bounds.size.x + 0.08f), top - bottom, Mathf.Max(0.3f, bounds.size.z + 0.08f));
            return new Bounds(new Vector3(bounds.center.x, (top + bottom) * 0.5f, bounds.center.z), size);
        }

        // ------------------------------------------------------------ helpers

        private static InteractionEvents LoadOrCreateEvents()
        {
            var events = AssetDatabase.LoadAssetAtPath<InteractionEvents>(EventsPath);
            if (events != null)
                return events;
            events = ScriptableObject.CreateInstance<InteractionEvents>();
            AssetDatabase.CreateAsset(events, EventsPath);
            AssetDatabase.SaveAssets();
            return events;
        }

        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material, bool keepCollider) =>
            Primitive(PrimitiveType.Cube, name, parent, Vector3.zero, Quaternion.identity, size, material, keepCollider, position);

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Quaternion localRotation,
            Vector3 scale, Material material, bool keepCollider, Vector3? worldPosition = null)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            if (!keepCollider)
                Object.DestroyImmediate(obj.GetComponent<Collider>());
            obj.transform.SetParent(parent, false);
            if (worldPosition.HasValue)
                obj.transform.position = worldPosition.Value;
            else
                obj.transform.localPosition = localPosition;
            obj.transform.localRotation = localRotation;
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            return obj;
        }

        private static void Label(Transform parent, string text, Vector3 localOffset)
        {
            var label = new GameObject(text + " label").AddComponent<TextMeshPro>();
            label.transform.SetParent(parent, false);
            label.transform.localPosition = new Vector3(localOffset.x / parent.lossyScale.x, localOffset.y / parent.lossyScale.y, localOffset.z / parent.lossyScale.z);
            label.transform.rotation = Quaternion.identity; // readable from the rig, which faces +Z
            label.transform.localScale = new Vector3(1f / parent.lossyScale.x, 1f / parent.lossyScale.y, 1f / parent.lossyScale.z);
            label.text = text;
            label.fontSize = 1f;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(1f, 0.3f);
        }

        private static Material Mat(string name, Color color)
        {
            EnsureFolder(Root + "/Materials");
            string path = Root + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material SaveMaterial(Material material, string name)
        {
            EnsureFolder(Root + "/Materials");
            string path = Root + "/Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Transform FindTransform(string name)
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == name)
                    return t;
            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) { Debug.LogError("[Interaction] Missing field " + field + " on " + target); return; }
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(Object target, string field, string value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(SerializedObject serialized, string field, bool value)
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property != null)
                property.boolValue = value;
            else
                Debug.LogWarning("[Interaction] Rig field not found: " + field);
        }
    }
}
