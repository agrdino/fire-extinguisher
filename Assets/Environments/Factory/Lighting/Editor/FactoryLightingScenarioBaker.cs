using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace _Scripts.Environments.Factory.Editor
{
    public static class FactoryLightingScenarioBaker
    {
        private const string FactoryScenePath = "Assets/Scenes/Factory Scene.unity";
        private const string LightingSettingsPath =
            "Assets/Environments/Factory/Lighting/Factory_Quest_LightingSettings.lighting";
        private const string ScenarioFolder = "Assets/Environments/Factory/Lighting/Scenarios";
        private const string NormalScenarioPath = ScenarioFolder + "/Factory Lighting - Normal.asset";
        private const string BlackoutScenarioPath = ScenarioFolder + "/Factory Lighting - Blackout.asset";
        private const string EmergencyPrefabPath =
            "Assets/Environments/Factory/Props/EmergencyLighting/Emergency Wall Light.prefab";
        private const string ExitSignPrefabPath =
            "Assets/Environments/Factory/Props/Exit Sign/Exit Sign.prefab";

        private const float NormalAmbientIntensity = 1f;
        private const float NormalReflectionIntensity = 0.85f;
        private const float BlackoutAmbientIntensity = 0.02f;
        private const float BlackoutReflectionIntensity = 0.15f;

        [MenuItem("Tools/Factory Lighting/1. Prepare WebGL Baked Lighting")]
        public static void PrepareWebGlBakedLighting()
        {
            EnsureFactoryScene();
            EnsureFolder(ScenarioFolder);
            ConfigurePrefabLightsAsBaked(EmergencyPrefabPath);
            ConfigurePrefabLightsAsBaked(ExitSignPrefabPath);

            LightingSettings lightingSettings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingSettingsPath);
            if (lightingSettings == null)
                throw new InvalidOperationException("Missing factory LightingSettings asset: " + LightingSettingsPath);

            Lightmapping.lightingSettings = lightingSettings;
            LightmapSettings.lightmapsMode = LightmapsMode.NonDirectional;
            EnsureLegacyLightProbeGrid();

            Light[] lights = FindSceneObjects<Light>();
            foreach (Light light in lights)
            {
                light.lightmapBakeType = LightmapBakeType.Baked;

                if (light.type == LightType.Spot || light.type == LightType.Point)
                    light.shadows = LightShadows.None;
                else
                {
                    light.shadows = LightShadows.Soft;
                    light.shadowStrength = Mathf.Min(light.shadowStrength, 0.62f);
                }

                EditorUtility.SetDirty(light);
            }

            FactoryPowerLightingController controller =
                UnityEngine.Object.FindFirstObjectByType<FactoryPowerLightingController>(FindObjectsInactive.Include);
            if (controller == null)
                throw new InvalidOperationException("FactoryPowerLightingController was not found in Factory Scene.");

            FactoryBakedLightingSwitcher switcher = controller.GetComponent<FactoryBakedLightingSwitcher>();
            if (switcher == null)
                switcher = Undo.AddComponent<FactoryBakedLightingSwitcher>(controller.gameObject);

            FactoryLightingScenario normalScenario = LoadOrCreateScenario(NormalScenarioPath);
            FactoryLightingScenario blackoutScenario = LoadOrCreateScenario(BlackoutScenarioPath);
            switcher.ConfigureEditor(normalScenario, blackoutScenario);

            var controllerObject = new SerializedObject(controller);
            controllerObject.FindProperty("_bakedLighting").objectReferenceValue = switcher;
            controllerObject.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(switcher);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Factory Lighting/2. Configure Normal Bake")]
        public static void ConfigureNormalBake()
        {
            EnsureFactoryScene();
            ConfigureBakeState(false);
        }

        [MenuItem("Tools/Factory Lighting/3. Capture Normal Bake")]
        public static void CaptureNormalBake()
        {
            CaptureScenario(NormalScenarioPath, "Normal", NormalAmbientIntensity, NormalReflectionIntensity);
        }

        [MenuItem("Tools/Factory Lighting/4. Configure Blackout Bake")]
        public static void ConfigureBlackoutBake()
        {
            EnsureFactoryScene();
            ConfigureBakeState(true);
        }

        [MenuItem("Tools/Factory Lighting/5. Capture Blackout + Finalize")]
        public static void CaptureBlackoutBakeAndFinalize()
        {
            FactoryLightingScenario blackout = CaptureScenario(
                BlackoutScenarioPath,
                "Blackout",
                BlackoutAmbientIntensity,
                BlackoutReflectionIntensity);
            FactoryLightingScenario normal = AssetDatabase.LoadAssetAtPath<FactoryLightingScenario>(NormalScenarioPath);

            if (normal != null && !string.Equals(
                    normal.RendererLayoutSignature,
                    blackout.RendererLayoutSignature,
                    StringComparison.Ordinal))
                Debug.LogWarning("Factory lighting scenarios use different renderer lightmap layouts. " +
                                 "Rebake without changing geometry between the two bakes.");

            ConfigureRuntimeSourceState();
            if (normal != null) normal.Apply();

            FactoryBakedLightingSwitcher switcher =
                UnityEngine.Object.FindFirstObjectByType<FactoryBakedLightingSwitcher>(FindObjectsInactive.Include);
            if (switcher != null)
            {
                switcher.ConfigureEditor(normal, blackout);
                switcher.SetBlackout(false);
                EditorUtility.SetDirty(switcher);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ConfigureRuntimeSourceState()
        {
            HashSet<Light> ceilingLights = CollectChildLights<CeilLight>();

            foreach (CeilLight ceilingLight in FindSceneObjects<CeilLight>())
            {
                ceilingLight.SetPowered(true);
                EditorUtility.SetDirty(ceilingLight);
            }

            foreach (Light light in FindSceneObjects<Light>())
            {
                light.enabled = false;
                if (ceilingLights.Contains(light))
                    light.gameObject.SetActive(false);
                EditorUtility.SetDirty(light);
            }

            RenderSettings.ambientIntensity = NormalAmbientIntensity;
            RenderSettings.reflectionIntensity = NormalReflectionIntensity;
        }

        private static void EnsureLegacyLightProbeGrid()
        {
            const string probeGroupName = "Factory Legacy Light Probes";
            const float horizontalPadding = 1.5f;
            const float targetSpacing = 7.5f;

            Renderer[] renderers = FindSceneObjects<Renderer>();
            bool hasBounds = false;
            Bounds factoryBounds = default;

            foreach (Renderer renderer in renderers)
            {
                if (!renderer.gameObject.activeInHierarchy || renderer is ParticleSystemRenderer ||
                    renderer is TrailRenderer || renderer is LineRenderer)
                    continue;

                if (!hasBounds)
                {
                    factoryBounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                    factoryBounds.Encapsulate(renderer.bounds);
            }

            if (!hasBounds) return;

            GameObject probeObject = GameObject.Find(probeGroupName);
            if (probeObject == null)
            {
                probeObject = new GameObject(probeGroupName);
                Undo.RegisterCreatedObjectUndo(probeObject, "Create factory legacy light probes");

                GameObject environmentRoot = GameObject.Find("Factory Environment");
                if (environmentRoot != null)
                    probeObject.transform.SetParent(environmentRoot.transform, true);
            }

            LightProbeGroup probeGroup = probeObject.GetComponent<LightProbeGroup>();
            if (probeGroup == null)
                probeGroup = Undo.AddComponent<LightProbeGroup>(probeObject);

            float minX = factoryBounds.min.x + horizontalPadding;
            float maxX = factoryBounds.max.x - horizontalPadding;
            float minZ = factoryBounds.min.z + horizontalPadding;
            float maxZ = factoryBounds.max.z - horizontalPadding;
            int xCount = Mathf.Max(2, Mathf.CeilToInt((maxX - minX) / targetSpacing) + 1);
            int zCount = Mathf.Max(2, Mathf.CeilToInt((maxZ - minZ) / targetSpacing) + 1);
            float[] probeHeights = { 0.6f, 1.8f, 4f };
            var positions = new List<Vector3>(xCount * zCount * probeHeights.Length);

            for (int xIndex = 0; xIndex < xCount; xIndex++)
            {
                float x = Mathf.Lerp(minX, maxX, xIndex / (float)(xCount - 1));
                for (int zIndex = 0; zIndex < zCount; zIndex++)
                {
                    float z = Mathf.Lerp(minZ, maxZ, zIndex / (float)(zCount - 1));
                    foreach (float height in probeHeights)
                    {
                        Vector3 worldPosition = new Vector3(x, height, z);
                        positions.Add(probeObject.transform.InverseTransformPoint(worldPosition));
                    }
                }
            }

            probeGroup.probePositions = positions.ToArray();
            EditorUtility.SetDirty(probeGroup);
            EditorUtility.SetDirty(probeObject);
        }

        private static void ConfigureBakeState(bool blackout)
        {
            HashSet<Light> ceilingLights = CollectChildLights<CeilLight>();
            HashSet<Light> emergencyLights = CollectChildLights<FactoryEmergencyLightFixture>();
            Light[] allLights = FindSceneObjects<Light>();

            foreach (CeilLight ceilingLight in FindSceneObjects<CeilLight>())
            {
                ceilingLight.SetPowered(!blackout);
                EditorUtility.SetDirty(ceilingLight);
            }

            foreach (Light light in allLights)
            {
                light.lightmapBakeType = LightmapBakeType.Baked;

                bool isCeilingLight = ceilingLights.Contains(light);
                bool isEmergencyLight = emergencyLights.Contains(light);
                bool shouldContribute = blackout ? isEmergencyLight : !isEmergencyLight;

                if (isCeilingLight)
                    light.gameObject.SetActive(!blackout);

                light.enabled = shouldContribute;
                EditorUtility.SetDirty(light);
            }

            RenderSettings.ambientIntensity = blackout
                ? BlackoutAmbientIntensity
                : NormalAmbientIntensity;
            RenderSettings.reflectionIntensity = blackout
                ? BlackoutReflectionIntensity
                : NormalReflectionIntensity;

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            SceneView.RepaintAll();
        }

        private static FactoryLightingScenario CaptureScenario(
            string scenarioPath,
            string scenarioName,
            float ambientIntensity,
            float reflectionIntensity)
        {
            EnsureFactoryScene();
            EnsureFolder(ScenarioFolder);

            LightmapData[] sourceLightmaps = LightmapSettings.lightmaps;
            if (sourceLightmaps == null || sourceLightmaps.Length == 0)
                throw new InvalidOperationException("No baked lightmaps are loaded. Complete the bake before capture.");

            var colorLightmaps = new Texture2D[sourceLightmaps.Length];
            var directionalLightmaps = new Texture2D[sourceLightmaps.Length];
            var shadowMasks = new Texture2D[sourceLightmaps.Length];

            for (int index = 0; index < sourceLightmaps.Length; index++)
            {
                colorLightmaps[index] = CopyTexture(
                    sourceLightmaps[index].lightmapColor,
                    scenarioName + "_Lightmap_" + index + "_Color");
                directionalLightmaps[index] = CopyTexture(
                    sourceLightmaps[index].lightmapDir,
                    scenarioName + "_Lightmap_" + index + "_Direction");
                shadowMasks[index] = CopyTexture(
                    sourceLightmaps[index].shadowMask,
                    scenarioName + "_Lightmap_" + index + "_ShadowMask");
            }

            LightProbes lightProbes = LightmapSettings.lightProbes;
            SphericalHarmonicsL2[] bakedProbes = lightProbes != null
                ? lightProbes.bakedProbes
                : Array.Empty<SphericalHarmonicsL2>();

            FactoryLightingScenario scenario = LoadOrCreateScenario(scenarioPath);
            scenario.CaptureEditorData(
                LightmapSettings.lightmapsMode,
                colorLightmaps,
                directionalLightmaps,
                shadowMasks,
                bakedProbes,
                ambientIntensity,
                reflectionIntensity,
                CalculateRendererLayoutSignature());

            EditorUtility.SetDirty(scenario);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Captured {scenarioName} factory lighting: " +
                      $"{scenario.LightmapCount} lightmaps, {scenario.BakedProbeCount} probes.");
            return scenario;
        }

        private static Texture2D CopyTexture(Texture2D source, string destinationName)
        {
            if (source == null) return null;

            string sourcePath = AssetDatabase.GetAssetPath(source);
            string extension = System.IO.Path.GetExtension(sourcePath);
            string destinationPath = ScenarioFolder + "/" + destinationName + extension;

            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(destinationPath) != null)
                AssetDatabase.DeleteAsset(destinationPath);

            if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
                throw new InvalidOperationException(
                    $"Could not copy baked texture from '{sourcePath}' to '{destinationPath}'.");

            AssetDatabase.ImportAsset(destinationPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(destinationPath);
        }

        private static void ConfigurePrefabLightsAsBaked(string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                foreach (Light light in root.GetComponentsInChildren<Light>(true))
                {
                    light.lightmapBakeType = LightmapBakeType.Baked;
                    if (light.type == LightType.Spot || light.type == LightType.Point)
                        light.shadows = LightShadows.None;
                    EditorUtility.SetDirty(light);
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static HashSet<Light> CollectChildLights<T>() where T : Component
        {
            var result = new HashSet<Light>();
            foreach (T component in FindSceneObjects<T>())
            {
                foreach (Light light in component.GetComponentsInChildren<Light>(true))
                    result.Add(light);
            }

            return result;
        }

        private static T[] FindSceneObjects<T>() where T : UnityEngine.Object
        {
            T[] objects = UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            var sceneObjects = new List<T>(objects.Length);

            foreach (T item in objects)
            {
                if (item is Component component && component.gameObject.scene == SceneManager.GetActiveScene())
                    sceneObjects.Add(item);
            }

            return sceneObjects.ToArray();
        }

        private static FactoryLightingScenario LoadOrCreateScenario(string path)
        {
            FactoryLightingScenario scenario = AssetDatabase.LoadAssetAtPath<FactoryLightingScenario>(path);
            if (scenario != null) return scenario;

            scenario = ScriptableObject.CreateInstance<FactoryLightingScenario>();
            AssetDatabase.CreateAsset(scenario, path);
            return scenario;
        }

        private static string CalculateRendererLayoutSignature()
        {
            Renderer[] renderers = FindSceneObjects<Renderer>();
            Array.Sort(renderers, (left, right) =>
                string.CompareOrdinal(GetHierarchyPath(left.transform), GetHierarchyPath(right.transform)));

            var builder = new StringBuilder(renderers.Length * 48);
            foreach (Renderer renderer in renderers)
            {
                if (renderer.lightmapIndex < 0 || renderer.lightmapIndex >= 0xFFFE) continue;

                Vector4 scaleOffset = renderer.lightmapScaleOffset;
                builder.Append(GetHierarchyPath(renderer.transform)).Append('|')
                    .Append(renderer.lightmapIndex).Append('|')
                    .Append(scaleOffset.x.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(scaleOffset.y.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(scaleOffset.z.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(scaleOffset.w.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            }

            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
            return BitConverter.ToString(hash).Replace("-", string.Empty);
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }

        private static void EnsureFactoryScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path == FactoryScenePath) return;

            if (scene.isDirty && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                throw new InvalidOperationException("Factory lighting preparation was cancelled.");

            EditorSceneManager.OpenScene(FactoryScenePath, OpenSceneMode.Single);
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            string parent = System.IO.Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            string folderName = System.IO.Path.GetFileName(folderPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folderName))
                throw new InvalidOperationException("Invalid asset folder path: " + folderPath);

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
