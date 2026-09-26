using System;
using System.Collections.Generic;
using System.Linq;
using _Scripts.Controller;
using _Scripts.Environments.EmergencyContact;
using _Scripts.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Spaxtek.EditorTools
{
    internal static class EmergencyContactPhaseSetup
    {
        private const string RootFolder = "Assets/Environments/EmergencyContact";
        private const string MaterialFolder = RootFolder + "/Materials";
        private const string PhoneMaterialPath = MaterialFolder + "/Emergency Phone Material.mat";
        private const string PhonePrefabPath = RootFolder + "/Emergency Phone.prefab";
        private const string ContactUIPrefabPath = "Assets/UIs/Variants/Contact Emergency Scene.prefab";
        private const string EscapeUIPrefabPath = "Assets/UIs/Variants/Escape Scene.prefab";
        private const string ApplicationPrefabPath = "Assets/Prefabs/Application/Application Runtime.prefab";
        private const string HighlightMaterialPath = "Assets/Environments/Factory/CircuitBreaker/Materials/Highlight Circuit Breaker Material.mat";

        private static readonly string[] EnvironmentScenePaths =
        {
            "Assets/Scenes/Factory Scene.unity",
            "Assets/Scenes/Park Scene.unity"
        };

        [MenuItem("Tools/Fire Training/Setup Emergency Contact Phase")]
        public static void Run()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(MaterialFolder);
            UpdateLocalizationTables();
            Material phoneMaterial = EnsurePhoneMaterial();
            GameObject phonePrefab = EnsurePhonePrefab(phoneMaterial);
            GameObject contactUIPrefab = EnsureContactUIPrefab();
            EnsureApplicationPrefab(contactUIPrefab);
            for (int index = 0; index < EnvironmentScenePaths.Length; index++) EnsurePhoneInScene(EnvironmentScenePaths[index], phonePrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Emergency Contact Setup] Added the contact phase, UI, and one emergency phone per environment.");
        }

        private static Material EnsurePhoneMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(PhoneMaterialPath);
            if (material != null) return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("No suitable shader was found for the emergency phone material.");
            material = new Material(shader) { name = "Emergency Phone Material", color = new Color(0.12f, 0.28f, 0.42f, 1f) };
            AssetDatabase.CreateAsset(material, PhoneMaterialPath);
            return material;
        }

        private static GameObject EnsurePhonePrefab(Material phoneMaterial)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PhonePrefabPath);
            if (existing != null) return existing;

            GameObject root = new("Emergency Phone");
            try
            {
                root.layer = 9;
                BoxCollider collider = root.AddComponent<BoxCollider>();
                collider.size = new Vector3(0.42f, 0.3f, 0.22f);
                EmergencyContactController controller = root.AddComponent<EmergencyContactController>();
                EmergencyContactInteractable interactable = root.AddComponent<EmergencyContactInteractable>();
                _Scripts.Environments.Factory.HoverMaterialHighlight highlight = root.AddComponent<_Scripts.Environments.Factory.HoverMaterialHighlight>();

                GameObject view = new("View");
                view.layer = 9;
                view.transform.SetParent(root.transform, false);

                GameObject model = GameObject.CreatePrimitive(PrimitiveType.Cube);
                model.name = "Cube Model";
                model.layer = 9;
                model.transform.SetParent(view.transform, false);
                model.transform.localScale = new Vector3(0.36f, 0.24f, 0.16f);
                Object.DestroyImmediate(model.GetComponent<Collider>());
                Renderer renderer = model.GetComponent<Renderer>();
                renderer.sharedMaterial = phoneMaterial;

                GameObject hintTarget = new("Hint Target");
                hintTarget.layer = 9;
                hintTarget.transform.SetParent(root.transform, false);
                hintTarget.transform.localPosition = new Vector3(0f, 0.18f, 0f);

                GameObject uiAnchor = new("UI Point - Emergency Contact");
                uiAnchor.transform.SetParent(root.transform, false);
                uiAnchor.transform.localPosition = new Vector3(0f, 0.55f, 0.5f);

                SerializedObject serializedHighlight = new(highlight);
                SetObjectArray(serializedHighlight.FindProperty("_renderers"), renderer);
                serializedHighlight.FindProperty("_highlightMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(HighlightMaterialPath);
                serializedHighlight.ApplyModifiedPropertiesWithoutUndo();

                SerializedObject serializedInteractable = new(interactable);
                serializedInteractable.FindProperty("_contactController").objectReferenceValue = controller;
                serializedInteractable.FindProperty("_hintTarget").objectReferenceValue = hintTarget.transform;
                serializedInteractable.FindProperty("_hoverHighlight").objectReferenceValue = highlight;
                SetObjectArray(serializedInteractable.FindProperty("_renderers"), renderer);
                serializedInteractable.ApplyModifiedPropertiesWithoutUndo();

                SerializedObject serializedController = new(controller);
                SetObjectArray(serializedController.FindProperty("_steps"), interactable);
                serializedController.FindProperty("_uiAnchor").objectReferenceValue = uiAnchor.transform;
                serializedController.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, PhonePrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject EnsureContactUIPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ContactUIPrefabPath) == null)
            {
                if (!AssetDatabase.CopyAsset(EscapeUIPrefabPath, ContactUIPrefabPath)) throw new InvalidOperationException("Unable to copy the Escape UI prefab.");
                AssetDatabase.ImportAsset(ContactUIPrefabPath, ImportAssetOptions.ForceSynchronousImport);
            }

            GameObject root = PrefabUtility.LoadPrefabContents(ContactUIPrefabPath);
            try
            {
                root.name = "Contact Emergency Scene";
                EscapeScene escapeScene = root.GetComponentInChildren<EscapeScene>(true);
                GameObject host = escapeScene != null ? escapeScene.gameObject : root;
                if (escapeScene != null) Object.DestroyImmediate(escapeScene, true);
                ContactEmergencyScene contactScene = host.GetComponent<ContactEmergencyScene>();
                if (contactScene == null) contactScene = host.AddComponent<ContactEmergencyScene>();

                TMP_Text title = FindText(root, "txtTitle");
                TMP_Text guide = FindText(root, "txtGuide");
                if (title != null)
                {
                    title.SetText("CONTACT THE EMERGENCY TEAM");
                    ConfigureLocalizer(title, "contact.title");
                }
                if (guide != null) guide.SetText("Go to the emergency phone near the exit and interact with it to contact GA (2121) or Security (2480).");

                Slider timer = root.GetComponentInChildren<Slider>(true);
                if (timer != null) timer.gameObject.SetActive(false);

                SerializedObject serializedScene = new(contactScene);
                serializedScene.FindProperty("_txtGuide").objectReferenceValue = guide;
                serializedScene.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, ContactUIPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(ContactUIPrefabPath);
        }

        private static void EnsureApplicationPrefab(GameObject contactUIPrefab)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ApplicationPrefabPath);
            try
            {
                UIController uiController = root.GetComponentInChildren<UIController>(true);
                if (uiController == null) throw new InvalidOperationException("Application Runtime prefab has no UIController.");

                ContactEmergencyScene contactScene = uiController.GetComponentInChildren<ContactEmergencyScene>(true);
                if (contactScene == null)
                {
                    GameObject instance = PrefabUtility.InstantiatePrefab(contactUIPrefab, uiController.transform) as GameObject;
                    if (instance == null) throw new InvalidOperationException("Unable to instantiate Contact Emergency UI in the Application Runtime prefab.");
                    instance.name = "Contact Emergency Scene";
                    instance.SetActive(false);
                    contactScene = instance.GetComponentInChildren<ContactEmergencyScene>(true);
                }

                SerializedObject serializedController = new(uiController);
                serializedController.FindProperty("_contactEmergencyScene").objectReferenceValue = contactScene;
                serializedController.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, ApplicationPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsurePhoneInScene(string scenePath, GameObject phonePrefab)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                EmergencyContactController[] existingPhones = scene.GetRootGameObjects().SelectMany(gameObject => gameObject.GetComponentsInChildren<EmergencyContactController>(true)).ToArray();
                if (existingPhones.Length > 1) throw new InvalidOperationException($"{scenePath} contains more than one emergency phone.");
                if (existingPhones.Length == 0)
                {
                    EmergencyExitSpawnPoint exitPoint = scene.GetRootGameObjects().SelectMany(gameObject => gameObject.GetComponentsInChildren<EmergencyExitSpawnPoint>(true)).FirstOrDefault();
                    if (exitPoint == null) throw new InvalidOperationException($"{scenePath} has no emergency exit spawn point.");
                    GameObject phone = PrefabUtility.InstantiatePrefab(phonePrefab, scene) as GameObject;
                    if (phone == null) throw new InvalidOperationException($"Unable to instantiate the emergency phone in {scenePath}.");
                    phone.name = "Emergency Phone";
                    phone.transform.SetPositionAndRotation(exitPoint.transform.position + exitPoint.transform.right * 1.1f + Vector3.up * 1.2f, exitPoint.transform.rotation);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void UpdateLocalizationTables()
        {
            Dictionary<string, Dictionary<string, string>> values = new()
            {
                ["en"] = new Dictionary<string, string>
                {
                    ["contact.title"] = "CONTACT THE EMERGENCY TEAM",
                    ["contact.instruction"] = "Go to the emergency phone near the exit and interact with it to contact GA (2121) or Security (2480).",
                    ["hint.contact_emergency_team"] = "Use the emergency phone near the exit to contact GA (2121) or Security (2480).",
                    ["guide.instructions_full"] = "Before the fire drill:\n\n1. Explore the area and locate the emergency exit.\n\n2. When a fire appears, choose an extinguisher that matches the fire type.\n\n3. Remove the safety pin.\n\n4. Aim at the base of the fire.\n\n5. Squeeze the lever and sweep side to side.\n\n6. After extinguishing the fire, use the emergency phone to contact GA or Security.\n\n7. Follow the exit marker."
                },
                ["vi"] = new Dictionary<string, string>
                {
                    ["contact.title"] = "LIÊN LẠC ĐỘI ỨNG PHÓ KHẨN CẤP",
                    ["contact.instruction"] = "Đi đến điện thoại khẩn cấp gần lối thoát và tương tác để liên lạc GA (2121) hoặc Bảo vệ (2480).",
                    ["hint.contact_emergency_team"] = "Dùng điện thoại khẩn cấp gần lối thoát để liên lạc GA (2121) hoặc Bảo vệ (2480).",
                    ["guide.instructions_full"] = "Trước khi diễn tập:\n\n1. Khám phá khu vực và xác định lối thoát hiểm.\n\n2. Khi có cháy, chọn bình chữa cháy phù hợp với loại đám cháy.\n\n3. Rút chốt an toàn.\n\n4. Hướng vòi phun vào gốc lửa.\n\n5. Bóp cò và lia vòi qua lại.\n\n6. Sau khi dập lửa, dùng điện thoại khẩn cấp để liên lạc GA hoặc Bảo vệ.\n\n7. Đi theo chỉ dẫn đến lối thoát hiểm."
                },
                ["ja"] = new Dictionary<string, string>
                {
                    ["contact.title"] = "緊急対応チームへ連絡",
                    ["contact.instruction"] = "非常口付近の非常電話を操作し、総務（2121）または警備（2480）へ連絡してください。",
                    ["hint.contact_emergency_team"] = "非常口付近の非常電話で総務（2121）または警備（2480）へ連絡してください。",
                    ["guide.instructions_full"] = "訓練を始める前に：\n\n1. 周囲を確認し、非常口の位置を把握します。\n\n2. 火災が発生したら、種類に合った消火器を選びます。\n\n3. 安全ピンを抜きます。\n\n4. ノズルを火元に向けます。\n\n5. レバーを握り、左右に噴射します。\n\n6. 消火後、非常電話で総務または警備へ連絡します。\n\n7. 案内表示に従って非常口へ向かいます。"
                }
            };

            StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection("UI");
            if (collection == null) throw new InvalidOperationException("UI localization table collection was not found.");
            foreach (KeyValuePair<string, Dictionary<string, string>> localeValues in values)
            {
                StringTable table = collection.GetTable(localeValues.Key) as StringTable;
                if (table == null) throw new InvalidOperationException($"UI localization table for {localeValues.Key} was not found.");
                foreach (KeyValuePair<string, string> entry in localeValues.Value) table.AddEntry(entry.Key, entry.Value);
                EditorUtility.SetDirty(table);
                EditorUtility.SetDirty(table.SharedData);
            }
        }

        private static void ConfigureLocalizer(TMP_Text text, string key)
        {
            LocalizeStringEvent localizer = text.GetComponent<LocalizeStringEvent>();
            if (localizer == null) localizer = text.gameObject.AddComponent<LocalizeStringEvent>();
            localizer.StringReference = new LocalizedString("UI", key);
            EditorUtility.SetDirty(localizer);
        }

        private static TMP_Text FindText(GameObject root, string name)
        {
            return root.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.name == name);
        }

        private static void SetObjectArray(SerializedProperty property, params Object[] values)
        {
            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++) property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
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
