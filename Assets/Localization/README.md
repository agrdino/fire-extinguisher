# Unity Localization

This project uses the official Unity Localization package (`com.unity.localization`), not a custom localization framework.

## Locales

- `vi` - Vietnamese
- `en` - English (project fallback)
- `ja` - Japanese

Unity uses the standard locale identifiers above. The language selector is inserted into the application flow as:

`Environment selection -> Language selection -> Safety Guide`

The selected locale is persisted by Unity through the PlayerPrefs key `fire-extinguisher.locale`.

## Folder layout

- `Settings/` - Unity Localization Settings
- `Locales/` - Locale assets
- `Tables/` - the `UI` String Table Collection and its three locale tables
- `Prefabs/` - language selection panel
- `Runtime/` - application UI integration only
- `Fonts/` - Noto Sans JP source, TMP font asset, and license
- `Editor/` - translation catalog and optional rebuild tool

`Assets/AddressableAssetsData` remains at Unity Addressables' required default configuration path. All localization-owned content is kept in this folder.

## Add or edit text

1. Edit the `UI` String Table Collection in Unity's Localization Tables window, or update `Editor/LocalizationTranslations.json`.
2. Reference the entry from a `LocalizeStringEvent` for static TMP text, or from `LocalizedString` for runtime-generated text.
3. If the JSON catalog or UI prefab text mapping changed, run `Tools > Localization > Rebuild Unity Localization`.

The rebuild command is manual by design, so opening the project does not rewrite or dirty scenes and prefabs.
