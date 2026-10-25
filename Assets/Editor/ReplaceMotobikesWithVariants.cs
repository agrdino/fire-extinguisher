#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class ReplaceMotobikesWithVariants
{
    private const string ScenePath = "Assets/Scenes/Parking Area Scene.unity";
    private const string MotorBikeFolder = "Assets/Environments/Props/MotorBike";
    private const string BasePrefabPath = MotorBikeFolder + "/Motobike.prefab";
    private const string VariantsFolder = MotorBikeFolder + "/Variants";
    private const string RootName = "Motobike";
    private const int ExpectedOriginalRoots = 84;
    private const int ExpectedPrefabSources = 36;
    private const int ExpectedRawAssemblies = 48;
    private const int ExpectedCompositeAssemblies = 8;
    private const int ExpectedExpandedInstances = 108;
    private const int ExpectedVariantTypes = 17;

    private sealed class BikeSource
    {
        public Transform ModelTransform;
        public GameObject OriginalTopRoot;
        public GameObject SourcePrefab;
        public string SourcePrefabPath;
        public string RawSignature;
        public Matrix4x4 DesiredLocalMatrix;
        public Matrix4x4 NestedModelMatrix;
        public int VariantIndex;
    }

    private sealed class VariantDefinition
    {
        public string Key;
        public string SortKey;
        public string RepresentativeName;
        public GameObject SourcePrefab;
        public GameObject RawRepresentative;
        public Matrix4x4 NestedModelMatrix;
        public Matrix4x4 DefaultModelCorrection;
        public readonly List<BikeSource> Sources = new List<BikeSource>();
        public int Index;
    }

    private struct TransformParts
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
    }

    [MenuItem("Tools/Scene Tools/Rebuild Motobike Variants and Stage")]
    private static void RebuildAndStage()
    {
        if (!TryCollectSources(out var scene, out var root, out var originalRoots, out var oldVariants, out var definitions))
            return;
        if (definitions.Count != ExpectedVariantTypes)
        {
            Debug.LogError("Expected 17 unique Motobike variants after splitting the four-bike assemblies, found " + definitions.Count + ". No prefab or scene objects were changed.");
            return;
        }
        if (definitions.Sum(d => d.Sources.Count) != ExpectedExpandedInstances)
        {
            Debug.LogError("Expected 108 individual Motobike placements after splitting composite assemblies. No prefab or scene objects were changed.");
            return;
        }
        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
        if (basePrefab == null || basePrefab.transform.Find("View") == null || basePrefab.transform.Find("View").childCount != 0 || basePrefab.GetComponent<BoxCollider>() == null)
        {
            Debug.LogError("The Motobike base prefab is missing its empty View child or parent BoxCollider. No changes were made.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(VariantsFolder))
        {
            Debug.LogError("Motobike Variants folder is missing. No changes were made.");
            return;
        }

        foreach (var definition in definitions)
        {
            definition.NestedModelMatrix = definition.SourcePrefab != null
                ? LocalMatrix(definition.SourcePrefab.transform)
                : Matrix4x4.identity;
            definition.DefaultModelCorrection = CalculateDefaultModelCorrection(definition);
            var path = VariantPath(definition.Index);
            if (!CreateVariantAsset(definition, VariantName(definition.Index), path, scene, basePrefab))
            {
                Debug.LogError("Could not create " + VariantName(definition.Index) + ". Original source roots remain hidden and generated scene replacements remain available; inspect generated assets before retrying.");
                return;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        var variantAssets = new GameObject[ExpectedVariantTypes];
        foreach (var definition in definitions)
        {
            var path = VariantPath(definition.Index);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || PrefabUtility.GetPrefabAssetType(asset) != PrefabAssetType.Variant ||
                asset.transform.Find("View/VisualRoot") == null || asset.GetComponent<BoxCollider>() == null)
            {
                Debug.LogError("Variant validation failed for " + path + ". The original source roots remain intact.");
                return;
            }
            variantAssets[definition.Index - 1] = asset;
        }

        Undo.IncrementCurrentGroup();
        var undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Rebuild Motobike Variants and Stage Individual Bikes");
        Undo.RegisterFullObjectHierarchyUndo(root, "Rebuild Motobike Variants and Stage Individual Bikes");
        var newInstances = new List<GameObject>(ExpectedExpandedInstances);

        foreach (var definition in definitions)
        {
            var asset = variantAssets[definition.Index - 1];
            foreach (var source in definition.Sources)
            {
                var instance = PrefabUtility.InstantiatePrefab(asset, scene) as GameObject;
                if (instance == null)
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                    Debug.LogError("Could not instantiate all new Motobike variants. Scene staging was rolled back; original sources remain intact.");
                    return;
                }
                Undo.RegisterCreatedObjectUndo(instance, "Create Normalized Motobike Variant Instance");
                instance.transform.SetParent(root.transform, false);

                var sourceParts = Decompose(source.DesiredLocalMatrix);
                var yawRotation = Quaternion.Euler(0f, sourceParts.Rotation.eulerAngles.y, 0f);
                var normalizedRootMatrix = Matrix4x4.TRS(sourceParts.Position, yawRotation, Vector3.one);
                instance.transform.localPosition = sourceParts.Position;
                instance.transform.localRotation = yawRotation;
                instance.transform.localScale = Vector3.one;
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);

                var visualRoot = instance.transform.Find("View/VisualRoot");
                if (visualRoot == null)
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                    Debug.LogError("A staged variant is missing View/VisualRoot. Scene staging was rolled back.");
                    return;
                }
                var instanceCorrection = normalizedRootMatrix.inverse * source.DesiredLocalMatrix * source.NestedModelMatrix.inverse;
                ApplyTransformMatrix(visualRoot, instanceCorrection);
                PrefabUtility.RecordPrefabInstancePropertyModifications(visualRoot);

                var collider = instance.GetComponent<BoxCollider>();
                if (collider == null || !TryCalculateBounds(instance.transform, out var instanceBounds))
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                    Debug.LogError("Could not calculate the normalized bike bounds for " + instance.name + ". Scene staging was rolled back.");
                    return;
                }
                instanceBounds.Expand(new Vector3(0.05f, 0.05f, 0.05f));
                Undo.RecordObject(collider, "Fit Motobike Box Collider");
                collider.center = instanceBounds.center;
                collider.size = instanceBounds.size;
                PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
                newInstances.Add(instance);
            }
        }

        foreach (var oldVariant in oldVariants)
            Undo.DestroyObjectImmediate(oldVariant);

        foreach (var sourceRoot in originalRoots)
        {
            if (sourceRoot.activeSelf)
            {
                Undo.RecordObject(sourceRoot, "Keep Original Motobike Source Hidden");
                sourceRoot.SetActive(false);
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;

        MoveObsoleteVariantsToLegacy();
        AssetDatabase.SaveAssets();

        Debug.Log("Rebuilt " + ExpectedVariantTypes + " sequential Motobike variants and staged " + newInstances.Count + " individual bike instances. Root transforms now use position + Y rotation + scale 1; geometry correction is under View/VisualRoot. All 84 original source roots remain hidden, and the scene has not been saved.");
        foreach (var definition in definitions)
            Debug.Log(VariantName(definition.Index) + " <- " + definition.RepresentativeName + " (" + definition.Sources.Count + " scene bikes)");
    }

    [MenuItem("Tools/Scene Tools/Finalize Motobike Replacement (Delete Sources)")]
    private static void FinalizeReplacement()
    {
        if (!TryGetRoot(out var scene, out var root))
            return;

        var directChildren = root.transform.Cast<Transform>().ToArray();
        var variants = directChildren.Where(t => IsGeneratedVariantInstance(t.gameObject)).ToArray();
        var sources = directChildren.Where(t => !IsGeneratedVariantInstance(t.gameObject)).ToArray();
        if (variants.Length != ExpectedExpandedInstances || sources.Length != ExpectedOriginalRoots || sources.Any(t => t.gameObject.activeSelf))
        {
            Debug.LogError("Expected 108 active normalized variant instances and 84 hidden source roots under Motobike. No objects were deleted.");
            return;
        }

        Undo.IncrementCurrentGroup();
        var undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Delete Replaced Motobike Sources");
        Undo.RegisterFullObjectHierarchyUndo(root, "Delete Replaced Motobike Sources");
        foreach (var source in sources)
            Undo.DestroyObjectImmediate(source.gameObject);
        Undo.CollapseUndoOperations(undoGroup);

        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
        {
            Debug.LogError("Motobike sources were removed, but Unity could not save the scene.");
            return;
        }

        Selection.activeGameObject = root;
        Debug.Log("Replaced the original Motobike source roots with 108 instances across 17 variants and saved the Parking Area Scene.");
    }

    [MenuItem("Tools/Scene Tools/Audit Normalized Motobike Stage")]
    private static void AuditNormalizedStage()
    {
        if (!TryGetRoot(out _, out var root))
            return;

        var directChildren = root.transform.Cast<Transform>().ToArray();
        var variants = directChildren.Where(t => IsGeneratedVariantInstance(t.gameObject)).ToArray();
        var sources = directChildren.Where(t => !IsGeneratedVariantInstance(t.gameObject)).ToArray();
        var invalidRoots = variants.Where(t => !IsNormalizedRoot(t)).Select(t => t.name).ToArray();
        var sceneInstanceCounts = variants
            .Select(t => PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject) as GameObject)
            .Where(source => source != null)
            .GroupBy(source => Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(source)), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var report = new List<string>
        {
            "Direct children: " + directChildren.Length,
            "Generated variant instances: " + variants.Length,
            "Original source roots: " + sources.Length + " (active=" + sources.Count(t => t.gameObject.activeSelf) + ")",
            "Scene roots normalized: " + (variants.Length - invalidRoots.Length) + "/" + variants.Length,
            "Root transform violations: " + (invalidRoots.Length == 0 ? "none" : string.Join(", ", invalidRoots))
        };

        var assetFailures = new List<string>();
        for (var index = 1; index <= ExpectedVariantTypes; index++)
        {
            var path = VariantPath(index);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var view = asset != null ? asset.transform.Find("View") : null;
            var visualRoot = view != null ? view.Find("VisualRoot") : null;
            if (asset == null || PrefabUtility.GetPrefabAssetType(asset) != PrefabAssetType.Variant ||
                asset.GetComponent<BoxCollider>() == null || view == null || visualRoot == null || !IsNormalizedRoot(asset.transform))
                assetFailures.Add(VariantName(index));
            report.Add(VariantName(index) + ": meshes=" + (asset != null ? asset.GetComponentsInChildren<MeshFilter>(true).Count(f => f.sharedMesh != null) : 0) +
                ", scene instances=" + (sceneInstanceCounts.TryGetValue(VariantName(index), out var instanceCount) ? instanceCount : 0) +
                ", direct VisualRoot children=" + (visualRoot != null ? visualRoot.childCount : -1) +
                ", collider=" + (asset != null && asset.GetComponent<BoxCollider>() != null));
        }
        report.Add("Variant asset failures: " + (assetFailures.Count == 0 ? "none" : string.Join(", ", assetFailures)));

        var reportPath = Path.Combine(Application.dataPath, "../Library/MotobikeRebuildAudit.txt");
        File.WriteAllLines(reportPath, report);
        Debug.Log("Motobike normalized-stage audit saved to " + reportPath + ". " + string.Join("; ", report.Take(5)));
    }

    private static bool IsNormalizedRoot(Transform transform)
    {
        var euler = transform.localEulerAngles;
        return Mathf.Abs(Mathf.DeltaAngle(euler.x, 0f)) < 0.01f &&
               Mathf.Abs(Mathf.DeltaAngle(euler.z, 0f)) < 0.01f &&
               (transform.localScale - Vector3.one).sqrMagnitude < 0.000001f;
    }

    private static bool TryCollectSources(out Scene scene, out GameObject root, out GameObject[] originalRoots, out GameObject[] oldVariants, out List<VariantDefinition> definitions)
    {
        scene = default;
        root = null;
        originalRoots = Array.Empty<GameObject>();
        oldVariants = Array.Empty<GameObject>();
        definitions = new List<VariantDefinition>();
        if (!TryGetRoot(out scene, out root))
            return false;

        var directChildren = root.transform.Cast<Transform>().ToArray();
        oldVariants = directChildren.Where(t => IsGeneratedVariantInstance(t.gameObject)).Select(t => t.gameObject).ToArray();
        originalRoots = directChildren.Where(t => !IsGeneratedVariantInstance(t.gameObject)).Select(t => t.gameObject).ToArray();
        if (originalRoots.Length != ExpectedOriginalRoots || originalRoots.Any(go => go.activeSelf) ||
            (oldVariants.Length != 84 && oldVariants.Length != ExpectedExpandedInstances))
        {
            Debug.LogError("Expected 84 hidden original roots and either 84 or 108 staged variant instances. Found " + originalRoots.Length + " source roots and " + oldVariants.Length + " generated variants. No changes were made.");
            return false;
        }

        var prefabGroups = new Dictionary<string, VariantDefinition>(StringComparer.OrdinalIgnoreCase);
        var rawGroups = new Dictionary<string, VariantDefinition>(StringComparer.Ordinal);
        var prefabCount = 0;
        var rawAssemblyCount = 0;
        var compositeAssemblyCount = 0;
        var expandedSources = new List<BikeSource>(ExpectedExpandedInstances);

        foreach (var topRoot in originalRoots)
        {
            var sourcePrefab = PrefabUtility.GetCorrespondingObjectFromSource(topRoot) as GameObject;
            var sourcePath = sourcePrefab != null ? AssetDatabase.GetAssetPath(sourcePrefab) : string.Empty;
            if (sourcePrefab != null && sourcePath.StartsWith(MotorBikeFolder + "/", StringComparison.OrdinalIgnoreCase) &&
                !sourcePath.StartsWith(VariantsFolder + "/", StringComparison.OrdinalIgnoreCase))
            {
                prefabCount++;
                var info = CreateSource(root.transform, topRoot.transform, topRoot, sourcePrefab, sourcePath, string.Empty);
                if (!prefabGroups.TryGetValue(sourcePath, out var definition))
                {
                    definition = new VariantDefinition
                    {
                        Key = "Prefab:" + sourcePath,
                        SortKey = Path.GetFileNameWithoutExtension(sourcePath),
                        RepresentativeName = Path.GetFileNameWithoutExtension(sourcePath),
                        SourcePrefab = sourcePrefab
                    };
                    prefabGroups.Add(sourcePath, definition);
                }
                definition.Sources.Add(info);
                continue;
            }

            if (!topRoot.name.StartsWith("Assembly-", StringComparison.OrdinalIgnoreCase) ||
                topRoot.GetComponentsInChildren<MeshFilter>(true).Length == 0)
            {
                Debug.LogError("Unexpected original Motobike source root: " + topRoot.name + ". No changes were made.");
                return false;
            }
            rawAssemblyCount++;

            if (IsFourBikeAssembly(topRoot.transform))
            {
                compositeAssemblyCount++;
                foreach (Transform bikeMesh in topRoot.transform)
                    AddRawSource(root.transform, topRoot, bikeMesh, expandedSources, rawGroups);
            }
            else
                AddRawSource(root.transform, topRoot, topRoot.transform, expandedSources, rawGroups);
        }

        if (prefabCount != ExpectedPrefabSources || rawAssemblyCount != ExpectedRawAssemblies || compositeAssemblyCount != ExpectedCompositeAssemblies)
        {
            Debug.LogError("Motobike inventory mismatch. Found " + prefabCount + " prefab sources, " + rawAssemblyCount + " raw assemblies, and " + compositeAssemblyCount + " four-bike assemblies. No changes were made.");
            return false;
        }

        definitions.AddRange(prefabGroups.Values.OrderBy(d => d.SortKey, StringComparer.OrdinalIgnoreCase));
        definitions.AddRange(rawGroups.Values.OrderBy(d => d.SortKey, StringComparer.OrdinalIgnoreCase));
        for (var i = 0; i < definitions.Count; i++)
        {
            definitions[i].Index = i + 1;
            foreach (var source in definitions[i].Sources)
                source.VariantIndex = i + 1;
        }

        var allSources = prefabGroups.Values.SelectMany(d => d.Sources).Concat(rawGroups.Values.SelectMany(d => d.Sources)).ToArray();
        if (allSources.Length != ExpectedExpandedInstances || definitions.Count != ExpectedVariantTypes)
        {
            Debug.LogError("Could not map every individual Motobike to one of 17 variants. Found " + allSources.Length + " bikes across " + definitions.Count + " types. No changes were made.");
            return false;
        }
        return true;
    }

    private static void AddRawSource(Transform groupRoot, GameObject originalTopRoot, Transform modelTransform, List<BikeSource> expandedSources, Dictionary<string, VariantDefinition> rawGroups)
    {
        var signature = BuildRawSignature(modelTransform.gameObject);
        var info = CreateSource(groupRoot, modelTransform, originalTopRoot, null, string.Empty, signature);
        expandedSources.Add(info);
        if (!rawGroups.TryGetValue(signature, out var definition))
        {
            var sortKey = originalTopRoot.name + "/" + modelTransform.name;
            definition = new VariantDefinition
            {
                Key = "Raw:" + signature,
                SortKey = sortKey,
                RepresentativeName = sortKey,
                RawRepresentative = modelTransform.gameObject
            };
            rawGroups.Add(signature, definition);
        }
        if (string.CompareOrdinal(originalTopRoot.name + "/" + modelTransform.name, definition.SortKey) < 0)
        {
            definition.SortKey = originalTopRoot.name + "/" + modelTransform.name;
            definition.RepresentativeName = definition.SortKey;
            definition.RawRepresentative = modelTransform.gameObject;
        }
        definition.Sources.Add(info);
    }

    private static BikeSource CreateSource(Transform groupRoot, Transform modelTransform, GameObject originalTopRoot, GameObject sourcePrefab, string sourcePrefabPath, string rawSignature)
    {
        return new BikeSource
        {
            ModelTransform = modelTransform,
            OriginalTopRoot = originalTopRoot,
            SourcePrefab = sourcePrefab,
            SourcePrefabPath = sourcePrefabPath,
            RawSignature = rawSignature,
            DesiredLocalMatrix = groupRoot.worldToLocalMatrix * modelTransform.localToWorldMatrix,
            NestedModelMatrix = sourcePrefab != null ? LocalMatrix(sourcePrefab.transform) : Matrix4x4.identity
        };
    }

    private static bool IsFourBikeAssembly(Transform root)
    {
        var children = root.Cast<Transform>().ToArray();
        if (children.Length != 4)
            return false;
        var expectedMeshes = new HashSet<string>(new[] { "ID1671", "ID1674", "ID1677", "ID1680" }, StringComparer.Ordinal);
        var actualMeshes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var child in children)
        {
            var filters = child.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).ToArray();
            if (filters.Length != 1 || filters[0].sharedMesh.vertexCount < 8000)
                return false;
            actualMeshes.Add(filters[0].sharedMesh.name);
        }
        return actualMeshes.SetEquals(expectedMeshes);
    }

    private static Matrix4x4 CalculateDefaultModelCorrection(VariantDefinition definition)
    {
        if (definition.SourcePrefab != null)
        {
            var assetParts = Decompose(LocalMatrix(definition.SourcePrefab.transform));
            var inverseAssetYaw = Quaternion.Inverse(Quaternion.Euler(0f, assetParts.Rotation.eulerAngles.y, 0f));
            return Matrix4x4.TRS(Vector3.zero, inverseAssetYaw, Vector3.one);
        }

        var representative = definition.Sources[0];
        var parts = Decompose(representative.DesiredLocalMatrix);
        var yaw = Quaternion.Euler(0f, parts.Rotation.eulerAngles.y, 0f);
        return Matrix4x4.TRS(Vector3.zero, Quaternion.Inverse(yaw) * parts.Rotation, parts.Scale);
    }

    private static bool CreateVariantAsset(VariantDefinition definition, string variantName, string assetPath, Scene scene, GameObject basePrefab)
    {
        var instance = PrefabUtility.InstantiatePrefab(basePrefab, scene) as GameObject;
        if (instance == null)
            return false;

        instance.name = variantName;
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        var view = instance.transform.Find("View");
        if (view == null)
        {
            UnityEngine.Object.DestroyImmediate(instance);
            return false;
        }

        var visualRoot = new GameObject("VisualRoot");
        visualRoot.transform.SetParent(view, false);
        ApplyTransformMatrix(visualRoot.transform, definition.DefaultModelCorrection);

        if (definition.SourcePrefab != null)
        {
            var nested = PrefabUtility.InstantiatePrefab(definition.SourcePrefab, scene) as GameObject;
            if (nested == null)
            {
                UnityEngine.Object.DestroyImmediate(instance);
                return false;
            }
            nested.transform.SetParent(visualRoot.transform, false);
        }
        else
        {
            var model = UnityEngine.Object.Instantiate(definition.RawRepresentative, visualRoot.transform, false);
            if (model == null)
            {
                UnityEngine.Object.DestroyImmediate(instance);
                return false;
            }
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            model.SetActive(true);
        }

        if (!TryCalculateBounds(instance.transform, out var bounds))
        {
            UnityEngine.Object.DestroyImmediate(instance);
            return false;
        }
        bounds.Expand(new Vector3(0.05f, 0.05f, 0.05f));
        var collider = instance.GetComponent<BoxCollider>();
        if (collider == null)
        {
            UnityEngine.Object.DestroyImmediate(instance);
            return false;
        }
        collider.center = bounds.center;
        collider.size = bounds.size;

        var saved = PrefabUtility.SaveAsPrefabAsset(instance, assetPath, out bool success);
        UnityEngine.Object.DestroyImmediate(instance);
        return success && saved != null;
    }

    private static bool TryCalculateBounds(Transform root, out Bounds bounds)
    {
        bounds = new Bounds();
        var initialized = false;
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
                continue;
            var meshBounds = filter.sharedMesh.bounds;
            var relative = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            for (var corner = 0; corner < 8; corner++)
            {
                var point = new Vector3(
                    (corner & 1) == 0 ? meshBounds.min.x : meshBounds.max.x,
                    (corner & 2) == 0 ? meshBounds.min.y : meshBounds.max.y,
                    (corner & 4) == 0 ? meshBounds.min.z : meshBounds.max.z);
                var local = relative.MultiplyPoint3x4(point);
                if (!initialized)
                {
                    bounds = new Bounds(local, Vector3.zero);
                    initialized = true;
                }
                else
                    bounds.Encapsulate(local);
            }
        }
        return initialized;
    }

    private static string BuildRawSignature(GameObject root)
    {
        var parts = new List<string>();
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
                continue;
            var renderer = filter.GetComponent<Renderer>();
            var materialKeys = renderer != null
                ? renderer.sharedMaterials.Select(MaterialKey).OrderBy(k => k, StringComparer.Ordinal)
                : Enumerable.Empty<string>();
            var relative = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            parts.Add(ObjectKey(filter.sharedMesh) + "|" + string.Join(",", materialKeys) + "|" + MatrixKey(relative));
        }
        parts.Sort(StringComparer.Ordinal);
        return string.Join(";", parts);
    }

    private static string ObjectKey(UnityEngine.Object value)
    {
        if (value != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long localId))
            return guid + ":" + localId.ToString(CultureInfo.InvariantCulture);
        return value != null ? AssetDatabase.GetAssetPath(value) + ":" + value.name : "null";
    }

    private static string MaterialKey(Material material) => ObjectKey(material);

    private static string MatrixKey(Matrix4x4 matrix)
    {
        var values = new string[16];
        var index = 0;
        for (var row = 0; row < 4; row++)
        {
            for (var column = 0; column < 4; column++)
            {
                var value = Math.Round(matrix[row, column], 4, MidpointRounding.AwayFromZero);
                if (Math.Abs(value) < 0.00005)
                    value = 0;
                values[index++] = value.ToString("0.0000", CultureInfo.InvariantCulture);
            }
        }
        return string.Join(",", values);
    }

    private static Matrix4x4 LocalMatrix(Transform transform) => Matrix4x4.TRS(transform.localPosition, transform.localRotation, transform.localScale);

    private static TransformParts Decompose(Matrix4x4 matrix)
    {
        var x = (Vector3)matrix.GetColumn(0);
        var y = (Vector3)matrix.GetColumn(1);
        var z = (Vector3)matrix.GetColumn(2);
        var scale = new Vector3(x.magnitude, y.magnitude, z.magnitude);
        if (matrix.determinant < 0f)
        {
            scale.x = -scale.x;
            x = -x;
        }
        var rotation = Quaternion.LookRotation(z.normalized, y.normalized);
        return new TransformParts
        {
            Position = (Vector3)matrix.GetColumn(3),
            Rotation = rotation,
            Scale = scale
        };
    }

    private static void ApplyTransformMatrix(Transform target, Matrix4x4 matrix)
    {
        var parts = Decompose(matrix);
        target.localPosition = parts.Position;
        target.localRotation = parts.Rotation;
        target.localScale = parts.Scale;
    }

    private static void MoveObsoleteVariantsToLegacy()
    {
        var legacyFolder = VariantsFolder + "/Legacy";
        if (!AssetDatabase.IsValidFolder(legacyFolder))
            AssetDatabase.CreateFolder(VariantsFolder, "Legacy");
        foreach (var oldIndex in new[] { 18, 19 })
        {
            var oldPath = VariantPath(oldIndex);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(oldPath) == null)
                continue;
            var newPath = legacyFolder + "/" + VariantName(oldIndex) + " (pre-split).prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(newPath) != null)
                continue;
            var error = AssetDatabase.MoveAsset(oldPath, newPath);
            if (!string.IsNullOrEmpty(error))
                Debug.LogWarning("Kept obsolete variant at " + oldPath + ": " + error);
        }
    }

    private static string VariantName(int index) => "Motobike " + index.ToString("00", CultureInfo.InvariantCulture);
    private static string VariantPath(int index) => VariantsFolder + "/" + VariantName(index) + ".prefab";

    private static bool IsGeneratedVariantInstance(GameObject gameObject)
    {
        var source = PrefabUtility.GetCorrespondingObjectFromSource(gameObject) as GameObject;
        var path = source != null ? AssetDatabase.GetAssetPath(source) : string.Empty;
        return path.StartsWith(VariantsFolder + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetRoot(out Scene scene, out GameObject root)
    {
        scene = SceneManager.GetActiveScene();
        root = null;
        if (scene.path != ScenePath)
        {
            Debug.LogError("Open the Parking Area Scene before running this command.");
            return false;
        }
        var matches = scene.GetRootGameObjects()
            .SelectMany(go => go.GetComponentsInChildren<Transform>(true))
            .Where(t => t.name == RootName)
            .ToArray();
        if (matches.Length != 1)
        {
            Debug.LogError("Expected exactly one Motobike group in the active scene.");
            return false;
        }
        root = matches[0].gameObject;
        return true;
    }
}
#endif
