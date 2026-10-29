using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using _Scripts.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FireExtinguisher.EditorTools
{
    /// <summary>
    /// Rebuilds the project UI around one editable button prefab and prefab variants.
    /// The legacy assets are moved instead of deleted so pre-refactor artwork remains recoverable.
    /// </summary>
    public static class ReachUIDesignSystemBuilder
    {
        private const string MenuPath = "Tools/Fire Extinguisher/Rebuild Reach UI Design System";
        private const string ButtonsFolder = "Assets/UIs/Buttons";
        private const string LegacyFolder = "Assets/UIs/Legacy";

        private const string BasePath = ButtonsFolder + "/UI Button Base.prefab";
        private const string ActionMediumPath = ButtonsFolder + "/UI Button Action Medium.prefab";
        private const string ActionLargePath = ButtonsFolder + "/UI Button Action Large.prefab";
        private const string BackPath = "Assets/UIs/UI Button Back.prefab";
        private const string ChoicePath = "Assets/UIs/UI Choice Item.prefab";
        private const string ChoiceEnvironmentPath = "Assets/UIs/UI Choice Environment Item.prefab";
        private const string ChoiceExtinguisherPath = "Assets/UIs/UI Choice Fire Extinguisher Item.prefab";
        private const string LanguagePath = "Assets/UIs/UI Language Button.prefab";

        private const string LegacyBackPath = LegacyFolder + "/UI Button Back Legacy.prefab";
        private const string LegacyChoicePath = LegacyFolder + "/UI Choice Item Legacy.prefab";
        private const string LegacyChoiceEnvironmentPath = LegacyFolder + "/UI Choice Environment Item Legacy.prefab";
        private const string LegacyChoiceExtinguisherPath = LegacyFolder + "/UI Choice Fire Extinguisher Item Legacy.prefab";
        private const string LegacyLanguagePath = LegacyFolder + "/UI Language Button Legacy.prefab";

        private const string FrameSpritePath =
            "Assets/ThirdParties/Michsky/Reach - Complete Sci-Fi UI/Textures/Borders/Special/Cross Frame.png";
        private const string GlowSpritePath =
            "Assets/ThirdParties/Michsky/Reach - Complete Sci-Fi UI/Textures/Shadows/Radial/Radial Directional Shadow Filled.png";
        private const string RegularFontPath = "Assets/Localization/Fonts/NotoSans-SemiBold Dynamic SDF.asset";
        private const string BoldFontPath = "Assets/Localization/Fonts/NotoSans-Bold Dynamic SDF.asset";

        private static readonly Color Aqua = new Color(0f, 0.7843137f, 1f, 1f);
        private static readonly Color Surface = new Color(0.09803922f, 0.13725491f, 0.1764706f, 0.9f);

        private static readonly string[] ParentPrefabPaths =
        {
            "Assets/UIs/Variants/Completed Scene.prefab",
            "Assets/UIs/Variants/Escaped Scene.prefab",
            "Assets/UIs/Variants/Explore Scene.prefab",
            "Assets/UIs/Variants/Failed Scene.prefab",
            "Assets/UIs/Variants/Select Environment Scene.prefab",
            "Assets/UIs/Variants/Select Extinguisher Scene.prefab",
            "Assets/UIs/Variants/Start Scene.prefab",
            "Assets/Localization/Prefabs/Select Language Scene.prefab"
        };

        [MenuItem(MenuPath)]
        public static void Rebuild()
        {
            EnsureFolder(ButtonsFolder);
            EnsureFolder(LegacyFolder);

            ArchiveLegacyAssets();
            BuildBaseButton();
            BuildActionVariant(ActionMediumPath, "UI Button Action Medium", new Vector2(450f, 100f), 30f);
            BuildActionVariant(ActionLargePath, "UI Button Action Large", new Vector2(600f, 100f), 32f);
            BuildBackVariant();
            BuildLanguageVariant();
            BuildChoiceVariant();
            BuildSpecializedChoiceVariant(
                ChoiceEnvironmentPath,
                LegacyChoiceEnvironmentPath,
                "UI Choice Environment Item");
            BuildSpecializedChoiceVariant(
                ChoiceExtinguisherPath,
                LegacyChoiceExtinguisherPath,
                "UI Choice Fire Extinguisher Item");

            foreach (string path in ParentPrefabPaths)
                MigrateParentPrefab(path);

            NormalizeStaticUi();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Reach UI design system rebuilt: shared base + variants, with single-state button artwork.");
        }

        private static void ArchiveLegacyAssets()
        {
            ArchiveIfRegular(ChoiceEnvironmentPath, LegacyChoiceEnvironmentPath);
            ArchiveIfRegular(ChoiceExtinguisherPath, LegacyChoiceExtinguisherPath);
            ArchiveIfRegular(ChoicePath, LegacyChoicePath);
            ArchiveIfRegular(LanguagePath, LegacyLanguagePath);
            ArchiveIfRegular(BackPath, LegacyBackPath);
        }

        private static void ArchiveIfRegular(string sourcePath, string legacyPath)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null || IsBasedOnSharedButton(source)) return;

            if (AssetDatabase.LoadAssetAtPath<GameObject>(legacyPath) != null)
                return;

            string error = AssetDatabase.MoveAsset(sourcePath, legacyPath);
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException("Could not archive " + sourcePath + ": " + error);
        }

        private static bool IsBasedOnSharedButton(GameObject prefab)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(prefab);
            while (source != null)
            {
                if (AssetDatabase.GetAssetPath(source) == BasePath) return true;
                source = PrefabUtility.GetCorrespondingObjectFromSource(source);
            }
            return false;
        }

        private static void BuildBaseButton()
        {
            GameObject root = CreateUiObject("UI Button Base", null);
            RectTransform rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(450f, 100f);

            Image hitArea = root.AddComponent<Image>();
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;

            Button button = root.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hitArea;

            Image background = CreateImage("Background", root.transform, null, Surface);
            Image glow = CreateImage(
                "Glow",
                root.transform,
                AssetDatabase.LoadAssetAtPath<Sprite>(GlowSpritePath),
                new Color(Aqua.r, Aqua.g, Aqua.b, 0f));
            glow.enabled = false;
            Image frame = CreateImage(
                "Frame",
                root.transform,
                AssetDatabase.LoadAssetAtPath<Sprite>(FrameSpritePath),
                Aqua);
            frame.pixelsPerUnitMultiplier = 4f;

            TextMeshProUGUI text = CreateText("Text", root.transform, "BUTTON", 30f);
            text.color = Color.white;

            UIButtonTween tween = root.AddComponent<UIButtonTween>();
            tween.ConfigureVisuals(background, frame, glow, new TMP_Text[] { text });

            SaveRegularPrefab(root, BasePath);
        }

        private static void BuildActionVariant(string path, string name, Vector2 size, float fontSize)
        {
            GameObject instance = InstantiateVariant(BasePath);
            instance.name = name;
            ((RectTransform)instance.transform).sizeDelta = size;

            TMP_Text text = instance.GetComponentInChildren<TMP_Text>(true);
            text.text = "ACTION";
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontPath);
            text.fontSize = fontSize;
            text.color = Color.white;

            ConfigureTween(instance);
            SaveVariant(instance, path);
        }

        private static void BuildBackVariant()
        {
            GameObject instance = InstantiateVariant(BasePath);
            instance.name = "UI Button Back";

            TMP_Text text = instance.GetComponentInChildren<TMP_Text>(true);
            text.text = "Back";
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RegularFontPath);
            text.fontSize = 18f;
            text.color = Color.white;

            if (AssetDatabase.LoadAssetAtPath<GameObject>(LegacyBackPath) != null)
                CopyResolvedPrefabValues(LegacyBackPath, instance);

            // Start Scene is the canonical Back-button sample.
            ((RectTransform)instance.transform).sizeDelta = new Vector2(250f, 50f);
            ConfigureTween(instance);
            SaveVariant(instance, BackPath);
        }

        private static void BuildLanguageVariant()
        {
            GameObject instance = InstantiateVariant(BasePath);
            instance.name = "UI Language Button";
            ((RectTransform)instance.transform).sizeDelta = new Vector2(360f, 110f);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(LegacyLanguagePath) != null)
                CopyResolvedPrefabValues(LegacyLanguagePath, instance);

            TMP_Text text = instance.GetComponentInChildren<TMP_Text>(true);
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontPath);
            text.color = Color.white;
            ConfigureTween(instance);
            SaveVariant(instance, LanguagePath);
        }

        private static void BuildChoiceVariant()
        {
            GameObject instance = InstantiateVariant(BasePath);
            instance.name = "UI Choice Item";

            Transform inheritedText = instance.transform.Find("Text");
            if (inheritedText != null) inheritedText.gameObject.SetActive(false);

            GameObject legacyRoot = AssetDatabase.LoadAssetAtPath<GameObject>(LegacyChoicePath);
            if (legacyRoot != null)
            {
                GameObject loaded = PrefabUtility.LoadPrefabContents(LegacyChoicePath);
                try
                {
                    CopyRect((RectTransform)loaded.transform, (RectTransform)instance.transform);
                    for (int i = 0; i < loaded.transform.childCount; i++)
                    {
                        Transform child = loaded.transform.GetChild(i);
                        if (child.name == "Reach States") continue;
                        GameObject clone = Object.Instantiate(child.gameObject, instance.transform);
                        clone.name = child.name;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(loaded);
                }
            }

            foreach (TMP_Text text in instance.GetComponentsInChildren<TMP_Text>(true))
                text.color = Color.white;

            ConfigureTween(instance);
            SaveVariant(instance, ChoicePath);
        }

        private static void BuildSpecializedChoiceVariant(string path, string legacyPath, string name)
        {
            GameObject instance = InstantiateVariant(ChoicePath);
            instance.name = name;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(legacyPath) != null)
                CopyResolvedPrefabValues(legacyPath, instance);

            foreach (TMP_Text text in instance.GetComponentsInChildren<TMP_Text>(true))
                text.color = Color.white;

            ConfigureTween(instance);
            SaveVariant(instance, path);
        }

        private static void CopyResolvedPrefabValues(string sourcePath, GameObject destination)
        {
            GameObject source = PrefabUtility.LoadPrefabContents(sourcePath);
            try
            {
                CopyHierarchyValues(source, destination, null);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(source);
            }
        }

        private static void MigrateParentPrefab(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Button[] buttons = root.GetComponentsInChildren<Button>(true);
                foreach (Button oldButton in buttons)
                {
                    string sourcePath = ResolveSharedPrefab(oldButton);
                    if (string.IsNullOrEmpty(sourcePath)) continue;
                    GameObject currentSource =
                        PrefabUtility.GetCorrespondingObjectFromSource(oldButton.gameObject);
                    if (currentSource != null
                        && AssetDatabase.GetAssetPath(currentSource) == sourcePath)
                        continue;
                    ReplaceButton(root, oldButton, sourcePath);
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string ResolveSharedPrefab(Button button)
        {
            string name = button.name;
            if (name == "btnBack" || name == "UI Button Back") return BackPath;
            if (name.StartsWith("UI Choice Environment Item", StringComparison.Ordinal))
                return ChoiceEnvironmentPath;
            if (name.StartsWith("UI Choice Fire Extinguisher Item", StringComparison.Ordinal))
                return ChoiceExtinguisherPath;
            if (name == "UI Vietnamese" || name == "UI English" || name == "UI Japanese")
                return LanguagePath;

            RectTransform rect = button.transform as RectTransform;
            return rect != null && Mathf.Abs(rect.rect.width) >= 500f
                ? ActionLargePath
                : ActionMediumPath;
        }

        private static void ReplaceButton(GameObject prefabRoot, Button oldButton, string sourcePath)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null) throw new InvalidOperationException("Missing shared button prefab: " + sourcePath);

            Transform oldTransform = oldButton.transform;
            Transform parent = oldTransform.parent;
            int siblingIndex = oldTransform.GetSiblingIndex();
            GameObject replacement = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            replacement.name = oldButton.name;
            replacement.SetActive(oldButton.gameObject.activeSelf);
            replacement.transform.SetSiblingIndex(siblingIndex);
            CopyRect((RectTransform)oldTransform, (RectTransform)replacement.transform);

            Dictionary<Object, Object> mapping = new Dictionary<Object, Object>();
            CopyHierarchyValues(oldButton.gameObject, replacement, mapping);
            RemapObjectReferences(replacement, mapping, null);
            RemapObjectReferences(prefabRoot, mapping, oldTransform);

            foreach (TMP_Text text in replacement.GetComponentsInChildren<TMP_Text>(true))
                if (text.gameObject.activeSelf)
                    text.color = Color.white;

            ConfigureTween(replacement);
            Object.DestroyImmediate(oldButton.gameObject);
        }

        private static void CopyHierarchyValues(
            GameObject sourceRoot,
            GameObject destinationRoot,
            Dictionary<Object, Object> externalMapping)
        {
            Dictionary<Object, Object> mapping = externalMapping ?? new Dictionary<Object, Object>();
            MapObjects(sourceRoot, destinationRoot, mapping);
            CopyRect(sourceRoot.transform as RectTransform, destinationRoot.transform as RectTransform);

            Transform[] sourceTransforms = sourceRoot.GetComponentsInChildren<Transform>(true);
            foreach (Transform sourceTransform in sourceTransforms)
            {
                if (sourceTransform == sourceRoot.transform || IsUnderReachStates(sourceTransform, sourceRoot.transform))
                    continue;

                string relativePath = GetRelativePath(sourceRoot.transform, sourceTransform);
                Transform destinationTransform = destinationRoot.transform.Find(relativePath);
                if (destinationTransform == null && sourceTransform.GetComponent<TMP_Text>() != null)
                    destinationTransform = destinationRoot.GetComponentsInChildren<TMP_Text>(true)
                        .Select(t => t.transform)
                        .FirstOrDefault(t => t.gameObject.activeSelf);
                if (destinationTransform == null) continue;

                MapObjects(sourceTransform.gameObject, destinationTransform.gameObject, mapping);
                CopyRect(sourceTransform as RectTransform, destinationTransform as RectTransform);
            }

            foreach (KeyValuePair<Object, Object> pair in mapping.ToArray())
            {
                Component sourceComponent = pair.Key as Component;
                Component destinationComponent = pair.Value as Component;
                if (sourceComponent == null || destinationComponent == null) continue;
                if (sourceComponent is Transform || sourceComponent is CanvasRenderer || sourceComponent is UIButtonTween)
                    continue;
                EditorUtility.CopySerialized(sourceComponent, destinationComponent);
            }

            RemapObjectReferences(destinationRoot, mapping, null);
        }

        private static void MapObjects(
            GameObject source,
            GameObject destination,
            Dictionary<Object, Object> mapping)
        {
            mapping[source] = destination;
            mapping[source.transform] = destination.transform;

            Component[] sourceComponents = source.GetComponents<Component>();
            foreach (Component sourceComponent in sourceComponents)
            {
                if (sourceComponent == null || sourceComponent is Transform || sourceComponent is CanvasRenderer)
                    continue;

                Type type = sourceComponent.GetType();
                if (type == typeof(UIButtonTween))
                {
                    UIButtonTween destinationTween = destination.GetComponent<UIButtonTween>();
                    if (destinationTween != null) mapping[sourceComponent] = destinationTween;
                    continue;
                }

                Component destinationComponent = destination.GetComponent(type);
                if (destinationComponent == null)
                {
                    try
                    {
                        destinationComponent = destination.AddComponent(type);
                    }
                    catch
                    {
                        continue;
                    }
                }

                mapping[sourceComponent] = destinationComponent;
            }
        }

        private static void RemapObjectReferences(
            GameObject root,
            IReadOnlyDictionary<Object, Object> mapping,
            Transform excludedSubtree)
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                if (component is Transform || component is CanvasRenderer) continue;
                if (excludedSubtree != null
                    && (component.transform == excludedSubtree
                        || component.transform.IsChildOf(excludedSubtree)))
                    continue;

                SerializedObject serialized = new SerializedObject(component);
                SerializedProperty property = serialized.GetIterator();
                bool enterChildren = true;
                bool changed = false;
                while (property.Next(enterChildren))
                {
                    enterChildren = true;
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (property.propertyPath == "m_GameObject"
                        || property.propertyPath == "m_Script"
                        || property.propertyPath == "m_CorrespondingSourceObject"
                        || property.propertyPath == "m_PrefabInstance"
                        || property.propertyPath == "m_PrefabAsset")
                        continue;
                    Object current = property.objectReferenceValue;
                    Object replacement;
                    if (current != null && mapping.TryGetValue(current, out replacement))
                    {
                        property.objectReferenceValue = replacement;
                        changed = true;
                    }
                }

                if (changed) serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static bool IsUnderReachStates(Transform child, Transform root)
        {
            Transform current = child;
            while (current != null && current != root)
            {
                if (current.name == "Reach States") return true;
                current = current.parent;
            }
            return false;
        }

        private static string GetRelativePath(Transform root, Transform child)
        {
            List<string> parts = new List<string>();
            Transform current = child;
            while (current != null && current != root)
            {
                parts.Add(current.name);
                current = current.parent;
            }
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        private static void ConfigureTween(GameObject root)
        {
            Button button = root.GetComponent<Button>();
            Image hitArea = root.GetComponent<Image>();
            if (button != null)
            {
                button.transition = Selectable.Transition.None;
                button.targetGraphic = hitArea;
            }

            Image background = FindDirectImage(root.transform, "Background");
            Image frame = FindDirectImage(root.transform, "Frame");
            Image glow = FindDirectImage(root.transform, "Glow");
            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true)
                .Where(t => t.gameObject.activeSelf)
                .ToArray();

            UIButtonTween tween = root.GetComponent<UIButtonTween>();
            if (tween == null) tween = root.AddComponent<UIButtonTween>();
            tween.ConfigureVisuals(background, frame, glow, texts);
        }

        private static Image FindDirectImage(Transform root, string childName)
        {
            Transform child = root.Find(childName);
            return child != null ? child.GetComponent<Image>() : null;
        }

        private static void NormalizeStaticUi()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { "Assets/UIs", "Assets/Localization/Prefabs" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith(LegacyFolder, StringComparison.Ordinal))
                    continue;

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                bool changed = false;
                try
                {
                    foreach (Image image in root.GetComponentsInChildren<Image>(true))
                    {
                        if (image.GetComponentInParent<Button>() != null || image.name != "Frame") continue;
                        Color color = image.color;
                        Color normalized = new Color(Aqua.r, Aqua.g, Aqua.b, color.a);
                        if (image.color != normalized)
                        {
                            image.color = normalized;
                            changed = true;
                        }
                        if (!Mathf.Approximately(image.pixelsPerUnitMultiplier, 4f))
                        {
                            image.pixelsPerUnitMultiplier = 4f;
                            changed = true;
                        }
                    }

                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static GameObject InstantiateVariant(string sourcePath)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null) throw new InvalidOperationException("Missing prefab source: " + sourcePath);
            return (GameObject)PrefabUtility.InstantiatePrefab(source);
        }

        private static void SaveRegularPrefab(GameObject instance, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
        }

        private static void SaveVariant(GameObject instance, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            if (parent != null) gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static Image CreateImage(
            string name,
            Transform parent,
            Sprite sprite,
            Color color)
        {
            GameObject gameObject = CreateUiObject(name, parent);
            Stretch((RectTransform)gameObject.transform);
            Image image = gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            string value,
            float fontSize)
        {
            GameObject gameObject = CreateUiObject(name, parent);
            Stretch((RectTransform)gameObject.transform);
            TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RegularFontPath);
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void CopyRect(RectTransform source, RectTransform destination)
        {
            if (source == null || destination == null) return;
            destination.anchorMin = source.anchorMin;
            destination.anchorMax = source.anchorMax;
            destination.anchoredPosition3D = source.anchoredPosition3D;
            destination.sizeDelta = source.sizeDelta;
            destination.pivot = source.pivot;
            destination.localRotation = source.localRotation;
            destination.localScale = source.localScale;
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
