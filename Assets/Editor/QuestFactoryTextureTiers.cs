using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class QuestFactoryTextureTiers
{
    private const string ScenePath = "Assets/Scenes/Factory Scene.unity";
    private const string ModelRootName = "Trong Nhà Máy_V1";
    private const string OutputPath = "Assets/Environments/Factory/GeneratedLOD/TextureTiers";
    private const string TexturePath = OutputPath + "/Textures";
    private const string MaterialPath = OutputPath + "/Materials";
    private static readonly Dictionary<string, string> textureFiles = new Dictionary<string, string>();
    private static readonly Dictionary<string, Texture2D> importedTextures = new Dictionary<string, Texture2D>();
    private static readonly Dictionary<string, Material> materialVariants = new Dictionary<string, Material>();

    [MenuItem("Tools/Quest Mobile/Build Three Texture Tiers")]
    public static void BuildThreeTiers()
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
            var root = FindInScene(scene, ModelRootName);
            if (root == null)
                throw new InvalidOperationException("Could not find Factory model root: " + ModelRootName);
            if (!AssetDatabase.IsValidFolder("Assets/Environments/Factory/GeneratedLOD"))
                throw new InvalidOperationException("Factory LOD assets must be generated first.");
            if (AssetDatabase.IsValidFolder(OutputPath))
                throw new InvalidOperationException("Texture tiers already exist; refusing to create duplicates.");

            EnsureFolder(OutputPath);
            EnsureFolder(TexturePath);
            EnsureFolder(MaterialPath);
            textureFiles.Clear();
            importedTextures.Clear();
            materialVariants.Clear();

            var groups = root.GetComponentsInChildren<LODGroup>(true);
            int affectedRenderers = 0;
            for (int i = 0; i < groups.Length; i++)
            {
                var lods = groups[i].GetLODs();
                if (lods.Length != 3)
                    continue;
                for (int level = 1; level <= 2; level++)
                {
                    var renderers = lods[level].renderers;
                    for (int r = 0; r < renderers.Length; r++)
                    {
                        var renderer = renderers[r] as MeshRenderer;
                        if (renderer == null)
                            continue;
                        var materials = renderer.sharedMaterials;
                        bool changed = false;
                        for (int m = 0; m < materials.Length; m++)
                        {
                            var variant = GetMaterialVariant(materials[m], level);
                            if (variant != null && variant != materials[m])
                            {
                                materials[m] = variant;
                                changed = true;
                            }
                        }
                        if (changed)
                        {
                            renderer.sharedMaterials = materials;
                            affectedRenderers++;
                        }
                    }
                }
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var pair in textureFiles)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(pair.Value);
                if (texture != null)
                    importedTextures[pair.Key] = texture;
            }
            AssignVariantTextures();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Factory texture tiers built. LOD0 keeps source textures, LOD1 uses half-resolution variants, LOD2 uses quarter-resolution variants. LODGroups: " +
                      groups.Length + ", renderers with variants: " + affectedRenderers +
                      ", generated textures: " + importedTextures.Count + ", generated materials: " + materialVariants.Count +
                      ". Original baked textures and UV coordinates are unchanged.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (opened && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static Material GetMaterialVariant(Material source, int tier)
    {
        if (source == null || source.shader == null)
            return null;
        string materialKey = source.GetInstanceID() + "_T" + tier;
        Material cached;
        if (materialVariants.TryGetValue(materialKey, out cached))
            return cached;

        var replacementTextures = new Dictionary<string, string>();
        int propertyCount = ShaderUtil.GetPropertyCount(source.shader);
        for (int p = 0; p < propertyCount; p++)
        {
            if (ShaderUtil.GetPropertyType(source.shader, p) != ShaderUtil.ShaderPropertyType.TexEnv)
                continue;
            string propertyName = ShaderUtil.GetPropertyName(source.shader, p);
            var sourceTexture = source.GetTexture(propertyName) as Texture2D;
            if (sourceTexture == null)
                continue;
            string textureKey = GetTextureKey(sourceTexture, tier);
            if (!textureFiles.ContainsKey(textureKey))
                CreateTextureVariant(sourceTexture, propertyName, tier, textureKey);
            replacementTextures[propertyName] = textureKey;
        }
        if (replacementTextures.Count == 0)
            return null;

        var variant = new Material(source);
        variant.name = source.name + "_TextureTier" + tier;
        var path = AssetDatabase.GenerateUniqueAssetPath(MaterialPath + "/" + SafeName(variant.name) + ".mat");
        AssetDatabase.CreateAsset(variant, path);
        materialVariants.Add(materialKey, variant);
        // Apply after the PNG assets have been imported.
        pendingMaterialTextures.Add(variant, replacementTextures);
        return variant;
    }

    private static readonly Dictionary<Material, Dictionary<string, string>> pendingMaterialTextures =
        new Dictionary<Material, Dictionary<string, string>>();

    private static void AssignVariantTextures()
    {
        foreach (var material in pendingMaterialTextures)
        {
            foreach (var texture in material.Value)
            {
                Texture2D replacement;
                if (importedTextures.TryGetValue(texture.Value, out replacement))
                    material.Key.SetTexture(texture.Key, replacement);
            }
            EditorUtility.SetDirty(material.Key);
        }
        pendingMaterialTextures.Clear();
    }

private static void CreateTextureVariant(Texture2D source, string propertyName, int tier, string key)
    {
        int width = Mathf.Max(1, source.width >> tier);
        int height = Mathf.Max(1, source.height >> tier);
        string fileName = SafeName(source.name) + "_Tier" + tier + "_" + Math.Abs(source.GetInstanceID()) + ".png";
        string assetPath = TexturePath + "/" + fileName;
        string absolutePath = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar));

        var sourceImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(source)) as TextureImporter;
        bool normalHint = propertyName.IndexOf("normal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          propertyName.IndexOf("bump", StringComparison.OrdinalIgnoreCase) >= 0;
        bool srgb = sourceImporter != null ? sourceImporter.sRGBTexture : !normalHint;
        var previous = RenderTexture.active;
        RenderTexture renderTarget = null;
        Texture2D resized = null;
        try
        {
            renderTarget = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32,
                srgb ? RenderTextureReadWrite.Default : RenderTextureReadWrite.Linear);
            Graphics.Blit(source, renderTarget);
            RenderTexture.active = renderTarget;
            resized = new Texture2D(width, height, TextureFormat.RGBA32, false, !srgb);
            resized.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            resized.Apply(false, false);
            File.WriteAllBytes(absolutePath, resized.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            if (renderTarget != null)
                RenderTexture.ReleaseTemporary(renderTarget);
            if (resized != null)
                UnityEngine.Object.DestroyImmediate(resized);
        }

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            if (sourceImporter != null)
            {
                importer.textureType = sourceImporter.textureType;
                importer.sRGBTexture = sourceImporter.sRGBTexture;
                importer.alphaSource = sourceImporter.alphaSource;
                importer.alphaIsTransparency = sourceImporter.alphaIsTransparency;
                importer.wrapModeU = sourceImporter.wrapModeU;
                importer.wrapModeV = sourceImporter.wrapModeV;
                importer.wrapModeW = sourceImporter.wrapModeW;
                importer.filterMode = sourceImporter.filterMode;
                importer.anisoLevel = sourceImporter.anisoLevel;
                importer.textureCompression = sourceImporter.textureCompression;
                importer.compressionQuality = sourceImporter.compressionQuality;
                importer.crunchedCompression = sourceImporter.crunchedCompression;
                importer.streamingMipmapsPriority = sourceImporter.streamingMipmapsPriority;
            }
            else
            {
                importer.textureType = normalHint ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = srgb;
            }
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            int size = Mathf.Max(width, height);
            importer.maxTextureSize = Mathf.Max(32, Mathf.NextPowerOfTwo(size));
            importer.SaveAndReimport();

            if (sourceImporter != null)
            {
                var android = sourceImporter.GetPlatformTextureSettings("Android");
                if (android.overridden)
                {
                    android.maxTextureSize = Mathf.Max(32, Mathf.Min(android.maxTextureSize, Mathf.NextPowerOfTwo(size)));
                    importer.SetPlatformTextureSettings(android);
                    importer.SaveAndReimport();
                }
            }
        }
        textureFiles.Add(key, assetPath);
    }

    private static string GetTextureKey(Texture2D source, int tier)
    {
        return source.GetInstanceID() + "_T" + tier;
    }

private static void ConfigureTextureImporter(string assetPath, TextureImporter sourceImporter, bool normalHint,
        bool srgb, int width, int height)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return;
        if (sourceImporter != null)
        {
            importer.textureType = sourceImporter.textureType;
            importer.sRGBTexture = sourceImporter.sRGBTexture;
            importer.alphaSource = sourceImporter.alphaSource;
            importer.alphaIsTransparency = sourceImporter.alphaIsTransparency;
            importer.wrapModeU = sourceImporter.wrapModeU;
            importer.wrapModeV = sourceImporter.wrapModeV;
            importer.wrapModeW = sourceImporter.wrapModeW;
            importer.filterMode = sourceImporter.filterMode;
            importer.anisoLevel = sourceImporter.anisoLevel;
            importer.textureCompression = sourceImporter.textureCompression;
            importer.compressionQuality = sourceImporter.compressionQuality;
            importer.crunchedCompression = sourceImporter.crunchedCompression;
            importer.streamingMipmapsPriority = sourceImporter.streamingMipmapsPriority;
        }
        else
        {
            importer.textureType = normalHint ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb;
        }
        importer.mipmapEnabled = true;
        importer.streamingMipmaps = true;
        int size = Mathf.Max(width, height);
        importer.maxTextureSize = Mathf.Max(32, Mathf.NextPowerOfTwo(size));
        importer.SaveAndReimport();
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

    private static string SafeName(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        return value.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
    }
}