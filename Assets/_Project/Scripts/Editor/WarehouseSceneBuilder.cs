using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRConTrain.Training;

namespace VRConTrain.EditorTools
{
    /// <summary>
    /// Builds the warehouse environment in the active scene: the building with collision, rows of racks with boxes
    /// on the shelves, a staging area of floor boxes, a cone lane and slalom for driving practice, and floating
    /// markers. Also creates the prefabs for the rack, box, cone and marker. Safe to run again; the previous
    /// warehouse is replaced.
    /// </summary>
    public static class WarehouseSceneBuilder
    {
        /// <summary>Name of the scene root that holds the whole warehouse.</summary>
        public const string k_RootName = "Warehouse";

        const string k_BuildingModelPath = "Assets/Art/Warehouse Environment.blend";
        const string k_StuffModelPath = "Assets/Art/Warehouse Stuff.blend";
        const string k_PrefabFolder = "Assets/_Project/Prefabs/Warehouse";

        const float k_BoxMass = 30f;
        const float k_ConeMass = 3f;
        const float k_ConeMaxDepenetrationSpeed = 2f;

        // Inner faces of the building's walls and floor, measured with raycasts. The model's surfaces have no
        // thickness, so solid blocks are added behind them.
        const float k_InnerHalfSize = 22.97f;
        const float k_SolidThickness = 1f;
        const float k_SolidWallHeight = 8f;
        const float k_SettleGap = 0.002f;

        // Racks: rows run along X, with 4.5 m aisles. The back row stands clear of the end-wall columns (z 21.84).
        const int k_RacksPerRow = 5;
        const float k_RackGap = 0.1f;
        static readonly float[] s_RowZ = { 21.03f, 15.41f, 14.19f, 8.57f, 7.35f };

        // Shelf surfaces above the rack base, measured from the model (board tops at 0.09, 1.17, 2.21, 3.26 m).
        static readonly float[] s_ShelfHeights = { 0.06f, 1.14f, 2.18f, 3.23f };
        static readonly float[] s_SlotX = { -1.1f, 0f, 1.1f };
        const float k_ShelfFillChance = 0.6f;
        const int k_ShelfFillSeed = 7;

        // The empty slot the drop-off marker points at: front row, middle rack, second shelf, middle slot.
        const int k_DropRow = 4;
        const int k_DropRack = 2;
        const int k_DropShelf = 1;
        const int k_DropSlot = 1;

        static readonly Vector3 s_ForkliftSpawn = new Vector3(-14f, 0f, -17f);

        static readonly Vector2[] s_LaneCones =
        {
            new Vector2(-15.75f, -13.5f), new Vector2(-15.75f, -11f), new Vector2(-15.75f, -8.5f), new Vector2(-15.75f, -6f), new Vector2(-15.75f, -3.5f),
            new Vector2(-12.25f, -13.5f), new Vector2(-12.25f, -11f), new Vector2(-12.25f, -8.5f), new Vector2(-12.25f, -6f), new Vector2(-12.25f, -3.5f),
        };

        static readonly Vector2[] s_SlalomCones =
        {
            new Vector2(-4f, -9f), new Vector2(1f, -9f), new Vector2(6f, -9f), new Vector2(11f, -9f), new Vector2(16f, -9f),
        };

        static readonly Vector2[] s_StagingBoxes =
        {
            new Vector2(13.5f, 2f), new Vector2(14.7f, 2f), new Vector2(15.9f, 2f),
            new Vector2(13.5f, 3.2f), new Vector2(14.7f, 3.2f), new Vector2(15.9f, 3.2f),
        };

        static readonly Vector2 s_StackedBox = new Vector2(15.9f, 3.2f);
        static readonly Vector2 s_LaneEndMarker = new Vector2(-14f, -1f);

        /// <summary>Builds or rebuilds the warehouse in the active scene.</summary>
        [MenuItem("VRConTrain/Setup Warehouse Environment")]
        public static void Build()
        {
            var scene = SceneManager.GetActiveScene();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Warehouse Environment");

            var prefabs = CreatePrefabs();

            foreach (var name in new[] { k_RootName, "Ground" })
            {
                var old = FindRoot(scene, name);
                if (old != null)
                    Undo.DestroyObjectImmediate(old);
            }

            // Loose copies of the two models dragged into the scene would overlap the built warehouse.
            foreach (var rootObject in scene.GetRootGameObjects())
            {
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(rootObject))
                    continue;

                var source = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(rootObject));
                if (source == k_BuildingModelPath || source == k_StuffModelPath)
                {
                    Debug.Log($"Setup Warehouse Environment: removed the loose model copy '{rootObject.name}' from the scene root.");
                    Undo.DestroyObjectImmediate(rootObject);
                }
            }

            var root = new GameObject(k_RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create warehouse");

            BuildBuilding(root.transform);
            BuildRacks(root.transform, prefabs);
            BuildFloorItems(root.transform, prefabs);
            PlaceForklift(scene, root.transform);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Setup Warehouse Environment: done. Save the scene to keep the changes.");
        }

        struct Prefabs
        {
            public GameObject Rack;
            public GameObject Box;
            public GameObject Cone;
            public GameObject Marker;
            public float RackWidth;
            public float BoxHeight;
        }

        static Prefabs CreatePrefabs()
        {
            EnsureFolder(k_PrefabFolder);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(k_StuffModelPath).transform;
            var result = new Prefabs();

            var rackBounds = PartBounds(model, Require(model, "Rack"));
            result.RackWidth = rackBounds.size.x;
            result.Rack = CreatePartPrefab(model, "Rack", "Rack", BottomCenter(rackBounds), (root, mesh) =>
            {
                // Open shelving: the exact mesh shape lets boxes sit on the shelves and forks go between them.
                mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.GetComponent<MeshFilter>().sharedMesh;
                SetStaticRecursively(root);
            });

            var boxBounds = PartBounds(model, Require(model, "Box"));
            result.BoxHeight = boxBounds.size.y;
            result.Box = CreatePartPrefab(model, "Box", "Box", BottomCenter(boxBounds), (root, mesh) =>
            {
                var collider = root.AddComponent<BoxCollider>();
                collider.center = boxBounds.center - BottomCenter(boxBounds);
                collider.size = boxBounds.size;
                root.AddComponent<Rigidbody>().mass = k_BoxMass;
            });

            var coneBounds = PartBounds(model, Require(model, "Cone"));
            result.Cone = CreatePartPrefab(model, "Cone", "Cone", BottomCenter(coneBounds), (root, mesh) =>
            {
                // A convex hull of the real shape, so a hit cone tips over and rolls like a cone.
                var collider = mesh.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh.GetComponent<MeshFilter>().sharedMesh;
                collider.convex = true;

                // Light and fast when hit, so continuous detection stops it tunnelling, and a low push-out speed
                // stops it being launched when the forks pin it against the floor.
                var body = root.AddComponent<Rigidbody>();
                body.mass = k_ConeMass;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.maxDepenetrationVelocity = k_ConeMaxDepenetrationSpeed;
            });

            // The arrow is modelled upright with its tip at the bottom; the marker pivots on the tip.
            var arrowBounds = PartBounds(model, Require(model, "Arrow"));
            var tip = new Vector3(arrowBounds.center.x, arrowBounds.min.y, arrowBounds.center.z);
            result.Marker = CreatePartPrefab(model, "Arrow", "Floating Marker", tip, (root, mesh) =>
            {
                var visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);
                mesh.SetParent(visual, false);
                var marker = root.AddComponent<FloatingMarker>();
                SetFields(marker, ("_visual", visual));
            });

            return result;
        }

        /// <summary>
        /// Saves one part of the props model as a prefab. The part's mesh goes on a child, offset so that the
        /// chosen pivot point (in the model's space) lands on the prefab's origin.
        /// </summary>
        static GameObject CreatePartPrefab(Transform model, string partName, string prefabName, Vector3 pivot, System.Action<GameObject, Transform> configure)
        {
            var part = Require(model, partName);
            var root = new GameObject(prefabName);
            var mesh = new GameObject("Mesh").transform;
            mesh.SetParent(root.transform, false);
            mesh.localPosition = model.InverseTransformPoint(part.position) - pivot;
            mesh.localRotation = Quaternion.Inverse(model.rotation) * part.rotation;
            mesh.localScale = part.lossyScale;
            mesh.gameObject.AddComponent<MeshFilter>().sharedMesh = part.GetComponent<MeshFilter>().sharedMesh;
            mesh.gameObject.AddComponent<MeshRenderer>().sharedMaterials = part.GetComponent<MeshRenderer>().sharedMaterials;

            configure(root, mesh);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{k_PrefabFolder}/{prefabName}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void BuildBuilding(Transform root)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(k_BuildingModelPath);
            var building = (GameObject)PrefabUtility.InstantiatePrefab(model, root);
            building.name = "Building";
            Undo.RegisterCreatedObjectUndo(building, "Create building");

            // Floor, walls and roof are one mesh, so one mesh collider gives the floor to drive on and solid walls.
            var shell = Require(building.transform, "Warehouse");
            shell.gameObject.AddComponent<MeshCollider>().sharedMesh = shell.GetComponent<MeshFilter>().sharedMesh;

            // The mesh floor and walls are single surfaces, so anything pushed hard into them can pass through.
            // Solid blocks just behind them stop that.
            var solids = new GameObject("Solid Floor And Walls");
            solids.transform.SetParent(building.transform, false);
            var span = (k_InnerHalfSize + k_SolidThickness) * 2f;
            var wallCenter = k_InnerHalfSize + k_SolidThickness * 0.5f;
            AddBox(solids, new Vector3(0f, -k_SolidThickness * 0.5f, 0f), new Vector3(span, k_SolidThickness, span));
            AddBox(solids, new Vector3(wallCenter, k_SolidWallHeight * 0.5f, 0f), new Vector3(k_SolidThickness, k_SolidWallHeight, span));
            AddBox(solids, new Vector3(-wallCenter, k_SolidWallHeight * 0.5f, 0f), new Vector3(k_SolidThickness, k_SolidWallHeight, span));
            AddBox(solids, new Vector3(0f, k_SolidWallHeight * 0.5f, wallCenter), new Vector3(span, k_SolidWallHeight, k_SolidThickness));
            AddBox(solids, new Vector3(0f, k_SolidWallHeight * 0.5f, -wallCenter), new Vector3(span, k_SolidWallHeight, k_SolidThickness));

            SetStaticRecursively(building);
        }

        static void AddBox(GameObject holder, Vector3 center, Vector3 size)
        {
            var box = holder.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }

        static void BuildRacks(Transform root, Prefabs prefabs)
        {
            var racks = CreateGroup(root, "Racks");
            var shelfBoxes = CreateGroup(root, "Shelf Boxes");
            var random = new System.Random(k_ShelfFillSeed);
            var pitch = prefabs.RackWidth + k_RackGap;

            for (var row = 0; row < s_RowZ.Length; row++)
            {
                for (var index = 0; index < k_RacksPerRow; index++)
                {
                    var x = (index - (k_RacksPerRow - 1) * 0.5f) * pitch;
                    var rackPosition = new Vector3(x, 0f, s_RowZ[row]);
                    var rack = Place(prefabs.Rack, racks, rackPosition, 0f);
                    rack.name = $"Rack {row + 1}-{index + 1}";

                    for (var shelf = 0; shelf < s_ShelfHeights.Length; shelf++)
                    {
                        for (var slot = 0; slot < s_SlotX.Length; slot++)
                        {
                            var isDropSlot = row == k_DropRow && index == k_DropRack && shelf == k_DropShelf && slot == k_DropSlot;
                            var filled = random.NextDouble() < k_ShelfFillChance;
                            var yaw = (float)(random.NextDouble() * 6.0 - 3.0);
                            if (isDropSlot || !filled)
                                continue;

                            var position = rackPosition + new Vector3(s_SlotX[slot], s_ShelfHeights[shelf] + k_SettleGap, 0f);
                            Place(prefabs.Box, shelfBoxes, position, yaw);
                        }
                    }

                    if (row == k_DropRow && index == k_DropRack)
                    {
                        // A half-size marker fits between the shelves, pointing down at the empty slot.
                        var markers = CreateGroup(root, "Markers");
                        var target = rackPosition + new Vector3(s_SlotX[k_DropSlot], s_ShelfHeights[k_DropShelf] + 0.15f, 0f);
                        var marker = Place(prefabs.Marker, markers, target, 0f);
                        marker.name = "Marker - Drop Slot";
                        marker.transform.localScale = Vector3.one * 0.5f;
                    }
                }
            }
        }

        static void BuildFloorItems(Transform root, Prefabs prefabs)
        {
            var floorBoxes = CreateGroup(root, "Floor Boxes");
            foreach (var spot in s_StagingBoxes)
                Place(prefabs.Box, floorBoxes, Floor(spot, k_SettleGap), 0f);

            Place(prefabs.Box, floorBoxes, Floor(s_StackedBox, prefabs.BoxHeight + k_SettleGap * 2f), 4f);

            var cones = CreateGroup(root, "Cones");
            foreach (var spot in s_LaneCones)
                Place(prefabs.Cone, cones, Floor(spot, k_SettleGap), 0f).name = "Cone - Lane";

            foreach (var spot in s_SlalomCones)
                Place(prefabs.Cone, cones, Floor(spot, k_SettleGap), 0f).name = "Cone - Slalom";

            var markers = CreateGroup(root, "Markers");
            Place(prefabs.Marker, markers, Floor(s_LaneEndMarker, 0.6f), 0f).name = "Marker - Lane End";
            Place(prefabs.Marker, markers, Floor(s_StagingBoxes[0], prefabs.BoxHeight + 0.3f), 0f).name = "Marker - Pick Up Box";
        }

        static void PlaceForklift(Scene scene, Transform root)
        {
            var spawn = CreateGroup(root, "Forklift Spawn");
            spawn.SetPositionAndRotation(s_ForkliftSpawn, Quaternion.identity);

            var forklift = FindRoot(scene, "Forklift");
            if (forklift == null)
            {
                Debug.LogWarning("Setup Warehouse Environment: no Forklift in the scene to place at the spawn point.");
                return;
            }

            Undo.RecordObject(forklift.transform, "Place forklift");
            forklift.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
        }

        static Vector3 Floor(Vector2 spot, float height) => new Vector3(spot.x, height, spot.y);

        static GameObject Place(GameObject prefab, Transform parent, Vector3 position, float yaw)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            Undo.RegisterCreatedObjectUndo(instance, "Place " + prefab.name);
            return instance;
        }

        static Transform CreateGroup(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
                return existing;

            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        static Bounds PartBounds(Transform model, Transform part)
        {
            var vertices = part.GetComponent<MeshFilter>().sharedMesh.vertices;
            var bounds = new Bounds(model.InverseTransformPoint(part.TransformPoint(vertices[0])), Vector3.zero);
            foreach (var vertex in vertices)
                bounds.Encapsulate(model.InverseTransformPoint(part.TransformPoint(vertex)));

            return bounds;
        }

        static Vector3 BottomCenter(Bounds bounds) => new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

        static void SetStaticRecursively(GameObject gameObject)
        {
            foreach (var transform in gameObject.GetComponentsInChildren<Transform>(true))
                transform.gameObject.isStatic = true;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
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

        static Transform Require(Transform root, string name)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == name)
                    return transform;
            }

            throw new System.InvalidOperationException($"Setup Warehouse Environment: no part called {name}.");
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
