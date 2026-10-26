using System;
using System.Collections.Generic;
using System.IO;
using _Scripts.Controller;
using UnityEditor;
using UnityEngine;

namespace _Scripts.Audio.Editor
{
    public static class VoiceProjectConfigurator
    {
        private const string InventoryPath = "Assets/Audio/voice_script_inventory.json";
        private const string VoiceLibraryPath =
            "Assets/Audio/Resources/FireExtinguisherVoiceLibrary.asset";
        private const string RuntimePrefabPath =
            "Assets/Prefabs/Application/Application Runtime.prefab";

        [Serializable]
        private sealed class Inventory
        {
            public List<InventoryItem> items = new List<InventoryItem>();
        }

        [Serializable]
        private sealed class InventoryItem
        {
            public string id;
            public string category;
            public string priority;
        }

        [MenuItem("Tools/Fire Extinguisher/Voice/Configure Project")]
        public static void ConfigureProject()
        {
            AssetDatabase.Refresh();
            EnsureFolder("Assets/Audio/Resources");
            Inventory inventory = LoadInventory();
            VoiceClipLibrary library = BuildVoiceLibrary(inventory);
            ConfigureRuntimePrefab(library);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Voice] Configured {inventory.items.Count} localized voice cues.");
        }

        private static Inventory LoadInventory()
        {
            if (!File.Exists(InventoryPath))
                throw new FileNotFoundException("Voice inventory was not found.", InventoryPath);

            Inventory inventory = JsonUtility.FromJson<Inventory>(File.ReadAllText(InventoryPath));
            if (inventory?.items == null || inventory.items.Count == 0)
                throw new InvalidDataException("Voice inventory contains no items.");
            return inventory;
        }

        private static VoiceClipLibrary BuildVoiceLibrary(Inventory inventory)
        {
            VoiceClipLibrary library =
                AssetDatabase.LoadAssetAtPath<VoiceClipLibrary>(VoiceLibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<VoiceClipLibrary>();
                AssetDatabase.CreateAsset(library, VoiceLibraryPath);
            }

            SerializedObject serializedLibrary = new SerializedObject(library);
            SerializedProperty entries = serializedLibrary.FindProperty("_entries");
            entries.ClearArray();

            for (int index = 0; index < inventory.items.Count; index++)
            {
                InventoryItem item = inventory.items[index];
                if (item == null || string.IsNullOrWhiteSpace(item.id)) continue;

                int entryIndex = entries.arraySize;
                entries.InsertArrayElementAtIndex(entryIndex);
                SerializedProperty entry = entries.GetArrayElementAtIndex(entryIndex);
                entry.FindPropertyRelative("_id").stringValue = item.id.Trim();
                entry.FindPropertyRelative("_category").stringValue = item.category ?? string.Empty;
                entry.FindPropertyRelative("_priority").stringValue = item.priority ?? string.Empty;
                AssignClip(entry, "_vietnamese", item.id, "vi");
                AssignClip(entry, "_english", item.id, "en");
                AssignClip(entry, "_japanese", item.id, "jp");
            }

            serializedLibrary.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            return library;
        }

        private static void AssignClip(
            SerializedProperty entry,
            string propertyName,
            string cueId,
            string language)
        {
            string path = $"Assets/Audio/Clips/Voice/{language}/{cueId}.{language}.wav";
            entry.FindPropertyRelative(propertyName).objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static void ConfigureRuntimePrefab(VoiceClipLibrary library)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(RuntimePrefabPath);
            try
            {
                AudioManager audioManager = root.GetComponentInChildren<AudioManager>(true);
                ApplicationManager applicationManager =
                    root.GetComponentInChildren<ApplicationManager>(true);
                if (audioManager == null || applicationManager == null)
                {
                    throw new MissingReferenceException(
                        "Application Runtime requires AudioManager and ApplicationManager.");
                }

                ConfigureAudioManager(audioManager, library);
                GetOrAdd<VoiceLocaleSync>(audioManager.gameObject);
                ConfigureStatePresenter(audioManager.gameObject, applicationManager);
                ConfigureResultPresenter(audioManager.gameObject, applicationManager);
                PrefabUtility.SaveAsPrefabAsset(root, RuntimePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureAudioManager(
            AudioManager audioManager,
            VoiceClipLibrary library)
        {
            SerializedObject serializedManager = new SerializedObject(audioManager);
            SerializedProperty voice = serializedManager.FindProperty("_voice");
            voice.FindPropertyRelative("_library").objectReferenceValue = library;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureStatePresenter(
            GameObject owner,
            ApplicationManager applicationManager)
        {
            ApplicationStateVoicePresenter presenter =
                GetOrAdd<ApplicationStateVoicePresenter>(owner);
            SerializedObject serializedPresenter = new SerializedObject(presenter);
            serializedPresenter.FindProperty("_applicationManager").objectReferenceValue =
                applicationManager;

            SerializedProperty bindings = serializedPresenter.FindProperty("_bindings");
            bindings.arraySize = 1;
            SerializedProperty binding = bindings.GetArrayElementAtIndex(0);
            binding.FindPropertyRelative("_state").intValue = (int)ApplicationState.Ready;
            SetCue(binding.FindPropertyRelative("_cue"), "welcome.introduction");
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureResultPresenter(
            GameObject owner,
            ApplicationManager applicationManager)
        {
            TrainingResultVoicePresenter presenter =
                GetOrAdd<TrainingResultVoicePresenter>(owner);
            SerializedObject serializedPresenter = new SerializedObject(presenter);
            serializedPresenter.FindProperty("_applicationManager").objectReferenceValue =
                applicationManager;
            SetCue(
                serializedPresenter.FindProperty("_completedElectrical"),
                "completed.electrical.result");
            SetCue(
                serializedPresenter.FindProperty("_completedLiquid"),
                "completed.liquid.result");
            SetCue(
                serializedPresenter.FindProperty("_extinguisherDepleted"),
                "failure.extinguisherDepleted.result");
            SetCue(
                serializedPresenter.FindProperty("_wrongExtinguisherElectrical"),
                "failure.wrongExtinguisher.electrical.tip");
            SetCue(
                serializedPresenter.FindProperty("_wrongExtinguisherLiquid"),
                "failure.wrongExtinguisher.liquid.tip");
            SetCue(
                serializedPresenter.FindProperty("_unknownFailure"),
                "failure.unknown.result");
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetCue(SerializedProperty cueProperty, string id)
        {
            cueProperty.FindPropertyRelative("_id").stringValue = id;
        }

        private static T GetOrAdd<T>(GameObject owner) where T : Component
        {
            T component = owner.GetComponent<T>();
            return component != null ? component : owner.AddComponent<T>();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
                throw new InvalidDataException($"Invalid Unity folder path: {path}");

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
