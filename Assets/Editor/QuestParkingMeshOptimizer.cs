using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityMeshSimplifier;

public static class QuestParkingMeshOptimizer
{
    private const string ScenePath = "Assets/Scenes/Park Scene.unity";
    private const string TargetPath = "Parking Area Environment/Geometry/Assembly-2/SimLab_skp/Assembly-10899";
    private const string OutputPath = "Assets/Environments/Park/OptimizedParkingMeshes";

    [MenuItem("Tools/Quest Mobile/Optimize Parking Area Meshes")]
    public static void OptimizeParkingArea()
    {
        var scene = EditorSceneManager.GetActiveScene();
        bool opened = false;
        if (scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            opened = true;
        }

        try
        {
            var target = FindPath(scene, TargetPath);
            if (target == null)
                throw new InvalidOperationException("Could not find Parking Area model root: " + TargetPath);
            if (AssetDatabase.IsValidFolder(OutputPath))
                throw new InvalidOperationException("Parking mesh output already exists. Refusing to simplify generated meshes again: " + OutputPath);

            EnsureFolder(OutputPath);
            var filters = target.GetComponentsInChildren<MeshFilter>(true);
            var meshCache = new Dictionary<int, Mesh>();
            var options = SimplificationOptions.Default;
            options.PreserveBorderEdges = false;
            options.PreserveUVSeamEdges = false;
            options.PreserveUVFoldoverEdges = false;
            options.PreserveSurfaceCurvature = false;
            options.EnableSmartLink = true;

            int changed = 0, sharedReferences = 0, failed = 0;
            long before = 0, after = 0;
            for (int i = 0; i < filters.Length; i++)
            {
                var filter = filters[i];
                var source = filter != null ? filter.sharedMesh : null;
                if (source == null)
                    continue;
                before += TriangleCount(source);

                Mesh optimized;
                if (!meshCache.TryGetValue(source.GetInstanceID(), out optimized))
                {
                    try
                    {
                        var simplifier = new MeshSimplifier(source);
                        simplifier.SimplificationOptions = options;
                        simplifier.SimplifyMesh(0.60f);
                        optimized = simplifier.ToMesh();
                        if (optimized == null || TriangleCount(optimized) == 0)
                            throw new InvalidOperationException("Simplifier returned an empty mesh for " + source.name);
                        optimized.name = source.name + "_Parking_60pct";
                        FixWindingAgainstNormals(optimized);
                        var assetPath = AssetDatabase.GenerateUniqueAssetPath(
                            OutputPath + "/" + SafeName(optimized.name) + ".asset");
                        AssetDatabase.CreateAsset(optimized, assetPath);
                        meshCache.Add(source.GetInstanceID(), optimized);
                        changed++;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("Parking mesh optimization failed for " + source.name + ": " + e);
                        failed++;
                        continue;
                    }
                }
                else
                {
                    sharedReferences++;
                }

                filter.sharedMesh = optimized;
                after += TriangleCount(optimized);
                if ((i + 1) % 50 == 0)
                    EditorUtility.DisplayProgressBar("Optimizing Parking Area", (i + 1) + " / " + filters.Length + " mesh filters",
                        (float)(i + 1) / filters.Length);
            }

            EditorUtility.ClearProgressBar();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Parking mesh optimization complete. Unique meshes: " + changed + ", shared references: " +
                      sharedReferences + ", failed: " + failed + ", renderers: " + filters.Length +
                      ", triangles: " + before + " -> " + after + ". No LODGroups added; source FBX meshes unchanged.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (opened && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void FixWindingAgainstNormals(Mesh mesh)
    {
        var vertices = mesh.vertices;
        var normals = mesh.normals;
        if (vertices == null || normals == null || vertices.Length == 0 || normals.Length != vertices.Length)
            return;

        for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
        {
            if (mesh.GetTopology(subMesh) != MeshTopology.Triangles)
                continue;
            var indices = mesh.GetIndices(subMesh);
            bool changed = false;
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                int i0 = indices[i], i1 = indices[i + 1], i2 = indices[i + 2];
                var geometric = Vector3.Cross(vertices[i1] - vertices[i0], vertices[i2] - vertices[i0]);
                var averageNormal = normals[i0] + normals[i1] + normals[i2];
                if (geometric.sqrMagnitude > 1e-12f && averageNormal.sqrMagnitude > 1e-12f &&
                    Vector3.Dot(geometric.normalized, averageNormal.normalized) < -0.25f)
                {
                    indices[i + 1] = i2;
                    indices[i + 2] = i1;
                    changed = true;
                }
            }
            if (changed)
                mesh.SetIndices(indices, MeshTopology.Triangles, subMesh, false);
        }
        mesh.RecalculateBounds();
    }

    private static long TriangleCount(Mesh mesh)
    {
        long count = 0;
        for (int i = 0; i < mesh.subMeshCount; i++)
            if (mesh.GetTopology(i) == MeshTopology.Triangles)
                count += mesh.GetIndexCount(i) / 3;
        return count;
    }

    private static string SafeName(string value)
    {
        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        return value.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        var parts = path.Split('/');
        var current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static GameObject FindPath(UnityEngine.SceneManagement.Scene scene, string path)
    {
        var parts = path.Split('/');
        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name != parts[0])
                continue;
            Transform current = roots[i].transform;
            for (int p = 1; p < parts.Length && current != null; p++)
            {
                Transform next = null;
                for (int c = 0; c < current.childCount; c++)
                    if (current.GetChild(c).name == parts[p])
                    {
                        next = current.GetChild(c);
                        break;
                    }
                current = next;
            }
            if (current != null)
                return current.gameObject;
        }
        return null;
    }
}