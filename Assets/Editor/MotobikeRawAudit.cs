#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class MotobikeRawAudit
{
    [MenuItem("Tools/Scene Tools/Audit Motobike Raw Models")]
    private static void Run()
    {
        var scene = SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true)).Where(t => t.name == "Motobike").ToArray();
        if (roots.Length != 1) { Debug.LogError("Motobike group not found uniquely"); return; }
        var report = new StringBuilder();
        report.AppendLine("SCENE=" + scene.path + " DIRECT_CHILDREN=" + roots[0].childCount);
        foreach (Transform t in roots[0])
        {
            if (PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject) is GameObject sourcePrefab)
            {
                var sourcePath = AssetDatabase.GetAssetPath(sourcePrefab);
                if (!sourcePath.Contains("/Variants/"))
                {
                    report.AppendLine($"PREFAB {t.name} path={sourcePath} active={t.gameObject.activeSelf} pos={V(t.localPosition)} euler={V(t.localEulerAngles)} scale={V(t.localScale)} assetEuler={V(sourcePrefab.transform.localEulerAngles)} assetScale={V(sourcePrefab.transform.localScale)}");
                    continue;
                }
            }
            if (!t.name.StartsWith("Assembly-", StringComparison.OrdinalIgnoreCase)) continue;
            var filters = t.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).ToArray();
            report.AppendLine($"\nROOT {t.name} active={t.gameObject.activeSelf} pos={V(t.localPosition)} euler={V(t.localEulerAngles)} scale={V(t.localScale)} descendantMeshes={filters.Length}");
            foreach (Transform child in t)
            {
                var childFilters = child.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).ToArray();
                var worldRotation = t.rotation * child.localRotation;
                var worldPosition = t.TransformPoint(child.localPosition);
                var yaw = worldRotation.eulerAngles.y;
                var correction = Quaternion.Euler(0, -yaw, 0) * worldRotation;
                report.AppendLine($"  CHILD {child.name} pos={V(child.localPosition)} euler={V(child.localEulerAngles)} scale={V(child.localScale)} worldPos={V(worldPosition)} worldEuler={V(worldRotation.eulerAngles)} qCorrectionEuler={V(correction.eulerAngles)} meshFilters={childFilters.Length}");
                foreach (var f in childFilters)
                {
                    var mesh = f.sharedMesh;
                    var renderer = f.GetComponent<Renderer>();
                    var mats = renderer != null ? string.Join(",", renderer.sharedMaterials.Select(m => m != null ? m.name : "null")) : "";
                    report.AppendLine($"    MESH {f.name} asset={AssetDatabase.GetAssetPath(mesh)} name={mesh.name} verts={mesh.vertexCount} bounds={V(mesh.bounds.size)} mats={mats}");
                }
            }
        }
        var path = Path.Combine(Application.dataPath, "../Library/MotobikeRawAudit.txt");
        File.WriteAllText(path, report.ToString());
        Debug.Log("Motobike audit saved to " + path + " (" + report.Length + " chars)");
    }

    private static string V(Vector3 v) => $"({v.x:F3},{v.y:F3},{v.z:F3})";
}
#endif
