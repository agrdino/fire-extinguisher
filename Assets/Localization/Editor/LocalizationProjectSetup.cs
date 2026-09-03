using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using _Scripts.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Spaxtek.EditorTools
{
    internal static class LocalizationProjectSetup
    {
        private const string RootFolder = "Assets/Localization";
        private const string SettingsFolder = RootFolder + "/Settings";
        private const string LocalesFolder = RootFolder + "/Locales";
        private const string TablesFolder = RootFolder + "/Tables";
        private const string PrefabsFolder = RootFolder + "/Prefabs";
        private const string FontSourcePath = RootFolder + "/Fonts/NotoSansJP-Variable.ttf";
        private const string FontAssetPath = RootFolder + "/Fonts/NotoSansJP Dynamic SDF.asset";
        private const string SettingsPath = SettingsFolder + "/Localization Settings.asset";
        private const string CatalogPath = RootFolder + "/Editor/LocalizationTranslations.json";
        private const string LanguagePrefabPath = PrefabsFolder + "/Select Language Scene.prefab";
        private const string SourcePanelPath = "Assets/UIs/Variants/Ready View.prefab";
        private const string TableName = "UI";

        [Serializable]
        private sealed class TranslationCatalog
        {
            public Translation[] items = Array.Empty<Translation>();
        }

        [Serializable]
        private sealed class Translation
        {
            public string key = string.Empty;
            public string en = string.Empty;
            public string vi = string.Empty;
            public string ja = string.Empty;
        }

        private static Dictionary<string, string> _staticKeyByText;

        [MenuItem("Tools/Localization/Rebuild Unity Localization")]
        private static void RebuildFromMenu()
        {
            RunSetup();
        }

        private static void RunSetup()
        {
            try
            {
                EnsureFolders();
                Translation[] translations = LoadTranslations();
                _staticKeyByText = BuildStaticLookup(translations);
                TMP_FontAsset fontAsset = EnsureFontAsset();
                LocalizationSettings settings = EnsureSettingsAndLocales(out List<Locale> locales);
                StringTableCollection collection = EnsureStringTables(locales, translations);
                GameObject languagePrefab = EnsureLanguagePrefab();
                LocalizePrefabAssets(fontAsset);
                LocalizeScenes(fontAsset, languagePrefab);
                EditorUtility.SetDirty(settings);
                EditorUtility.SetDirty(collection);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[Localization Setup] Complete: en, vi, ja; UI tables; language panel; localized scene UI.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static Translation[] LoadTranslations()
        {
            string json = File.ReadAllText(CatalogPath);
            TranslationCatalog catalog = JsonUtility.FromJson<TranslationCatalog>(json);
            if (catalog?.items == null || catalog.items.Length == 0)
                throw new InvalidOperationException("Localization translation catalog is empty.");
            return catalog.items;
        }

        private static void EnsureFolders()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(SettingsFolder);
            EnsureFolder(LocalesFolder);
            EnsureFolder(TablesFolder);
            EnsureFolder(PrefabsFolder);
            EnsureFolder(RootFolder + "/Fonts");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static TMP_FontAsset EnsureFontAsset()
        {
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
                return existing;

            AssetDatabase.ImportAsset(FontSourcePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontSourcePath);
            if (sourceFont == null)
                throw new InvalidOperationException($"Unable to import font at {FontSourcePath}.");

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            fontAsset.name = "NotoSansJP Dynamic SDF";
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

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

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            return fontAsset;
        }

        private static LocalizationSettings EnsureSettingsAndLocales(out List<Locale> locales)
        {
            LocalizationSettings settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                settings.name = "Fire Extinguisher Localization Settings";
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            LocalizationEditorSettings.ActiveLocalizationSettings = settings;

            Locale english = EnsureLocale("en", "English");
            Locale vietnamese = EnsureLocale("vi", "Tiếng Việt");
            Locale japanese = EnsureLocale("ja", "日本語");
            locales = new List<Locale> { english, vietnamese, japanese };

            LocalizationSettings.ProjectLocale = english;
            List<IStartupLocaleSelector> selectors = settings.GetStartupLocaleSelectors();
            selectors.Clear();
            selectors.Add(new CommandLineLocaleSelector());
            selectors.Add(new PlayerPrefLocaleSelector { PlayerPreferenceKey = "fire-extinguisher.locale" });
            selectors.Add(new SystemLocaleSelector());
            selectors.Add(new SpecificLocaleSelector { LocaleId = english.Identifier });

            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static Locale EnsureLocale(string code, string displayName)
        {
            Locale locale = LocalizationEditorSettings.GetLocale(code);
            if (locale != null)
                return locale;

            string path = $"{LocalesFolder}/{code}.asset";
            locale = AssetDatabase.LoadAssetAtPath<Locale>(path);
            if (locale == null)
            {
                locale = Locale.CreateLocale(code);
                locale.name = $"{displayName} ({code})";
                AssetDatabase.CreateAsset(locale, path);
            }

            LocalizationEditorSettings.AddLocale(locale);
            EditorUtility.SetDirty(locale);
            return locale;
        }

        private static StringTableCollection EnsureStringTables(IList<Locale> locales, IEnumerable<Translation> translations)
        {
            StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(TableName);
            if (collection == null)
                collection = LocalizationEditorSettings.CreateStringTableCollection(TableName, TablesFolder, locales);

            foreach (Locale locale in locales)
            {
                StringTable table = collection.GetTable(locale.Identifier) as StringTable;
                if (table == null)
                    table = collection.AddNewTable(locale.Identifier) as StringTable;
                if (table == null)
                    throw new InvalidOperationException($"Unable to create String Table for {locale.Identifier.Code}.");

                foreach (Translation translation in translations)
                {
                    string value = locale.Identifier.Code switch
                    {
                        "vi" => translation.vi,
                        "ja" => translation.ja,
                        _ => translation.en
                    };

                    table.AddEntry(translation.key, value);
                }

                LocalizationEditorSettings.SetPreloadTableFlag(table, true);
                EditorUtility.SetDirty(table);
                EditorUtility.SetDirty(table.SharedData);
            }

            return collection;
        }

        private static GameObject EnsureLanguagePrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(LanguagePrefabPath) == null)
            {
                if (!AssetDatabase.CopyAsset(SourcePanelPath, LanguagePrefabPath))
                    throw new InvalidOperationException("Unable to copy the source UI panel for language selection.");
                AssetDatabase.ImportAsset(LanguagePrefabPath, ImportAssetOptions.ForceSynchronousImport);
            }

            GameObject root = PrefabUtility.LoadPrefabContents(LanguagePrefabPath);
            try
            {
                root.name = "Select Language Scene";
                ReadyView readyView = root.GetComponentInChildren<ReadyView>(true);
                GameObject host = readyView != null ? readyView.gameObject : root;
                if (readyView != null)
                    Object.DestroyImmediate(readyView, true);

                SelectLanguageScene languageScene = host.GetComponent<SelectLanguageScene>();
                if (languageScene == null)
                    languageScene = host.AddComponent<SelectLanguageScene>();

                TMP_Text title = FindText(root, "txtTitle");
                TMP_Text instruction = FindText(root, "txtGuide");
                if (title != null) title.SetText("SELECT LANGUAGE");
                if (instruction != null) instruction.SetText("Choose your preferred language.");

                Button continueButton = FindButton(root, "btnContinue") ?? FindButton(root, "btnStart");
                if (continueButton == null)
                    throw new InvalidOperationException("Language panel source is missing btnStart.");
                continueButton.name = "btnContinue";
                ConfigureButton(continueButton, "CONTINUE", new Vector2(260f, -390f), new Vector2(500f, 100f));

                Button vietnameseButton = EnsureButtonClone(continueButton, "btnVietnamese", "Tiếng Việt", new Vector2(-430f, -120f), new Vector2(360f, 110f));
                Button englishButton = EnsureButtonClone(continueButton, "btnEnglish", "English", new Vector2(0f, -120f), new Vector2(360f, 110f));
                Button japaneseButton = EnsureButtonClone(continueButton, "btnJapanese", "日本語", new Vector2(430f, -120f), new Vector2(360f, 110f));
                Button backButton = FindButton(root, "btnBack");
                if (backButton != null)
                    Object.DestroyImmediate(backButton.gameObject);

                SerializedObject serializedScene = new(languageScene);
                serializedScene.FindProperty("_btnVietnamese").objectReferenceValue = vietnameseButton;
                serializedScene.FindProperty("_btnEnglish").objectReferenceValue = englishButton;
                serializedScene.FindProperty("_btnJapanese").objectReferenceValue = japaneseButton;
                serializedScene.FindProperty("_btnContinue").objectReferenceValue = continueButton;
                serializedScene.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, LanguagePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(LanguagePrefabPath);
        }

        private static Button EnsureButtonClone(Button template, string name, string label, Vector2 position, Vector2 size)
        {
            Button existing = template.transform.parent.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => button.name == name);
            if (existing != null)
            {
                ConfigureButton(existing, label, position, size);
                return existing;
            }

            GameObject clone = Object.Instantiate(template.gameObject, template.transform.parent);
            clone.name = name;
            Button button = clone.GetComponent<Button>();
            ConfigureButton(button, label, position, size);
            return button;
        }

        private static void ConfigureButton(Button button, string label, Vector2 position, Vector2 size)
        {
            RectTransform rect = button.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = position;
                rect.sizeDelta = size;
                rect.localScale = Vector3.one;
            }

            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.SetText(label);
                text.enableAutoSizing = true;
                text.fontSizeMin = 28f;
                text.fontSizeMax = 48f;
            }
        }

        private static void LocalizePrefabAssets(TMP_FontAsset fontAsset)
        {
            List<string> paths = AssetDatabase.FindAssets("t:Prefab", new[]
                {
                    "Assets/UIs",
                    "Assets/Environments/EmergencyExit",
                    PrefabsFolder
                })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .OrderBy(path => path.Contains("/Variants/") ? 1 : 0)
                .ThenBy(path => path.Count(character => character == '/'))
                .ThenBy(path => path)
                .ToList();

            foreach (string path in paths)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed = LocalizeTexts(root.GetComponentsInChildren<TMP_Text>(true), fontAsset);
                    if (changed)
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void LocalizeScenes(TMP_FontAsset fontAsset, GameObject languagePrefab)
        {
            string[] scenePaths = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path)
                .ToArray();

            foreach (string scenePath in scenePaths)
            {
                Scene scene = SceneManager.GetSceneByPath(scenePath);
                bool wasLoaded = scene.IsValid() && scene.isLoaded;
                if (!wasLoaded)
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

                try
                {
                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true)
                            .Where(text => !PrefabUtility.IsPartOfPrefabInstance(text.gameObject))
                            .ToArray();
                        LocalizeTexts(texts, fontAsset);
                    }

                    if (scenePath.EndsWith("/SampleScene.unity", StringComparison.OrdinalIgnoreCase))
                        EnsureLanguagePanelInScene(scene, languagePrefab);

                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                finally
                {
                    if (!wasLoaded)
                        EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void EnsureLanguagePanelInScene(Scene scene, GameObject languagePrefab)
        {
            SelectLanguageScene languageScene = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<SelectLanguageScene>(true))
                .FirstOrDefault();

            if (languageScene == null)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(languagePrefab, scene) as GameObject;
                if (instance == null)
                    throw new InvalidOperationException("Unable to instantiate the language panel.");
                instance.name = "Select Language Scene";
                instance.SetActive(false);
                languageScene = instance.GetComponentInChildren<SelectLanguageScene>(true);
            }

            UIController controller = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<UIController>(true))
                .FirstOrDefault();
            if (controller == null)
                throw new InvalidOperationException("SampleScene is missing UIController.");

            SerializedObject serializedController = new(controller);
            SerializedProperty property = serializedController.FindProperty("_selectLanguageScene");
            if (property == null)
                throw new InvalidOperationException("UIController._selectLanguageScene was not found.");
            property.objectReferenceValue = languageScene;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        private static bool LocalizeTexts(IEnumerable<TMP_Text> texts, TMP_FontAsset fontAsset)
        {
            bool changed = false;
            foreach (TMP_Text text in texts)
            {
                if (text == null)
                    continue;

                if (fontAsset != null && text.font != fontAsset)
                {
                    text.font = fontAsset;
                    EditorUtility.SetDirty(text);
                    changed = true;
                }

                string normalized = Normalize(text.text);
                if (!_staticKeyByText.TryGetValue(normalized, out string key))
                    continue;
                if (key.StartsWith("hint.", StringComparison.Ordinal) && key != "hint.title")
                    continue;

                LocalizeStringEvent localizer = text.GetComponent<LocalizeStringEvent>();
                if (localizer == null)
                {
                    localizer = text.gameObject.AddComponent<LocalizeStringEvent>();
                    changed = true;
                }

                if (localizer.StringReference == null
                    || localizer.StringReference.TableReference.TableCollectionName != TableName
                    || localizer.StringReference.TableEntryReference.Key != key)
                {
                    localizer.StringReference = new LocalizedString(TableName, key);
                    changed = true;
                }

                bool hasListener = false;
                for (int index = 0; index < localizer.OnUpdateString.GetPersistentEventCount(); index++)
                {
                    if (localizer.OnUpdateString.GetPersistentTarget(index) == text
                        && localizer.OnUpdateString.GetPersistentMethodName(index) == nameof(TMP_Text.SetText))
                    {
                        hasListener = true;
                        break;
                    }
                }

                if (!hasListener)
                {
                    UnityAction<string> listener = text.SetText;
                    UnityEventTools.AddPersistentListener(localizer.OnUpdateString, listener);
                    changed = true;
                }

                EditorUtility.SetDirty(localizer);
            }

            return changed;
        }

        private static TMP_Text FindText(GameObject root, string name)
        {
            return root.GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(text => text.name == name);
        }

        private static Button FindButton(GameObject root, string name)
        {
            return root.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => button.name == name);
        }

        private static Dictionary<string, string> BuildStaticLookup(IEnumerable<Translation> translations)
        {
            Dictionary<string, string> lookup = new(StringComparer.OrdinalIgnoreCase);
            foreach (Translation translation in translations)
            {
                AddLookup(lookup, translation.en, translation.key);
                AddLookup(lookup, translation.vi, translation.key);
                AddLookup(lookup, translation.ja, translation.key);
            }

            AddLookup(lookup, "CHOOSE FIRE EXTINGUISHER", "select.title");
            AddLookup(lookup, "Choose a fire extinguisher suitable for the type of fire.", "select.subtitle");
            return lookup;
        }

        private static void AddLookup(IDictionary<string, string> lookup, string text, string key)
        {
            if (!string.IsNullOrWhiteSpace(text))
                lookup[Normalize(text)] = key;
        }

        private static string Normalize(string value)
        {
            return Regex.Replace(value?.Trim() ?? string.Empty, "\\s+", " ");
        }
    }
}
