using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityMeshSimplifier;

public static class QuestFactoryLODBuilder
{
    private const string FactoryScenePath = "Assets/Scenes/Factory Scene.unity";
    private const string ModelRootName = "Trong Nhà Máy_V1";
    private const string OutputPath = "Assets/Environments/Factory/GeneratedLOD";
    private const string MeshOutputPath = OutputPath + "/Meshes";
    private const float ChunkSize = 4.0f;
    private static readonly Dictionary<string, Mesh> chunkMeshCache = new Dictionary<string, Mesh>();
    private static readonly Dictionary<string, Mesh> lodMeshCache = new Dictionary<string, Mesh>();
    private static readonly Dictionary<int, Mesh> fixedMeshCache = new Dictionary<int, Mesh>();

    [MenuItem("Tools/Quest Mobile/Generate Factory LODs")]
    public static void GenerateFactoryLODs()
    {
        var scene = EditorSceneManager.GetActiveScene();
        bool opened = false;
        if (scene.path != FactoryScenePath)
        {
            scene = EditorSceneManager.OpenScene(FactoryScenePath, OpenSceneMode.Additive);
            opened = true;
        }

        try
        {
            var root = FindInScene(scene, ModelRootName);
            if (root == null)
                throw new InvalidOperationException("Could not find Factory model root: " + ModelRootName);
            RestorePreviousOutput(root);

            var navMesh = NavMesh.CalculateTriangulation();
            if (navMesh.vertices == null || navMesh.vertices.Length == 0 || navMesh.indices == null || navMesh.indices.Length < 3)
                throw new InvalidOperationException("No NavMesh polygons were found in the loaded Factory Scene. Refusing to generate broad-scope LODs.");

            EnsureFolder(OutputPath);
            EnsureFolder(MeshOutputPath);
            chunkMeshCache.Clear();
            lodMeshCache.Clear();
            fixedMeshCache.Clear();
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            var candidates = new List<MeshRenderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer.GetComponent<MeshFilter>() == null)
                    continue;
                if (!BoundsIntersectsNavMeshXZ(renderer.bounds, navMesh))
                    continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter.sharedMesh == null || !renderer.enabled)
                    continue;
                candidates.Add(renderer);
            }

            candidates.Sort((a, b) => string.CompareOrdinal(HierarchyPath(a.transform), HierarchyPath(b.transform)));
            var options = SimplificationOptions.Default;
            options.PreserveBorderEdges = false;
            options.PreserveUVSeamEdges = false;
            options.PreserveUVFoldoverEdges = false;
            options.EnableSmartLink = true;
            options.PreserveSurfaceCurvature = false;

            int generated = 0;
            int skipped = 0;
            int splitObjects = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                var sourceRenderer = candidates[i];
                if (sourceRenderer == null || !sourceRenderer.enabled)
                    continue;
                var sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
                var sourceMesh = sourceFilter != null ? sourceFilter.sharedMesh : null;
                if (sourceMesh == null)
                    continue;
                try
                {
                    if (TriangleCount(sourceMesh) > 50000)
                    {
                        var chunks = SplitRendererByGrid(sourceRenderer, sourceMesh);
                        if (chunks.Count == 0)
                        {
                            skipped++;
                            continue;
                        }
                        try
                        {
                            for (int c = 0; c < chunks.Count; c++)
                            {
                                var item = chunks[c];
                                if (BoundsIntersectsNavMeshXZ(item.renderer.bounds, navMesh))
                                {
                                    if (GenerateOneLOD(item.gameObject, item.renderer, item.mesh, options))
                                        generated++;
                                    else
                                        skipped++;
                                }
                            }
                            sourceRenderer.enabled = false;
                            splitObjects += chunks.Count;
                        }
                        catch
                        {
                            for (int c = 0; c < chunks.Count; c++)
                                if (chunks[c].gameObject != null) UnityEngine.Object.DestroyImmediate(chunks[c].gameObject);
                            throw;
                        }
                    }
                    else
                    {
                        if (GenerateOneLOD(sourceRenderer.gameObject, sourceRenderer, sourceMesh, options, true))
                            generated++;
                        else
                            skipped++;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("LOD failed for " + HierarchyPath(sourceRenderer.transform) + ": " + e);
                    skipped++;
                }
                if ((i + 1) % 10 == 0)
                    EditorUtility.DisplayProgressBar("Generating Factory LODs",
                        (i + 1) + " / " + candidates.Count + " objects", (float)(i + 1) / candidates.Count);
            }

            EditorUtility.ClearProgressBar();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Factory LOD generation complete. Generated groups: " + generated +
                      ", spatial chunks: " + splitObjects + ", skipped: " + skipped + ", source objects: " + candidates.Count +
                      ". Source FBX meshes and baked UVs remain unchanged.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (opened && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private sealed class ChunkOutput
    {
        public GameObject gameObject;
        public MeshRenderer renderer;
        public Mesh mesh;
    }

    private sealed class ChunkData
    {
        public readonly Dictionary<int, int> remap = new Dictionary<int, int>();
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<Vector3> normals = new List<Vector3>();
        public readonly List<Vector4> tangents = new List<Vector4>();
        public readonly List<Color> colors = new List<Color>();
        public readonly List<Vector4>[] uvs = new List<Vector4>[8];
        public readonly List<int>[] triangles;
        public ChunkData(int subMeshCount)
        {
            triangles = new List<int>[subMeshCount];
            for (int i = 0; i < subMeshCount; i++) triangles[i] = new List<int>();
            for (int i = 0; i < uvs.Length; i++) uvs[i] = new List<Vector4>();
        }
    }

    private static int TriangleCount(Mesh mesh)
    {
        long count = 0;
        for (int i = 0; i < mesh.subMeshCount; i++)
            if (mesh.GetTopology(i) == MeshTopology.Triangles) count += mesh.GetIndexCount(i) / 3;
        return count > int.MaxValue ? int.MaxValue : (int)count;
    }

    private static List<ChunkOutput> SplitRendererByGrid(MeshRenderer sourceRenderer, Mesh sourceMesh)
    {
        var sourceVertices = sourceMesh.vertices;
        if (sourceVertices == null || sourceVertices.Length == 0)
            throw new InvalidOperationException("Source mesh has no readable vertices: " + sourceMesh.name);
        var sourceNormals = sourceMesh.normals;
        var sourceTangents = sourceMesh.tangents;
        var sourceColors = sourceMesh.colors;
        var sourceUVs = new List<Vector4>[8];
        var hasUV = new bool[8];
        for (int channel = 0; channel < 8; channel++)
        {
            sourceUVs[channel] = new List<Vector4>();
            sourceMesh.GetUVs(channel, sourceUVs[channel]);
            hasUV[channel] = sourceUVs[channel].Count == sourceVertices.Length;
        }

        var chunks = new Dictionary<Vector3Int, ChunkData>();
        for (int subMesh = 0; subMesh < sourceMesh.subMeshCount; subMesh++)
        {
            if (sourceMesh.GetTopology(subMesh) != MeshTopology.Triangles)
                throw new InvalidOperationException("Non-triangle submesh found on " + sourceMesh.name + "; it was left untouched.");
            var indices = sourceMesh.GetIndices(subMesh);
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                int i0 = indices[i];
                int i1 = indices[i + 1];
                int i2 = indices[i + 2];
                var center = (sourceVertices[i0] + sourceVertices[i1] + sourceVertices[i2]) / 3f;
                var cell = new Vector3Int(
                    Mathf.FloorToInt((center.x - sourceMesh.bounds.min.x) / ChunkSize),
                    Mathf.FloorToInt((center.y - sourceMesh.bounds.min.y) / ChunkSize),
                    Mathf.FloorToInt((center.z - sourceMesh.bounds.min.z) / ChunkSize));
                ChunkData data;
                if (!chunks.TryGetValue(cell, out data))
                {
                    data = new ChunkData(sourceMesh.subMeshCount);
                    chunks.Add(cell, data);
                }

                // Repair confirmed triangle winding disagreements in the generated copy only.
                if (sourceNormals != null && sourceNormals.Length == sourceVertices.Length)
                {
                    var geometric = Vector3.Cross(sourceVertices[i1] - sourceVertices[i0], sourceVertices[i2] - sourceVertices[i0]);
                    var averageNormal = sourceNormals[i0] + sourceNormals[i1] + sourceNormals[i2];
                    if (geometric.sqrMagnitude > 1e-12f && averageNormal.sqrMagnitude > 1e-12f &&
                        Vector3.Dot(geometric.normalized, averageNormal.normalized) < -0.25f)
                    {
                        int swap = i1;
                        i1 = i2;
                        i2 = swap;
                    }
                }

                data.triangles[subMesh].Add(AddVertex(data, i0, sourceVertices, sourceNormals, sourceTangents, sourceColors, sourceUVs, hasUV));
                data.triangles[subMesh].Add(AddVertex(data, i1, sourceVertices, sourceNormals, sourceTangents, sourceColors, sourceUVs, hasUV));
                data.triangles[subMesh].Add(AddVertex(data, i2, sourceVertices, sourceNormals, sourceTangents, sourceColors, sourceUVs, hasUV));
            }
        }

        var result = new List<ChunkOutput>(chunks.Count);
        var cells = new List<Vector3Int>(chunks.Keys);
        cells.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : (a.y != b.y ? a.y.CompareTo(b.y) : a.z.CompareTo(b.z)));
        string sourceKey = sourceMesh.GetInstanceID().ToString();
        for (int c = 0; c < cells.Count; c++)
        {
            var cell = cells[c];
            var data = chunks[cell];
            var cacheKey = sourceKey + "_" + cell.x + "_" + cell.y + "_" + cell.z;
            Mesh chunkMesh;
            if (!chunkMeshCache.TryGetValue(cacheKey, out chunkMesh))
            {
                chunkMesh = new Mesh();
                chunkMesh.name = sourceMesh.name + "_Chunk_" + cell.x + "_" + cell.y + "_" + cell.z;
                if (data.vertices.Count > 65535) chunkMesh.indexFormat = IndexFormat.UInt32;
                chunkMesh.SetVertices(data.vertices);
                if (data.normals.Count == data.vertices.Count && data.normals.Count > 0) chunkMesh.SetNormals(data.normals);
                if (data.tangents.Count == data.vertices.Count && data.tangents.Count > 0) chunkMesh.SetTangents(data.tangents);
                if (data.colors.Count == data.vertices.Count && data.colors.Count > 0) chunkMesh.SetColors(data.colors);
                chunkMesh.subMeshCount = sourceMesh.subMeshCount;
                for (int subMesh = 0; subMesh < data.triangles.Length; subMesh++)
                    chunkMesh.SetTriangles(data.triangles[subMesh], subMesh, false);
                for (int channel = 0; channel < 8; channel++)
                    if (hasUV[channel]) chunkMesh.SetUVs(channel, data.uvs[channel]);
                chunkMesh.RecalculateBounds();
                string assetPath = AssetDatabase.GenerateUniqueAssetPath(
                    MeshOutputPath + "/" + SafeName(chunkMesh.name) + ".asset");
                AssetDatabase.CreateAsset(chunkMesh, assetPath);
                chunkMeshCache.Add(cacheKey, chunkMesh);
            }

            var chunkObject = new GameObject("_QuestChunk_" + cell.x + "_" + cell.y + "_" + cell.z);
            chunkObject.transform.SetParent(sourceRenderer.transform, false);
            var filter = chunkObject.AddComponent<MeshFilter>();
            filter.sharedMesh = chunkMesh;
            var renderer = chunkObject.AddComponent<MeshRenderer>();
            CopyRendererSettings(sourceRenderer, renderer);
            result.Add(new ChunkOutput { gameObject = chunkObject, renderer = renderer, mesh = chunkMesh });
        }
        return result;
    }

    private static int AddVertex(ChunkData data, int sourceIndex, Vector3[] vertices, Vector3[] normals,
        Vector4[] tangents, Color[] colors, List<Vector4>[] uvs, bool[] hasUV)
    {
        int mapped;
        if (data.remap.TryGetValue(sourceIndex, out mapped)) return mapped;
        mapped = data.vertices.Count;
        data.remap.Add(sourceIndex, mapped);
        data.vertices.Add(vertices[sourceIndex]);
        if (normals != null && normals.Length == vertices.Length) data.normals.Add(normals[sourceIndex]);
        if (tangents != null && tangents.Length == vertices.Length) data.tangents.Add(tangents[sourceIndex]);
        if (colors != null && colors.Length == vertices.Length) data.colors.Add(colors[sourceIndex]);
        for (int channel = 0; channel < 8; channel++)
            if (hasUV[channel]) data.uvs[channel].Add(uvs[channel][sourceIndex]);
        return mapped;
    }

    private static bool GenerateOneLOD(GameObject target, MeshRenderer renderer, Mesh sourceMesh,
        SimplificationOptions options, bool wrapOriginal = false)
    {
        GameObject lodObject = target;
        MeshRenderer lodRenderer = renderer;
        if (wrapOriginal)
        {
            lodObject = new GameObject("_QuestLOD_" + SafeName(renderer.gameObject.name));
            lodObject.transform.SetParent(renderer.transform, false);
            var filter = lodObject.AddComponent<MeshFilter>();
            filter.sharedMesh = CreateNormalFixedCopy(sourceMesh);
            sourceMesh = filter.sharedMesh;
            lodRenderer = lodObject.AddComponent<MeshRenderer>();
            CopyRendererSettings(renderer, lodRenderer);
        }

        var lods = new LODLevel[]
        {
            new LODLevel(0.60f, 1.00f),
            new LODLevel(0.25f, 0.45f),
            new LODLevel(0.08f, 0.18f)
        };
        for (int i = 0; i < lods.Length; i++) lods[i].Renderers = new Renderer[] { lodRenderer };
        try
        {
            var group = LODGenerator.GenerateLODs(lodObject, lods, false, options, OutputPath);
            if (group == null) return false;
            group.RecalculateBounds();
            ShareLODMeshes(group, sourceMesh);
            if (wrapOriginal) renderer.enabled = false;
            return true;
        }
        catch
        {
            if (wrapOriginal) UnityEngine.Object.DestroyImmediate(lodObject);
            throw;
        }
    }

    private static Mesh CreateNormalFixedCopy(Mesh source)
    {
        Mesh cached;
        if (fixedMeshCache.TryGetValue(source.GetInstanceID(), out cached)) return cached;
        var vertices = source.vertices;
        var normals = source.normals;
        if (vertices == null || normals == null || vertices.Length == 0 || normals.Length != vertices.Length)
        {
            fixedMeshCache[source.GetInstanceID()] = source;
            return source;
        }

        Mesh copy = null;
        for (int subMesh = 0; subMesh < source.subMeshCount; subMesh++)
        {
            if (source.GetTopology(subMesh) != MeshTopology.Triangles) continue;
            var indices = source.GetIndices(subMesh);
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
            {
                if (copy == null) copy = UnityEngine.Object.Instantiate(source);
                copy.SetIndices(indices, MeshTopology.Triangles, subMesh, false);
            }
        }

        if (copy == null)
        {
            fixedMeshCache[source.GetInstanceID()] = source;
            return source;
        }
        copy.name = source.name + "_NormalsFixed";
        copy.RecalculateBounds();
        string path = AssetDatabase.GenerateUniqueAssetPath(MeshOutputPath + "/" + SafeName(copy.name) + ".asset");
        AssetDatabase.CreateAsset(copy, path);
        fixedMeshCache[source.GetInstanceID()] = copy;
        return copy;
    }

private static void ShareLODMeshes(LODGroup group, Mesh sourceMesh)
    {
        var lods = group.GetLODs();
        for (int level = 0; level < lods.Length; level++)
        {
            var renderers = lods[level].renderers;
            for (int i = 0; i < renderers.Length; i++)
            {
                var filter = renderers[i].GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                if (level > 0)
                    FixWindingInPlace(filter.sharedMesh);
                string key = sourceMesh.GetInstanceID() + "_LOD" + level + "_" + i;
                Mesh shared;
                if (lodMeshCache.TryGetValue(key, out shared)) filter.sharedMesh = shared;
                else lodMeshCache.Add(key, filter.sharedMesh);
            }
        }
        group.SetLODs(lods);
    }

    private static void CopyRendererSettings(MeshRenderer source, MeshRenderer target)
    {
        target.sharedMaterials = source.sharedMaterials;
        target.shadowCastingMode = source.shadowCastingMode;
        target.receiveShadows = source.receiveShadows;
        target.lightProbeUsage = source.lightProbeUsage;
        target.reflectionProbeUsage = source.reflectionProbeUsage;
        target.lightmapIndex = source.lightmapIndex;
        target.realtimeLightmapIndex = source.realtimeLightmapIndex;
        target.lightmapScaleOffset = source.lightmapScaleOffset;
        target.realtimeLightmapScaleOffset = source.realtimeLightmapScaleOffset;
        target.probeAnchor = source.probeAnchor;
        target.lightProbeProxyVolumeOverride = source.lightProbeProxyVolumeOverride;
        target.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
        target.renderingLayerMask = source.renderingLayerMask;
        target.sortingLayerID = source.sortingLayerID;
        target.sortingOrder = source.sortingOrder;
        var block = new MaterialPropertyBlock();
        source.GetPropertyBlock(block);
        target.SetPropertyBlock(block);
    }

    private static string SafeName(string value)
    {
        foreach (char c in System.IO.Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
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

    private static GameObject FindInScene(UnityEngine.SceneManagement.Scene scene, string name)
    {
        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var all = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < all.Length; j++)
                if (all[j].name == name)
                    return all[j].gameObject;
        }
        return null;
    }

    private static string HierarchyPath(Transform t)
    {
        var path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    private static bool BoundsIntersectsNavMeshXZ(Bounds bounds, NavMeshTriangulation navMesh)
    {
        var center = new Vector2(bounds.center.x, bounds.center.z);
        var half = new Vector2(bounds.extents.x, bounds.extents.z);
        for (int i = 0; i + 2 < navMesh.indices.Length; i += 3)
        {
            var va = navMesh.vertices[navMesh.indices[i]];
            var vb = navMesh.vertices[navMesh.indices[i + 1]];
            var vc = navMesh.vertices[navMesh.indices[i + 2]];
            var a = new Vector2(va.x, va.z) - center;
            var b = new Vector2(vb.x, vb.z) - center;
            var c = new Vector2(vc.x, vc.z) - center;
            if (AxisOverlaps(a, b, c, half, Vector2.right) &&
                AxisOverlaps(a, b, c, half, Vector2.up) &&
                AxisOverlaps(a, b, c, half, new Vector2(-(b.y - a.y), b.x - a.x)) &&
                AxisOverlaps(a, b, c, half, new Vector2(-(c.y - b.y), c.x - b.x)) &&
                AxisOverlaps(a, b, c, half, new Vector2(-(a.y - c.y), a.x - c.x)))
                return true;
        }
        return false;
    }

    private static bool AxisOverlaps(Vector2 a, Vector2 b, Vector2 c, Vector2 half, Vector2 axis)
    {
        float length = axis.magnitude;
        if (length < 0.000001f)
            return true;
        axis /= length;
        float pa = Vector2.Dot(a, axis);
        float pb = Vector2.Dot(b, axis);
        float pc = Vector2.Dot(c, axis);
        float min = Mathf.Min(pa, Mathf.Min(pb, pc));
        float max = Mathf.Max(pa, Mathf.Max(pb, pc));
        float radius = half.x * Mathf.Abs(axis.x) + half.y * Mathf.Abs(axis.y);
        return min <= radius && max >= -radius;
    }


private static void RestorePreviousOutput(GameObject root)
    {
        var previousGroups = root.GetComponentsInChildren<LODGroup>(true);
        for (int i = 0; i < previousGroups.Length; i++)
        {
            var group = previousGroups[i];
            if (group == null)
                continue;
            if (!LODGenerator.DestroyLODs(group.gameObject))
                throw new InvalidOperationException("Could not restore an existing generated LOD group on " + HierarchyPath(group.transform));
        }

        var generatedObjects = new List<Transform>();
        var allTransforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            var t = allTransforms[i];
            if (t != root.transform && (t.name.StartsWith("_QuestChunk_", StringComparison.Ordinal) ||
                                        t.name.StartsWith("_QuestLOD_", StringComparison.Ordinal)))
                generatedObjects.Add(t);
        }
        generatedObjects.Sort((a, b) => GetDepth(b).CompareTo(GetDepth(a)));
        for (int i = 0; i < generatedObjects.Count; i++)
            if (generatedObjects[i] != null)
                UnityEngine.Object.DestroyImmediate(generatedObjects[i].gameObject);

        // Before the first generated pass, every source renderer under this model root was enabled.
        // Restore those originals after removing our temporary LOD wrappers and spatial chunks.
        var sourceRenderers = root.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < sourceRenderers.Length; i++)
            if (sourceRenderers[i] != null && sourceRenderers[i].GetComponent<MeshFilter>() != null)
                sourceRenderers[i].enabled = true;

        if (AssetDatabase.IsValidFolder(OutputPath) && !AssetDatabase.DeleteAsset(OutputPath))
            throw new InvalidOperationException("Could not replace generated output folder: " + OutputPath);
    }

    private static int GetDepth(Transform t)
    {
        int depth = 0;
        while (t.parent != null)
        {
            depth++;
            t = t.parent;
        }
        return depth;
    }


private static void FixWindingInPlace(Mesh mesh)
    {
        if (mesh == null)
            return;
        var vertices = mesh.vertices;
        var normals = mesh.normals;
        if (vertices == null || normals == null || vertices.Length == 0 || normals.Length != vertices.Length)
            return;

        bool meshChanged = false;
        for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
        {
            if (mesh.GetTopology(subMesh) != MeshTopology.Triangles)
                continue;
            var indices = mesh.GetIndices(subMesh);
            bool subMeshChanged = false;
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
                    subMeshChanged = true;
                }
            }
            if (subMeshChanged)
            {
                mesh.SetIndices(indices, MeshTopology.Triangles, subMesh, false);
                meshChanged = true;
            }
        }
        if (meshChanged)
        {
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
        }
    }
}
