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

internal static class RecenterMotobikePivots
{
    private const string ScenePath = "Assets/Scenes/Parking Area Scene.unity";
    private const string VariantsFolder = "Assets/Environments/Props/MotorBike/Variants";
    private const string RootName = "Motobike";
    private const int ExpectedInstances = 108;
    private const float CenterEpsilon = 0.0001f;

    private sealed class InstanceSnapshot
    {
        public GameObject Root;
        public Vector3 Offset;
        public Vector3 ColliderSize;
        public Vector3 TargetColliderCenter;
        public Dictionary<string, Matrix4x4> MeshWorldMatrices;
    }

    [InitializeOnLoadMethod]
    private static void RunIfRequested()
    {
        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrEmpty(projectRoot))
            return;

        var requestPath = Path.Combine(projectRoot, "Library", "MotobikePivotRecenter.request");
        if (!File.Exists(requestPath))
            return;

        File.Delete(requestPath);
        EditorApplication.delayCall += RecenterAndPreserveScene;
    }

    [MenuItem("Tools/Scene Tools/Recenter Motobike Pivots (Keep Scene Unsaved)")]
    private static void RecenterFromMenu()
    {
        RecenterAndPreserveScene();
    }

    private static void RecenterAndPreserveScene()
    {
        var report = new List<string>();
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            WriteReport(report, "FAILED: Parking Area Scene is not the active scene. No changes made.");
            Debug.LogError("Motobike pivot recenter skipped: Parking Area Scene is not active.");
            return;
        }

        var roots = scene.GetRootGameObjects()
            .SelectMany(go => go.GetComponentsInChildren<Transform>(true))
            .Where(t => t.name == RootName)
            .ToArray();
        if (roots.Length != 1)
        {
            WriteReport(report, "FAILED: Expected one Motobike scene group; found " + roots.Length + ". No changes made.");
            Debug.LogError("Motobike pivot recenter skipped: expected exactly one Motobike group.");
            return;
        }

        var group = roots[0];
        var instances = group.Cast<Transform>()
            .Select(t => t.gameObject)
            .Where(IsVariantInstance)
            .ToArray();
        if (instances.Length != ExpectedInstances)
        {
            WriteReport(report, "FAILED: Expected " + ExpectedInstances + " variant instances; found " + instances.Length + ". No changes made.");
            Debug.LogError("Motobike pivot recenter skipped: staged instance count did not match.");
            return;
        }

        var prefabPaths = Enumerable.Range(1, 17)
            .Select(i => VariantsFolder + "/Motobike " + i.ToString("00", CultureInfo.InvariantCulture) + ".prefab")
            .ToArray();
        if (prefabPaths.Any(path => AssetDatabase.LoadAssetAtPath<GameObject>(path) == null))
        {
            WriteReport(report, "FAILED: One or more of the 17 variant prefab assets are missing. No changes made.");
            Debug.LogError("Motobike pivot recenter skipped: a variant prefab asset is missing.");
            return;
        }

        var snapshots = new List<InstanceSnapshot>(instances.Length);
        foreach (var instance in instances)
        {
            var visualRoots = GetVisualRoots(instance.transform);
            var collider = instance.GetComponent<BoxCollider>();
            if (visualRoots.Length == 0 || collider == null || !TryCalculateBounds(instance.transform, out var bounds))
            {
                WriteReport(report, "FAILED: A scene instance is missing VisualRoot, BoxCollider, or mesh bounds. No changes made.");
                Debug.LogError("Motobike pivot recenter skipped: incomplete variant instance " + instance.name + ".");
                return;
            }

            snapshots.Add(new InstanceSnapshot
            {
                Root = instance,
                Offset = PivotOffset(bounds),
                ColliderSize = collider.size,
                TargetColliderCenter = bounds.center - PivotOffset(bounds),
                MeshWorldMatrices = CaptureMeshWorldMatrices(instance.transform)
            });
        }

        Undo.IncrementCurrentGroup();
        var undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Recenter Motobike Pivots");
        Undo.RegisterFullObjectHierarchyUndo(group.gameObject, "Recenter Motobike Pivots");

        foreach (var snapshot in snapshots)
        {
            var root = snapshot.Root.transform;
            var visualRoots = GetVisualRoots(root);
            var collider = snapshot.Root.GetComponent<BoxCollider>();
            var offset = snapshot.Offset;
            if (offset.sqrMagnitude > CenterEpsilon * CenterEpsilon)
            {
                var worldOffset = root.TransformVector(offset);
                root.position += worldOffset;
                foreach (var visualRoot in visualRoots)
                    visualRoot.localPosition -= offset;
                PrefabUtility.RecordPrefabInstancePropertyModifications(root);
                foreach (var visualRoot in visualRoots)
                    PrefabUtility.RecordPrefabInstancePropertyModifications(visualRoot);
            }
            collider.center = snapshot.TargetColliderCenter;
            PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
        }

        var adjustedAssets = 0;
        foreach (var path in prefabPaths)
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(path);
            if (prefabRoot == null)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                WriteReport(report, "FAILED: Could not load prefab contents at " + path + ". Scene edits rolled back.");
                Debug.LogError("Motobike pivot recenter failed to load " + path + ".");
                return;
            }

            var visualRoots = GetVisualRoots(prefabRoot.transform);
            var collider = prefabRoot.GetComponent<BoxCollider>();
            if (visualRoots.Length == 0 || collider == null || !TryCalculateBounds(prefabRoot.transform, out var bounds))
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
                Undo.RevertAllDownToGroup(undoGroup);
                WriteReport(report, "FAILED: Incomplete prefab content at " + path + ". Scene edits rolled back.");
                Debug.LogError("Motobike pivot recenter found incomplete prefab contents at " + path + ".");
                return;
            }

            var pivotOffset = PivotOffset(bounds);
            var targetColliderCenter = bounds.center - pivotOffset;
            var needsMeshOffset = pivotOffset.sqrMagnitude > CenterEpsilon * CenterEpsilon;
            var needsColliderOffset = Vector3.Distance(collider.center, targetColliderCenter) > CenterEpsilon;
            if (needsMeshOffset)
            {
                foreach (var visualRoot in visualRoots)
                    visualRoot.localPosition -= pivotOffset;
            }
            if (needsMeshOffset || needsColliderOffset)
                adjustedAssets++;
            collider.center = targetColliderCenter;

            var saved = PrefabUtility.SaveAsPrefabAsset(prefabRoot, path, out var success);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            if (!success || saved == null)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                WriteReport(report, "FAILED: Could not save " + path + ". Scene edits rolled back.");
                Debug.LogError("Motobike pivot recenter could not save " + path + ".");
                return;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var meshMatrixFailures = new List<string>();
        var colliderFailures = new List<string>();
        var boundsCentersAfter = new List<Vector3>();
        foreach (var snapshot in snapshots)
        {
            var root = snapshot.Root.transform;
            var collider = snapshot.Root.GetComponent<BoxCollider>();
            if (collider == null || !TryCalculateBounds(root, out var bounds))
            {
                meshMatrixFailures.Add(snapshot.Root.name + " (missing after asset refresh)");
                continue;
            }

            boundsCentersAfter.Add(PivotOffset(bounds));
            var currentMatrices = CaptureMeshWorldMatrices(root);
            if (!MatricesMatch(snapshot.MeshWorldMatrices, currentMatrices, 0.001f))
                meshMatrixFailures.Add(snapshot.Root.name);
            if ((collider.size - snapshot.ColliderSize).sqrMagnitude > 0.000001f ||
                Vector3.Distance(collider.center, bounds.center) > 0.001f)
                colliderFailures.Add(snapshot.Root.name);
        }

        var allPrefabFailures = new List<string>();
        foreach (var path in prefabPaths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || PrefabUtility.GetPrefabAssetType(prefab) != PrefabAssetType.Variant ||
                GetVisualRoots(prefab.transform).Length == 0 || prefab.GetComponent<BoxCollider>() == null ||
                !TryCalculateBounds(prefab.transform, out var bounds) || PivotOffset(bounds).sqrMagnitude > 0.001f ||
                Vector3.Distance(prefab.GetComponent<BoxCollider>().center, bounds.center) > 0.001f)
                allPrefabFailures.Add(Path.GetFileNameWithoutExtension(path));
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);

        report.Add("Result: centered pivot assets and scene instances for " + snapshots.Count + " bikes.");
        report.Add("Variant prefab assets adjusted: " + adjustedAssets + "/17.");
        report.Add("Scene mesh world transforms preserved: " + (meshMatrixFailures.Count == 0 ? "yes" : "no: " + string.Join(", ", meshMatrixFailures)) + ".");
        report.Add("Collider size preserved and center aligned to mesh bounds: " + (colliderFailures.Count == 0 ? "yes" : "no: " + string.Join(", ", colliderFailures)) + ".");
        report.Add("Root pivot residual max (center X/Z, bottom Y): " + (boundsCentersAfter.Count == 0 ? "n/a" : boundsCentersAfter.Max(v => v.magnitude).ToString("0.000000", CultureInfo.InvariantCulture)) + ".");
        report.Add("Variant prefab failures: " + (allPrefabFailures.Count == 0 ? "none" : string.Join(", ", allPrefabFailures)) + ".");
        report.Add("Scene saved: no.");
        WriteReport(report);

        if (meshMatrixFailures.Count == 0 && colliderFailures.Count == 0 && allPrefabFailures.Count == 0)
            Debug.Log("Motobike pivot recenter completed. " + string.Join("; ", report.Take(5)) + " The Parking Area Scene remains unsaved.");
        else
            Debug.LogError("Motobike pivot recenter completed with verification issues. See Library/MotobikePivotRecenterAudit.txt.");
    }

    private static bool IsVariantInstance(GameObject gameObject)
    {
        var source = PrefabUtility.GetCorrespondingObjectFromSource(gameObject) as GameObject;
        var path = source != null ? AssetDatabase.GetAssetPath(source) : string.Empty;
        return path.StartsWith(VariantsFolder + "/Motobike ", StringComparison.OrdinalIgnoreCase) &&
               !path.Contains("/Legacy/");
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

    private static Vector3 PivotOffset(Bounds bounds)
    {
        return new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
    }

    private static Transform[] GetVisualRoots(Transform root)
    {
        var view = root.Find("View");
        if (view == null)
            return Array.Empty<Transform>();

        var visualRoot = view.Find("VisualRoot");
        return visualRoot != null ? new[] { visualRoot } : view.Cast<Transform>().ToArray();
    }

    private static Dictionary<string, Matrix4x4> CaptureMeshWorldMatrices(Transform root)
    {
        var result = new Dictionary<string, Matrix4x4>(StringComparer.Ordinal);
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh != null)
                result[GetRelativePath(root, filter.transform)] = filter.transform.localToWorldMatrix;
        }
        return result;
    }

    private static string GetRelativePath(Transform root, Transform target)
    {
        var names = new Stack<string>();
        for (var current = target; current != null && current != root; current = current.parent)
            names.Push(current.GetSiblingIndex().ToString(CultureInfo.InvariantCulture) + ":" + current.name);
        return string.Join("/", names);
    }

    private static bool MatricesMatch(Dictionary<string, Matrix4x4> expected, Dictionary<string, Matrix4x4> actual, float tolerance)
    {
        if (expected.Count != actual.Count)
            return false;
        foreach (var entry in expected)
        {
            if (!actual.TryGetValue(entry.Key, out var matrix))
                return false;
            for (var row = 0; row < 4; row++)
                for (var column = 0; column < 4; column++)
                    if (Mathf.Abs(entry.Value[row, column] - matrix[row, column]) > tolerance)
                        return false;
        }
        return true;
    }

    private static void WriteReport(IEnumerable<string> lines)
    {
        var reportPath = Path.Combine(Application.dataPath, "../Library/MotobikePivotRecenterAudit.txt");
        File.WriteAllLines(reportPath, lines);
    }

    private static void WriteReport(IEnumerable<string> lines, string finalLine)
    {
        WriteReport(lines.Concat(new[] { finalLine }));
    }
}
#endif
// Trigger one-shot editor pass.
