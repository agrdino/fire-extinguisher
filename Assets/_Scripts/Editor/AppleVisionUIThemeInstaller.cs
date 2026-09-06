using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using _Scripts.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace _Scripts.EditorTools
{
    [InitializeOnLoad]
    internal static class AppleVisionUIThemeInstaller
    {
        private const int ThemeVersion = 3;
        private const string PackageRoot = "Packages/com.jetxr.visionui/Runtime";
        private const string ThemeFolder = "Assets/UIs/AppleVisionLight";
        private const string FontFolder = ThemeFolder + "/Fonts";
        private const string StatePath = ThemeFolder + "/Apple Vision Light Theme State.asset";
        private const string NotoFontPath = "Assets/Localization/Fonts/NotoSansJP Dynamic SDF.asset";

        private static readonly Color LabelPrimary = new(0.11f, 0.11f, 0.12f, 1f);
        private static readonly Color LabelSecondary = new(0.24f, 0.24f, 0.26f, 1f);
        private static readonly Color SystemBlue = new(0f, 0.478f, 1f, 1f);
        private static readonly Color NeutralSurface = new(1f, 1f, 1f, 0.78f);

        private sealed class ThemeResources
        {
            public TMP_FontAsset regular;
            public TMP_FontAsset medium;
            public TMP_FontAsset semiBold;
            public TMP_FontAsset bold;
            public Sprite buttonBackground;
            public Sprite buttonHighlight;
            public Sprite windowGlass;
            public Sprite scrollbarHandle;
            public Sprite sliderBackground;
            public Sprite sliderFill;
            public Material lightElementMaterial;
            public Material windowMaterial;
        }

        static AppleVisionUIThemeInstaller()
        {
            EditorApplication.delayCall += ApplyOnceWhenReady;
        }

        [MenuItem("Tools/UI/Apply Apple Vision Light Theme")]
        private static void ApplyFromMenu()
        {
            ApplyTheme(true);
        }

        private static void ApplyOnceWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += ApplyOnceWhenReady;
                return;
            }

            AppleVisionUIThemeState state =
                AssetDatabase.LoadAssetAtPath<AppleVisionUIThemeState>(StatePath);
            if (state != null && state.version >= ThemeVersion) return;
            ApplyTheme(false);
        }

        private static void ApplyTheme(bool force)
        {
            Sprite packageProbe = Load<Sprite>("Sprites/Buttons/RoundedRectBackground.png");
            if (packageProbe == null)
            {
                Debug.LogWarning("[Apple Vision UI] Package resources are not ready yet.");
                if (!force) EditorApplication.delayCall += ApplyOnceWhenReady;
                return;
            }

            try
            {
                EnsureFolder(ThemeFolder);
                EnsureFolder(FontFolder);
                ThemeResources resources = BuildResources(packageProbe);

                string[] prefabPaths = AssetDatabase.FindAssets(
                        "t:Prefab", new[] { "Assets/UIs", "Assets/Localization/Prefabs" })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => !path.Contains("/PressureGauge/", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();

                int themedCount = 0;
                foreach (string prefabPath in prefabPaths)
                {
                    GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                    try
                    {
                        StylePrefab(root, prefabPath, resources);
                        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                        themedCount++;
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }

                UpdateThemeState();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[Apple Vision UI] Applied Light theme v{ThemeVersion} to {themedCount} prefabs. " +
                          "Inter Dynamic covers English/Vietnamese; Noto Sans JP remains the Japanese fallback.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static ThemeResources BuildResources(Sprite packageProbe)
        {
            TMP_FontAsset noto = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NotoFontPath);
            return new ThemeResources
            {
                regular = EnsureDynamicFont("Inter-Regular", noto),
                medium = EnsureDynamicFont("Inter-Medium", noto),
                semiBold = EnsureDynamicFont("Inter-SemiBold", noto),
                bold = EnsureDynamicFont("Inter-Bold", noto),
                buttonBackground = packageProbe,
                buttonHighlight = Load<Sprite>("Sprites/Buttons/RoundedRectHighlight.png"),
                windowGlass = Load<Sprite>("Sprites/Windows/WindowGlassNoAlpha.png"),
                scrollbarHandle = Load<Sprite>("Sprites/Dropdown/ScrollbarHandle.png"),
                sliderBackground = Load<Sprite>("Sprites/Sliders/RegularBackground.png"),
                sliderFill = Load<Sprite>("Sprites/Sliders/RegularFill.png"),
                lightElementMaterial = Load<Material>("Materials/LightElementBackground.mat"),
                windowMaterial = Load<Material>("Materials/WindowBlurredBackground.mat")
            };
        }

        private static TMP_FontAsset EnsureDynamicFont(string fontName, TMP_FontAsset japaneseFallback)
        {
            string assetPath = $"{FontFolder}/{fontName} Dynamic SDF.asset";
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null)
            {
                ConfigureFallback(existing, japaneseFallback);
                return existing;
            }

            string sourcePath = $"{PackageRoot}/Fonts/Source/{fontName}.ttf";
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (sourceFont == null)
                throw new InvalidOperationException($"Unable to load {sourcePath}.");

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048,
                AtlasPopulationMode.Dynamic, true);
            fontAsset.name = $"{fontName} Dynamic SDF";
            AssetDatabase.CreateAsset(fontAsset, assetPath);

            if (fontAsset.atlasTextures != null)
            {
                foreach (Texture2D texture in fontAsset.atlasTextures)
                {
                    if (texture != null && !AssetDatabase.Contains(texture))
                        AssetDatabase.AddObjectToAsset(texture, fontAsset);
                }
            }

            if (fontAsset.material != null && !AssetDatabase.Contains(fontAsset.material))
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

            ConfigureFallback(fontAsset, japaneseFallback);
            return fontAsset;
        }

        private static void ConfigureFallback(TMP_FontAsset fontAsset, TMP_FontAsset fallback)
        {
            if (fontAsset == null || fallback == null) return;
            fontAsset.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
            if (!fontAsset.fallbackFontAssetTable.Contains(fallback))
                fontAsset.fallbackFontAssetTable.Add(fallback);
            EditorUtility.SetDirty(fontAsset);
        }

        private static void StylePrefab(GameObject root, string prefabPath, ThemeResources resources)
        {
            StyleWindowSurfaces(root, prefabPath, resources);
            StyleButtons(root, resources);
            StyleScrollViews(root, resources);
            StyleProgressBars(root, resources);
            StyleTypography(root, resources);
            StyleSelectionProperties(root);
            StyleHintPopup(root);
        }

        private static void StyleWindowSurfaces(
            GameObject root, string prefabPath, ThemeResources resources)
        {
            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                if (!IsWindowSurface(image, prefabPath)) continue;

                image.sprite = resources.windowGlass;
                image.type = Image.Type.Sliced;
                image.material = resources.windowMaterial;
                image.color = new Color(1f, 0.99f, 0.97f, 0.94f);
                image.raycastTarget = true;

                Shadow shadow = image.GetComponent<Shadow>() ?? image.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.18f);
                shadow.effectDistance = new Vector2(0f, -16f);
                shadow.useGraphicAlpha = true;
            }
        }

        private static bool IsWindowSurface(Image image, string prefabPath)
        {
            if (image.GetComponent<Button>() != null) return false;
            string name = image.name;
            if (prefabPath.EndsWith("/UI Panel.prefab", StringComparison.OrdinalIgnoreCase) &&
                name.Equals("Background", StringComparison.OrdinalIgnoreCase))
                return true;
            if (prefabPath.EndsWith("/UI Idle Hint Popup.prefab", StringComparison.OrdinalIgnoreCase) &&
                (name.Equals("Message", StringComparison.OrdinalIgnoreCase) ||
                 image.gameObject == image.transform.root.gameObject))
                return true;
            return false;
        }

        private static void StyleButtons(GameObject root, ThemeResources resources)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                Image image = button.image;
                if (image == null) continue;

                bool selectionButton = IsSelectionButton(button);
                bool primaryButton = !selectionButton && IsPrimaryButton(button.name);
                bool backButton = button.name.Contains("back", StringComparison.OrdinalIgnoreCase) ||
                                  button.transform.root.name.Contains("Button Back", StringComparison.OrdinalIgnoreCase);

                image.sprite = resources.buttonBackground;
                image.type = Image.Type.Sliced;
                image.material = primaryButton ? null : resources.lightElementMaterial;
                image.color = primaryButton ? SystemBlue : NeutralSurface;

                if (selectionButton)
                {
                    button.transition = Selectable.Transition.None;
                }
                else
                {
                    button.transition = Selectable.Transition.ColorTint;
                    ColorBlock colors = button.colors;
                    colors.normalColor = Color.white;
                    colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
                    colors.pressedColor = new Color(0.88f, 0.9f, 0.94f, 1f);
                    colors.selectedColor = Color.white;
                    colors.disabledColor = new Color(1f, 1f, 1f, 0.42f);
                    colors.colorMultiplier = 1f;
                    colors.fadeDuration = 0.08f;
                    button.colors = colors;
                }

                Image hover = EnsureHoverGraphic(button, resources.buttonHighlight);
                AppleVisionButtonFeedback feedback =
                    button.GetComponent<AppleVisionButtonFeedback>() ??
                    button.gameObject.AddComponent<AppleVisionButtonFeedback>();
                feedback.Configure(hover);

                Color labelColor = primaryButton ? Color.white :
                    backButton ? SystemBlue : LabelPrimary;
                foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
                    label.color = labelColor;
            }
        }

        private static Image EnsureHoverGraphic(Button button, Sprite highlightSprite)
        {
            Transform existing = button.transform.Find("Vision Hover");
            GameObject hoverObject;
            if (existing == null)
            {
                hoverObject = new GameObject(
                    "Vision Hover", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                hoverObject.transform.SetParent(button.transform, false);
                hoverObject.transform.SetAsFirstSibling();
            }
            else
            {
                hoverObject = existing.gameObject;
            }

            RectTransform rect = (RectTransform)hoverObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            Image hover = hoverObject.GetComponent<Image>();
            hover.sprite = highlightSprite;
            hover.type = Image.Type.Sliced;
            hover.color = new Color(1f, 1f, 1f, 0f);
            hover.raycastTarget = false;
            return hover;
        }

        private static void StyleScrollViews(GameObject root, ThemeResources resources)
        {
            foreach (ScrollRect scrollRect in root.GetComponentsInChildren<ScrollRect>(true))
            {
                scrollRect.movementType = ScrollRect.MovementType.Elastic;
                scrollRect.elasticity = 0.08f;
                scrollRect.inertia = true;
                scrollRect.decelerationRate = 0.135f;
                scrollRect.scrollSensitivity = 35f;
            }

            foreach (Scrollbar scrollbar in root.GetComponentsInChildren<Scrollbar>(true))
            {
                Image track = scrollbar.GetComponent<Image>();
                if (track != null)
                {
                    track.sprite = resources.buttonBackground;
                    track.type = Image.Type.Sliced;
                    track.material = resources.lightElementMaterial;
                    track.color = new Color(0.46f, 0.46f, 0.5f, 0.12f);
                }

                Image handle = scrollbar.handleRect != null
                    ? scrollbar.handleRect.GetComponent<Image>()
                    : null;
                if (handle != null)
                {
                    handle.sprite = resources.scrollbarHandle;
                    handle.type = Image.Type.Sliced;
                    handle.material = resources.lightElementMaterial;
                    handle.color = new Color(0.37f, 0.37f, 0.4f, 0.48f);
                }
            }
        }

        private static void StyleProgressBars(GameObject root, ThemeResources resources)
        {
            foreach (Slider slider in root.GetComponentsInChildren<Slider>(true))
            {
                slider.interactable = false;
                slider.transition = Selectable.Transition.None;
                slider.handleRect = null;

                Image background = slider.GetComponentsInChildren<Image>(true)
                    .FirstOrDefault(image => image.name.Equals(
                        "Background", StringComparison.OrdinalIgnoreCase));
                if (background != null)
                {
                    background.sprite = resources.sliderBackground;
                    background.type = Image.Type.Sliced;
                    background.material = resources.lightElementMaterial;
                    background.color = new Color(0.46f, 0.46f, 0.5f, 0.18f);
                }

                if (slider.fillRect != null)
                {
                    Image fill = slider.fillRect.GetComponent<Image>();
                    if (fill != null)
                    {
                        fill.sprite = resources.sliderFill;
                        fill.type = Image.Type.Sliced;
                        fill.material = null;
                        fill.color = SystemBlue;
                    }
                }
            }
        }

        private static void StyleTypography(GameObject root, ThemeResources resources)
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                bool inButton = text.GetComponentInParent<Button>() != null;
                bool title = text.name.Contains("title", StringComparison.OrdinalIgnoreCase) ||
                             text.fontSize >= 42f;
                bool subtitle = !title && text.fontSize >= 32f;

                text.font = title ? resources.bold :
                    inButton || subtitle ? resources.semiBold : resources.regular;
                text.textWrappingMode = TextWrappingModes.Normal;

                if (!inButton)
                {
                    bool secondary = text.name.Contains("guide", StringComparison.OrdinalIgnoreCase) ||
                                     text.name.Contains("description", StringComparison.OrdinalIgnoreCase) ||
                                     text.name.Contains("cooldown", StringComparison.OrdinalIgnoreCase);
                    text.color = secondary ? LabelSecondary : LabelPrimary;
                }
            }
        }

        private static void StyleSelectionProperties(GameObject root)
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null) continue;
                SerializedObject serialized = new(behaviour);
                bool changed = SetColor(serialized, "_selectedColor", SystemBlue);
                changed |= SetColor(serialized, "_unselectedColor", NeutralSurface);
                changed |= SetColor(serialized, "_selectedTextColor", Color.white);
                changed |= SetColor(serialized, "_unselectedTextColor", LabelPrimary);
                if (changed) serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static bool SetColor(SerializedObject serialized, string propertyName, Color color)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Color)
                return false;
            property.colorValue = color;
            return true;
        }

        private static void StyleHintPopup(GameObject root)
        {
            if (!root.name.Contains("Idle Hint", StringComparison.OrdinalIgnoreCase)) return;
            Image accent = root.GetComponentsInChildren<Image>(true)
                .FirstOrDefault(image => image.name.Equals("Accent", StringComparison.OrdinalIgnoreCase));
            if (accent != null) accent.color = new Color(1f, 0.584f, 0f, 1f);
        }

        private static bool IsSelectionButton(Button button)
        {
            for (Transform current = button.transform; current != null; current = current.parent)
            {
                string name = current.name;
                if (name.Contains("Options", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Environments", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Languages", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Choice Item", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Language Button", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool IsPrimaryButton(string name)
        {
            string normalized = name.ToLowerInvariant();
            return normalized.Contains("start") || normalized.Contains("confirm") ||
                   normalized.Contains("continue") || normalized.Contains("retry") ||
                   normalized.Contains("restart") || normalized.Contains("select");
        }

        private static T Load<T>(string relativePath) where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>($"{PackageRoot}/{relativePath}");
        }

        private static void UpdateThemeState()
        {
            AppleVisionUIThemeState state =
                AssetDatabase.LoadAssetAtPath<AppleVisionUIThemeState>(StatePath);
            if (state == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(StatePath) != null)
                    AssetDatabase.DeleteAsset(StatePath);
                state = ScriptableObject.CreateInstance<AppleVisionUIThemeState>();
                state.name = "Apple Vision Light Theme State";
                AssetDatabase.CreateAsset(state, StatePath);
            }
            state.version = ThemeVersion;
            EditorUtility.SetDirty(state);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
