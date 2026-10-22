using System;
using System.Linq;
using _Scripts.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.UI;

namespace FireExtinguisher.EditorTools
{
    internal static class GuideControllerTutorialPrefabBuilder
    {
        private const string TutorialPath = "Assets/UIs/Guide Controller Tutorial.prefab";
        private const string TrackedVisualPath = "Assets/UIs/Guide Controller Tracked Visual.prefab";
        private const string GuidePath = "Assets/UIs/Variants/Guide Scene.prefab";
        private const string ControllerPath = "Assets/Samples/XR Interaction Toolkit/3.3.2/Starter Assets/Prefabs/Controllers/XR Controller Left.prefab";
        private const string FontPath = "Assets/Localization/Fonts/NotoSans Dynamic SDF.asset";
        private const string BoldFontPath = "Assets/Localization/Fonts/NotoSans-SemiBold Dynamic SDF.asset";
        private const string MaterialFolder = "Assets/UIs/Materials";
        private const string HighlightMaterialPath = MaterialFolder + "/Guide Controller Highlight.mat";
        private const string GhostMaterialPath = MaterialFolder + "/Guide Controller Ghost.mat";

        [MenuItem("Tools/Fire Extinguisher/Rebuild Guide Controller Tutorial")]
        private static void BuildFromMenu()
        {
            Build();
            Debug.Log("[Guide Tutorial] Controller tutorial prefab and Guide Scene were rebuilt.");
        }

        internal static void Build()
        {
            EnsureFolder(MaterialFolder);
            Material highlightMaterial = EnsureHighlightMaterial();
            Material ghostMaterial = EnsureGhostMaterial();
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            TMP_FontAsset boldFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontPath);
            GameObject controllerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ControllerPath);
            if (controllerPrefab == null)
                throw new InvalidOperationException("XR Controller Left prefab was not found.");

            GameObject tutorialRoot = BuildTutorialPrefab(
                controllerPrefab,
                font,
                boldFont,
                highlightMaterial);
            PrefabUtility.SaveAsPrefabAsset(tutorialRoot, TutorialPath);
            UnityEngine.Object.DestroyImmediate(tutorialRoot);

            GameObject trackedVisualRoot = BuildTrackedVisualPrefab(controllerPrefab, ghostMaterial, highlightMaterial);
            PrefabUtility.SaveAsPrefabAsset(trackedVisualRoot, TrackedVisualPath);
            UnityEngine.Object.DestroyImmediate(trackedVisualRoot);

            EnsureTutorialTranslations();
            InstallIntoGuidePrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static GameObject BuildTutorialPrefab(
            GameObject controllerPrefab,
            TMP_FontAsset font,
            TMP_FontAsset boldFont,
            Material highlightMaterial)
        {
            GameObject root = new("Guide Controller Tutorial", typeof(RectTransform), typeof(GuideControllerTutorialView));
            Stretch((RectTransform)root.transform);

            GameObject visualArea = CreateRect(
                "Controller Visual",
                root.transform,
                Vector2.zero,
                new Vector2(0.58f, 0.58f));
            GameObject controller = (GameObject)PrefabUtility.InstantiatePrefab(controllerPrefab);
            controller.name = "XR Controller Visual";
            controller.transform.SetParent(visualArea.transform, false);
            controller.transform.localPosition = new Vector3(-15f, 30f, -80f);
            controller.transform.localRotation = Quaternion.Euler(22f, 150f, 18f);
            controller.transform.localScale = Vector3.one * 3800f;

            Transform trigger = Find(controller.transform, "Trigger");
            Transform stick = Find(controller.transform, "ThumbStick");
            Transform buttonX = Find(controller.transform, "Button_A");
            Transform buttonY = Find(controller.transform, "Button_B");
            CreateWorldLabel("X Label", buttonX, "X", font);
            CreateWorldLabel("Y Label", buttonY, "Y", font);

            GameObject callout = CreateRect(
                "Callout",
                root.transform,
                new Vector2(0.57f, 0.03f),
                new Vector2(0.98f, 0.45f));
            Image background = callout.AddComponent<Image>();
            background.color = new Color(0.035f, 0.075f, 0.105f, 0.96f);
            background.raycastTarget = false;
            Outline outline = callout.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0.7843137f, 1f, 0.85f);
            outline.effectDistance = new Vector2(3f, -3f);
            CanvasGroup calloutGroup = callout.AddComponent<CanvasGroup>();

            TMP_Text stepText = CreateText(
                "Step", callout.transform, "1 / 3", boldFont, 36f,
                new Color(0f, 0.7843137f, 1f, 1f),
                new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.95f),
                TextAlignmentOptions.TopLeft);
            TMP_Text instruction = CreateText(
                "Instruction", callout.transform, "Use the trigger to interact with UI.", boldFont, 52f,
                Color.white,
                new Vector2(0.08f, 0.30f), new Vector2(0.92f, 0.78f),
                TextAlignmentOptions.MidlineLeft);
            instruction.enableAutoSizing = true;
            instruction.fontSizeMin = 32f;
            instruction.fontSizeMax = 52f;
            TMP_Text advance = CreateText(
                "Advance Hint", callout.transform, "Please wait...", font, 30f,
                new Color(0.72f, 0.92f, 1f, 1f),
                new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.30f),
                TextAlignmentOptions.BottomLeft);

            SerializedObject serializedView = new(root.GetComponent<GuideControllerTutorialView>());
            SetReference(serializedView, "_controllerVisual", controller.transform);
            SetReference(serializedView, "_triggerTarget", trigger);
            SetReference(serializedView, "_stickTarget", stick);
            SetReference(serializedView, "_xButtonTarget", buttonX);
            SetReference(serializedView, "_yButtonTarget", buttonY);
            SetReference(serializedView, "_calloutCanvasGroup", calloutGroup);
            SetReference(serializedView, "_instructionText", instruction);
            SetReference(serializedView, "_stepText", stepText);
            SetReference(serializedView, "_advanceHintText", advance);
            SetReference(serializedView, "_highlightMaterial", highlightMaterial);
            serializedView.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static GameObject BuildTrackedVisualPrefab(
            GameObject controllerPrefab,
            Material ghostMaterial,
            Material highlightMaterial)
        {
            GameObject root = new("Guide Controller Tracked Visual", typeof(GuideTrackedControllerVisual));
            GameObject controller = (GameObject)PrefabUtility.InstantiatePrefab(controllerPrefab);
            controller.name = "XR Controller Left Ghost";
            controller.transform.SetParent(root.transform, false);
            controller.transform.localPosition = Vector3.zero;
            // The XR controller mesh prefab faces local -Z. Restore its authored
            // 180-degree Y rotation so it follows the tracked controller's +Z forward.
            controller.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            controller.transform.localScale = Vector3.one * 1.08f;

            foreach (Renderer targetRenderer in controller.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = targetRenderer.sharedMaterials;
                if (materials.Length == 0) materials = new Material[1];
                for (int index = 0; index < materials.Length; index++) materials[index] = ghostMaterial;
                targetRenderer.sharedMaterials = materials;
            }

            SerializedObject serializedView = new(root.GetComponent<GuideTrackedControllerVisual>());
            SetReference(serializedView, "_triggerTarget", Find(controller.transform, "Trigger"));
            SetReference(serializedView, "_stickTarget", Find(controller.transform, "ThumbStick"));
            SetReference(serializedView, "_xButtonTarget", Find(controller.transform, "Button_A"));
            SetReference(serializedView, "_yButtonTarget", Find(controller.transform, "Button_B"));
            SetReference(serializedView, "_highlightMaterial", highlightMaterial);
            serializedView.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static void InstallIntoGuidePrefab()
        {
            GameObject guideRoot = PrefabUtility.LoadPrefabContents(GuidePath);
            try
            {
                GuideScene guideScene = guideRoot.GetComponentInChildren<GuideScene>(true);
                if (guideScene == null)
                    throw new InvalidOperationException("GuideScene component was not found.");

                Transform titleRoot = guideRoot.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(transform => transform.name == "UI Guide Title");
                if (titleRoot == null)
                    throw new InvalidOperationException("UI Guide Title was not found.");

                Transform oldScroll = titleRoot.Find("Scroll View");
                if (oldScroll != null) oldScroll.gameObject.SetActive(false);

                Transform oldTutorial = titleRoot.Find("Guide Controller Tutorial");
                if (oldTutorial != null)
                    UnityEngine.Object.DestroyImmediate(oldTutorial.gameObject);

                GameObject tutorialAsset = AssetDatabase.LoadAssetAtPath<GameObject>(TutorialPath);
                GameObject tutorial = (GameObject)PrefabUtility.InstantiatePrefab(tutorialAsset, titleRoot);
                tutorial.name = "Guide Controller Tutorial";
                RectTransform rect = (RectTransform)tutorial.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.anchoredPosition = new Vector2(0f, -95f);
                rect.sizeDelta = new Vector2(-100f, -190f);
                rect.localScale = Vector3.one;

                SerializedObject serializedScene = new(guideScene);
                SetReference(serializedScene, "_controllerTutorial", tutorial.GetComponent<GuideControllerTutorialView>());
                GameObject trackedVisualAsset = AssetDatabase.LoadAssetAtPath<GameObject>(TrackedVisualPath);
                SetReference(serializedScene, "_trackedControllerVisualPrefab",
                    trackedVisualAsset.GetComponent<GuideTrackedControllerVisual>());
                serializedScene.FindProperty("_stepAdvanceDelay").floatValue = 2f;
                serializedScene.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(guideRoot, GuidePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(guideRoot);
            }
        }

        private static Material EnsureHighlightMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(HighlightMaterialPath);
            if (material != null) return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            material = new Material(shader) { name = "Guide Controller Highlight" };
            Color highlight = new(0f, 0.7843137f, 1f, 1f);
            material.color = highlight;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", highlight);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(0f, 1.2f, 2.5f, 1f));
            }

            AssetDatabase.CreateAsset(material, HighlightMaterialPath);
            return material;
        }

        private static Material EnsureGhostMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(GhostMaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                material = new Material(shader) { name = "Guide Controller Ghost" };
                AssetDatabase.CreateAsset(material, GhostMaterialPath);
            }

            Color ghost = new(0f, 0.62f, 0.82f, 0.24f);
            material.color = ghost;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", ghost);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", 5f);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", 10f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureTutorialTranslations()
        {
            StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection("UI");
            if (collection == null)
                throw new InvalidOperationException("The UI localization table collection was not found.");

            foreach (UnityEngine.Localization.Locale locale in LocalizationEditorSettings.GetLocales())
            {
                StringTable table = collection.GetTable(locale.Identifier) as StringTable;
                if (table == null) continue;

                bool isVietnamese = locale.Identifier.Code == "vi";
                bool isJapanese = locale.Identifier.Code == "ja";
                AddEntry(table, "guide.controller.interact",
                    isVietnamese ? "Dùng cò trỏ để tương tác với UI." :
                    isJapanese ? "トリガーでUIを操作します。" :
                    "Use the trigger to interact with UI.");
                AddEntry(table, "guide.controller.move",
                    isVietnamese ? "Dùng cần điều khiển để di chuyển." :
                    isJapanese ? "スティックで移動します。" :
                    "Use the thumbstick to move.");
                AddEntry(table, "guide.controller.call",
                    isVietnamese ? "Dùng nút X / Y để gọi GA hoặc Bảo vệ." :
                    isJapanese ? "X / Yボタンで総務または警備へ連絡します。" :
                    "Use X / Y to call GA or Security.");
                AddEntry(table, "guide.controller.wait",
                    isVietnamese ? "Vui lòng chờ..." :
                    isJapanese ? "しばらくお待ちください…" :
                    "Please wait...");
                AddEntry(table, "guide.controller.advance",
                    isVietnamese ? "Nhấn cò trỏ để xem hướng dẫn tiếp theo" :
                    isJapanese ? "トリガーを押して次へ" :
                    "Press the trigger to continue");
                AddEntry(table, "guide.controller.complete",
                    isVietnamese ? "Đã hoàn tất hướng dẫn. Chọn TIẾP TỤC để sang bước kế tiếp." :
                    isJapanese ? "コントローラーの説明は完了です。「続ける」を選択してください。" :
                    "Controller guide complete. Select CONTINUE to proceed.");
                EditorUtility.SetDirty(table);
                EditorUtility.SetDirty(table.SharedData);
            }
        }

        private static void AddEntry(StringTable table, string key, string value)
        {
            StringTableEntry entry = table.GetEntry(key);
            if (entry == null)
                table.AddEntry(key, value);
            else
                entry.Value = value;
        }

        private static GameObject CreateRect(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            GameObject gameObject = new(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return gameObject;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            string value,
            TMP_FontAsset font,
            float fontSize,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            TextAlignmentOptions alignment)
        {
            GameObject gameObject = CreateRect(name, parent, anchorMin, anchorMax);
            TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private static void CreateWorldLabel(string name, Transform parent, string value, TMP_FontAsset font)
        {
            if (parent == null) return;
            GameObject label = new(name, typeof(RectTransform), typeof(TextMeshPro));
            label.transform.SetParent(parent, false);
            label.transform.localPosition = new Vector3(0f, 0.018f, -0.004f);
            label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            label.transform.localScale = Vector3.one * 0.012f;
            TextMeshPro text = label.GetComponent<TextMeshPro>();
            text.text = value;
            text.font = font;
            text.fontSize = 4f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
        }

        private static Transform Find(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(transform => transform.name == name);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetReference(SerializedObject serializedObject, string name, UnityEngine.Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(name);
            if (property == null)
                throw new InvalidOperationException($"Serialized property not found: {name}");
            property.objectReferenceValue = value;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
