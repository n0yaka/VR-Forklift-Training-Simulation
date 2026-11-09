using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using VRConTrain.Forklift;
using VRConTrain.Interaction;

namespace VRConTrain.EditorTools
{
    /// <summary>
    /// Builds the ride test in the active scene: ground, light, forklift physics, steering wheel, mast levers and a
    /// seated XR rig.
    /// Safe to run again; anything it already created is reused.
    /// </summary>
    public static class RideTestSceneBuilder
    {
        const string k_RigPrefabPath = "Assets/Samples/XR Interaction Toolkit/3.4.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        const string k_ControlsPath = "Assets/_Project/Input/ForkliftControls.inputactions";
        const string k_TexturesFolder = "Assets/_Project/Art/Textures";
        const string k_MaterialsFolder = "Assets/_Project/Art/Materials";
        const string k_GroundTexturePath = k_TexturesFolder + "/GroundChecker.png";
        const string k_GroundMaterialPath = k_MaterialsFolder + "/Ground.mat";

        const float k_WheelRadius = 0.32f;
        const float k_WheelX = 0.605f;
        const float k_FrontAxleZ = 0.600f;
        const float k_RearAxleZ = -1.762f;
        const float k_WheelCenterY = 0.318f;
        const float k_SuspensionDistance = 0.06f;
        const float k_SteeringGrabRadius = 0.27f;
        // Measured from the mesh: the rim tube spans 0.18 to 0.24 m from the hub.
        const float k_SteeringRimRadius = 0.21f;
        // The levers are about 10 cm apart, so each grab sphere stays within its own lever's knob.
        const float k_LeverGrabRadius = 0.045f;
        const float k_LeverKnobLength = 0.08f;

        static readonly Vector3 s_SteeringAxisForkliftSpace = new Vector3(0f, 0.3746f, -0.9272f);
        static readonly Vector3 s_EyePoint = new Vector3(0f, 1.93f, -0.30f);
        static readonly Vector3 s_GearReadoutPosition = new Vector3(0f, 1.8f, 0.3f);

        /// <summary>Builds or updates the ride test in the active scene.</summary>
        [MenuItem("VRConTrain/Setup Ride Test Scene")]
        public static void Build()
        {
            var scene = SceneManager.GetActiveScene();
            var forklift = FindRoot(scene, "Forklift");
            if (forklift == null)
            {
                Debug.LogError("Setup Ride Test Scene: the active scene has no root object called Forklift.");
                return;
            }

            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Ride Test Scene");

            // The warehouse has its own floor, so the test ground is only needed without it.
            if (FindRoot(scene, WarehouseSceneBuilder.k_RootName) == null)
                BuildGround(scene);

            BuildLight(scene);
            GetOrAdd<XRInteractionManager>(GetOrCreateRoot(scene, "XR Interaction Manager"));

            var body = GetOrAdd<Rigidbody>(forklift);
            body.mass = 3000f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearDamping = 0f;
            body.angularDamping = 0.5f;

            var physicsRoot = GetOrCreateChild(forklift.transform, "Physics");
            var chassis = GetOrAdd<BoxCollider>(GetOrCreateChild(physicsRoot, "Chassis Collider").gameObject);
            chassis.center = new Vector3(0f, 0.75f, -0.69f);
            chassis.size = new Vector3(1.6f, 0.9f, 3.03f);

            var frontLeft = BuildWheel(physicsRoot, "Wheel Front Left", -k_WheelX, k_FrontAxleZ);
            var frontRight = BuildWheel(physicsRoot, "Wheel Front Right", k_WheelX, k_FrontAxleZ);
            var rearLeft = BuildWheel(physicsRoot, "Wheel Rear Left", -k_WheelX, k_RearAxleZ);
            var rearRight = BuildWheel(physicsRoot, "Wheel Rear Right", k_WheelX, k_RearAxleZ);

            var controller = GetOrAdd<ForkliftController>(forklift);
            SetFields(controller,
                ("_frontLeft", frontLeft), ("_frontRight", frontRight), ("_rearLeft", rearLeft), ("_rearRight", rearRight),
                ("_frontWheelsVisual", Require(forklift.transform, "Tire Front")),
                ("_rearLeftVisual", Require(forklift.transform, "Tire Back Left")),
                ("_rearRightVisual", Require(forklift.transform, "Tire Back Right")));

            var steering = BuildSteeringWheel(forklift.transform, Require(forklift.transform, "Steering"));

            var controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>(k_ControlsPath);
            var input = GetOrAdd<ControllerButtonInput>(forklift);
            SetFields(input, ("_controls", controls));

            var mast = BuildMast(forklift);
            BuildMastColliders(forklift.transform);
            var liftLever = BuildLever(forklift.transform, Require(forklift.transform, "Elevetor Raise"), false);
            var tiltLever = BuildLever(forklift.transform, Require(forklift.transform, "Elevator Tilt"), false);
            // Pull shifts the forks left and push shifts them right, so the side-shift lever's output is flipped.
            var sideShiftLever = BuildLever(forklift.transform, Require(forklift.transform, "Elevator Left/Right"), true);

            var router = GetOrAdd<ForkliftInputRouter>(forklift);
            SetFields(router, ("_controller", controller), ("_inputSource", input), ("_steering", steering),
                ("_mast", mast), ("_liftLever", liftLever), ("_tiltLever", tiltLever), ("_sideShiftLever", sideShiftLever));

            BuildGearReadout(forklift.transform, controller);
            BuildSeatedRig(forklift.transform, input);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Setup Ride Test Scene: done. Save the scene to keep the changes.");
        }

        static void BuildGround(Scene scene)
        {
            var ground = FindRoot(scene, "Ground");
            if (ground == null)
            {
                ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Ground";
                Undo.RegisterCreatedObjectUndo(ground, "Create ground");
            }

            ground.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            ground.transform.localScale = new Vector3(20f, 1f, 20f);
            ground.isStatic = true;
            ground.GetComponent<MeshRenderer>().sharedMaterial = GetGroundMaterial();
        }

        static Material GetGroundMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(k_GroundMaterialPath);
            if (material != null)
                return material;

            System.IO.Directory.CreateDirectory(k_TexturesFolder);
            System.IO.Directory.CreateDirectory(k_MaterialsFolder);

            const int size = 256;
            var pixels = new Color32[size * size];
            var light = new Color32(150, 155, 160, 255);
            var dark = new Color32(105, 110, 115, 255);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                    pixels[y * size + x] = (x < size / 2) == (y < size / 2) ? light : dark;
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
            texture.SetPixels32(pixels);
            texture.Apply();
            System.IO.File.WriteAllBytes(k_GroundTexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(k_GroundTexturePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(k_GroundTexturePath);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.SaveAndReimport();

            // The plane is 200 m across and each texture repeat covers two 2 m squares, so 50 repeats.
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(k_GroundTexturePath));
            material.SetTextureScale("_BaseMap", new Vector2(50f, 50f));
            material.SetFloat("_Smoothness", 0f);
            AssetDatabase.CreateAsset(material, k_GroundMaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        static void BuildLight(Scene scene)
        {
            var light = GetOrAdd<Light>(GetOrCreateRoot(scene, "Directional Light"));
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.shadows = LightShadows.None;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        static WheelCollider BuildWheel(Transform parent, string name, float x, float z)
        {
            var wheel = GetOrAdd<WheelCollider>(GetOrCreateChild(parent, name).gameObject);
            wheel.transform.localPosition = new Vector3(x, 0f, z);
            wheel.mass = 40f;
            wheel.radius = k_WheelRadius;
            wheel.wheelDampingRate = 0.25f;
            wheel.suspensionDistance = k_SuspensionDistance;
            wheel.forceAppPointDistance = 0.1f;

            // The wheel rests at the middle of its travel, so this keeps it level with the tyre mesh at rest.
            wheel.center = new Vector3(0f, k_WheelCenterY + k_SuspensionDistance * 0.5f, 0f);

            var spring = wheel.suspensionSpring;
            spring.spring = 250000f;
            spring.damper = 25000f;
            spring.targetPosition = 0.5f;
            wheel.suspensionSpring = spring;
            return wheel;
        }

        static SteeringWheelInteractable BuildSteeringWheel(Transform forklift, Transform steeringTransform)
        {
            var grab = GetOrAdd<SphereCollider>(steeringTransform.gameObject);
            grab.isTrigger = true;
            grab.radius = k_SteeringGrabRadius / steeringTransform.lossyScale.x;
            grab.center = Vector3.zero;

            var steering = GetOrAdd<SteeringWheelInteractable>(steeringTransform.gameObject);
            var axis = steeringTransform.parent.InverseTransformDirection(forklift.TransformDirection(s_SteeringAxisForkliftSpace));
            var serialized = new SerializedObject(steering);
            serialized.FindProperty("_axis").vector3Value = axis.normalized;
            serialized.FindProperty("_rimRadius").floatValue = k_SteeringRimRadius;
            serialized.ApplyModifiedProperties();
            return steering;
        }

        static MastController BuildMast(GameObject forklift)
        {
            var mastTransform = Require(forklift.transform, "Crane Elevator");
            var carriage = Require(forklift.transform, "Crane");
            var mast = GetOrAdd<MastController>(forklift);
            SetFields(mast, ("_mast", mastTransform), ("_carriage", carriage));

            // The mast hinges at front axle height, under the middle of the mast channel.
            var bounds = MeshBoundsInForkliftSpace(forklift.transform, mastTransform);
            var hinge = new Vector3(bounds.center.x, k_WheelCenterY, bounds.center.z);
            var serialized = new SerializedObject(mast);
            serialized.FindProperty("_tiltHinge").vector3Value = mastTransform.parent.InverseTransformPoint(forklift.transform.TransformPoint(hinge));
            serialized.FindProperty("_tiltAxis").vector3Value = mastTransform.parent.InverseTransformDirection(forklift.transform.TransformDirection(Vector3.left));
            serialized.ApplyModifiedProperties();
            return mast;
        }

        /// <summary>
        /// Adds box colliders to the mast and the fork carriage so they can't pass through racks, walls or loads.
        /// They are children of the moving parts, so they follow lift, tilt and side-shift. Sizes are measured from
        /// the model, in forklift space at rest.
        /// </summary>
        static void BuildMastColliders(Transform forklift)
        {
            // The mast stops 10 cm above the floor so it can't scrape the ground.
            BuildBoxColliders(forklift, Require(forklift, "Crane Elevator"), "Mast Colliders",
                Bounds(-0.55f, 0.55f, 0.10f, 2.40f, 0.94f, 1.11f));

            BuildBoxColliders(forklift, Require(forklift, "Crane"), "Fork Colliders",
                Bounds(-0.68f, 0.68f, 0.57f, 1.48f, 1.11f, 1.28f), // carriage plate and backrest
                Bounds(-0.68f, -0.59f, 0.57f, 0.63f, 1.28f, 2.84f), // tines, left to right
                Bounds(-0.19f, -0.06f, 0.57f, 0.63f, 1.28f, 2.84f),
                Bounds(0.06f, 0.19f, 0.57f, 0.63f, 1.28f, 2.84f),
                Bounds(0.59f, 0.68f, 0.57f, 0.63f, 1.28f, 2.84f));
        }

        static Bounds Bounds(float minX, float maxX, float minY, float maxY, float minZ, float maxZ)
        {
            var bounds = new Bounds();
            bounds.SetMinMax(new Vector3(minX, minY, minZ), new Vector3(maxX, maxY, maxZ));
            return bounds;
        }

        static void BuildBoxColliders(Transform forklift, Transform part, string name, params Bounds[] forkliftSpaceBoxes)
        {
            // The holder is aligned with the forklift at rest, so forklift-space boxes can be used directly.
            var holder = GetOrCreateChild(part, name);
            holder.SetPositionAndRotation(forklift.position, forklift.rotation);
            holder.localScale = Vector3.one;

            foreach (var existing in holder.GetComponents<BoxCollider>())
                Undo.DestroyObjectImmediate(existing);

            foreach (var box in forkliftSpaceBoxes)
            {
                var collider = Undo.AddComponent<BoxCollider>(holder.gameObject);
                collider.center = box.center;
                collider.size = box.size;
            }
        }

        static CabLeverInteractable BuildLever(Transform forklift, Transform lever, bool invertValue)
        {
            // The lever's pivot is at the model origin, so find the hinge (bottom end) and knob (top end) from the mesh.
            var mesh = lever.GetComponent<MeshFilter>().sharedMesh;
            var bounds = MeshBoundsInForkliftSpace(forklift, lever);
            var hinge = Vector3.zero;
            var knob = Vector3.zero;
            int hingeCount = 0, knobCount = 0;
            foreach (var vertex in mesh.vertices)
            {
                var point = forklift.InverseTransformPoint(lever.TransformPoint(vertex));
                if (point.y < bounds.min.y + 0.01f) { hinge += point; hingeCount++; }
                if (point.y > bounds.max.y - k_LeverKnobLength) { knob += point; knobCount++; }
            }

            hinge /= hingeCount;
            knob /= knobCount;

            var grab = GetOrAdd<SphereCollider>(lever.gameObject);
            grab.isTrigger = true;
            grab.radius = k_LeverGrabRadius / lever.lossyScale.x;
            grab.center = lever.InverseTransformPoint(forklift.TransformPoint(knob));

            var interactable = GetOrAdd<CabLeverInteractable>(lever.gameObject);
            var serialized = new SerializedObject(interactable);
            serialized.FindProperty("_hinge").vector3Value = lever.parent.InverseTransformPoint(forklift.TransformPoint(hinge));
            serialized.FindProperty("_axis").vector3Value = lever.parent.InverseTransformDirection(forklift.TransformDirection(Vector3.left)).normalized;
            serialized.FindProperty("_invertValue").boolValue = invertValue;
            serialized.ApplyModifiedProperties();
            return interactable;
        }

        static Bounds MeshBoundsInForkliftSpace(Transform forklift, Transform part)
        {
            var vertices = part.GetComponent<MeshFilter>().sharedMesh.vertices;
            var bounds = new Bounds(forklift.InverseTransformPoint(part.TransformPoint(vertices[0])), Vector3.zero);
            foreach (var vertex in vertices)
                bounds.Encapsulate(forklift.InverseTransformPoint(part.TransformPoint(vertex)));

            return bounds;
        }

        static void BuildGearReadout(Transform forklift, ForkliftController controller)
        {
            // Adding TextMeshPro swaps the Transform for a RectTransform, so only use the transform from the text.
            var text = GetOrAdd<TextMeshPro>(GetOrCreateChild(forklift, "Gear Readout").gameObject);
            text.text = "F";
            text.fontSize = 1.2f;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(0.4f, 0.2f);
            text.rectTransform.localPosition = s_GearReadoutPosition;
            text.rectTransform.localRotation = Quaternion.identity;

            var readout = GetOrAdd<ForkliftGearReadout>(text.gameObject);
            SetFields(readout, ("_controller", controller));
        }

        static void BuildSeatedRig(Transform forklift, ControllerButtonInput input)
        {
            var seat = GetOrCreateChild(forklift, "Seat Anchor");
            seat.localPosition = s_EyePoint;
            seat.localRotation = Quaternion.identity;

            var origin = seat.GetComponentInChildren<XROrigin>(true);
            if (origin == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_RigPrefabPath);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, seat);
                Undo.RegisterCreatedObjectUndo(instance, "Create XR rig");
                origin = instance.GetComponent<XROrigin>();
            }

            var rig = origin.gameObject;
            rig.transform.localPosition = Vector3.zero;
            rig.transform.localRotation = Quaternion.identity;

            // A seated driver doesn't walk, turn or teleport, and the rig's body capsule would push against the cab.
            var characterController = rig.GetComponent<CharacterController>();
            if (characterController != null)
                characterController.enabled = false;

            var locomotion = rig.transform.Find("Locomotion");
            if (locomotion != null)
                locomotion.gameObject.SetActive(false);

            foreach (var transform in rig.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == "Teleport Interactor")
                    transform.gameObject.SetActive(false);

                // This component would turn the teleport interactor back on when the stick is pushed.
                if (transform.GetComponent("ControllerInputActionManager") is Behaviour inputActionManager)
                    inputActionManager.enabled = false;
            }

            // Hands grab the wheel by reaching it, so no far ray, and the near check must see the wheel's trigger.
            foreach (var interactor in rig.GetComponentsInChildren<NearFarInteractor>(true))
            {
                interactor.enableFarCasting = false;

                // Let each cab control measure its own distance (rim, knob), so the hand grabs the control it touches.
                interactor.nearCasterSortingStrategy = NearFarInteractor.NearCasterSortingStrategy.InteractableBased;
                var caster = interactor.GetComponent<SphereInteractionCaster>();
                if (caster != null)
                {
                    caster.physicsTriggerInteraction = QueryTriggerInteraction.Collide;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(caster);
                }

                PrefabUtility.RecordPrefabInstancePropertyModifications(interactor);
            }

            var recenter = GetOrAdd<SeatedRigRecenter>(seat.gameObject);
            SetFields(recenter, ("_origin", origin), ("_eyePoint", seat), ("_inputSource", input));
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                    return root;
            }

            return null;
        }

        static GameObject GetOrCreateRoot(Scene scene, string name)
        {
            var existing = FindRoot(scene, name);
            if (existing != null)
                return existing;

            var created = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(created, "Create " + name);
            return created;
        }

        static Transform GetOrCreateChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
                return existing;

            var created = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(created, "Create " + name);
            created.transform.SetParent(parent, false);
            return created.transform;
        }

        static Transform Require(Transform root, string name)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == name)
                    return transform;
            }

            throw new System.InvalidOperationException($"Setup Ride Test Scene: the forklift has no part called {name}.");
        }

        static T GetOrAdd<T>(GameObject gameObject) where T : Component
        {
            var component = gameObject.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(gameObject);
        }

        static void SetFields(Object target, params (string name, Object value)[] fields)
        {
            var serialized = new SerializedObject(target);
            foreach (var (name, value) in fields)
                serialized.FindProperty(name).objectReferenceValue = value;

            serialized.ApplyModifiedProperties();
        }
    }
}
