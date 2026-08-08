using System;
using Game.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// Editor-only primitives and assets for the Arızalı Tesis Terminali HUD.
    /// </summary>
    internal static class HudTerminalFactory
    {
        internal const string FontSourcePath =
            "Assets/_Project/Art/UI/Fonts/CascadiaMono.ttf";
        internal const string FontAssetPath =
            "Assets/_Project/Art/UI/Fonts/CascadiaMono SDF.asset";

        private const string FilledImageSpritePath = "UI/Skin/UISprite.psd";
        private const string FontShaderName = "TextMeshPro/Mobile/Distance Field";
        private const int FontPointSize = 90;
        private const int FontPadding = 9;
        private const int FontAtlasSize = 1024;
        private const float FontOutlineWidth = 0.08f;

        private const string RequiredGlyphs =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789" +
            "ÇĞİÖŞÜçğıöşü" +
            " .,:;!?/\\|[](){}<>+-=_@#$%^&*'\"`~";

        internal static readonly Color Base = new Color32(6, 16, 12, 235);
        internal static readonly Color PanelBackground = new Color32(6, 16, 12, 190);
        internal static readonly Color Phosphor = new Color32(121, 255, 155, 235);
        internal static readonly Color MutedPhosphor = new Color32(121, 255, 155, 112);
        internal static readonly Color Danger = new Color32(255, 90, 54, 242);
        private static readonly Color FontOutlineColor = new Color32(6, 16, 12, 220);

        internal static TMP_FontAsset EnsureFontAsset()
        {
            if (TMP_Settings.instance == null)
            {
                Debug.LogError(
                    "[HUD Builder] TMP Essential Resources are missing; existing HUD was not changed.");
                return null;
            }

            TMP_FontAsset existing =
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
            {
                if (!ValidateFontAsset(existing, out string validationError))
                {
                    Debug.LogError(
                        $"[HUD Builder] Cascadia font asset is invalid: {validationError}. " +
                        "Existing HUD was not changed.", existing);
                    return null;
                }

                return existing;
            }

            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontSourcePath);
            if (sourceFont == null)
            {
                Debug.LogError(
                    $"[HUD Builder] Missing source font at '{FontSourcePath}'; " +
                    "existing HUD was not changed.");
                return null;
            }

            TMP_FontAsset created = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                FontPointSize,
                FontPadding,
                GlyphRenderMode.SDFAA,
                FontAtlasSize,
                FontAtlasSize,
                AtlasPopulationMode.Dynamic,
                false);

            if (created == null)
            {
                Debug.LogError(
                    "[HUD Builder] TMP could not create Cascadia Mono SDF; " +
                    "existing HUD was not changed.", sourceFont);
                return null;
            }

            if (!created.TryAddCharacters(RequiredGlyphs, out string missingCharacters, false) ||
                !string.IsNullOrEmpty(missingCharacters))
            {
                Debug.LogError(
                    $"[HUD Builder] Cascadia Mono is missing required glyphs: " +
                    $"'{missingCharacters}'. Existing HUD was not changed.", sourceFont);
                UnityEngine.Object.DestroyImmediate(created.material);
                UnityEngine.Object.DestroyImmediate(created.atlasTexture);
                UnityEngine.Object.DestroyImmediate(created);
                return null;
            }

            created.name = "CascadiaMono SDF";
            created.atlasPopulationMode = AtlasPopulationMode.Static;
            created.creationSettings = new FontAssetCreationSettings
            {
                sourceFontFileName = "CascadiaMono.ttf",
                sourceFontFileGUID = AssetDatabase.AssetPathToGUID(FontSourcePath),
                faceIndex = 0,
                pointSizeSamplingMode = 0,
                pointSize = FontPointSize,
                padding = FontPadding,
                paddingMode = 2,
                packingMode = 0,
                atlasWidth = FontAtlasSize,
                atlasHeight = FontAtlasSize,
                characterSetSelectionMode = 7,
                characterSequence = RequiredGlyphs,
                referencedFontAssetGUID = string.Empty,
                referencedTextAssetGUID = string.Empty,
                fontStyle = 0,
                fontStyleModifier = 0f,
                renderMode = (int)GlyphRenderMode.SDFAA,
                includeFontFeatures = false
            };
            created.material.name = "CascadiaMono SDF Material";
            created.material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            created.material.SetColor(ShaderUtilities.ID_OutlineColor, FontOutlineColor);
            created.material.SetFloat(ShaderUtilities.ID_OutlineWidth, FontOutlineWidth);

            AssetDatabase.CreateAsset(created, FontAssetPath);
            AssetDatabase.AddObjectToAsset(created.atlasTexture, created);
            AssetDatabase.AddObjectToAsset(created.material, created);
            EditorUtility.SetDirty(created);
            EditorUtility.SetDirty(created.material);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceUpdate);

            TMP_FontAsset saved =
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            string savedError = "asset could not be loaded";
            if (saved == null || !ValidateFontAsset(saved, out savedError))
            {
                bool removed = AssetDatabase.DeleteAsset(FontAssetPath);
                AssetDatabase.SaveAssets();
                Debug.LogError(
                    $"[HUD Builder] Saved Cascadia font asset failed validation: {savedError}. " +
                    $"The invalid asset was {(removed ? "removed" : "not removed")}; " +
                    "existing HUD was not changed.");
                return null;
            }

            return saved;
        }

        internal static RectTransform CreateFullScreenChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        internal static RectTransform CreateChild(Transform parent, string name,
            Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }

        internal static RectTransform CreateStretchChild(RectTransform parent, string name,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform rect = CreateChild(parent, name, Vector2.zero,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        internal static TerminalPanelGraphic CreatePanel(RectTransform parent)
        {
            RectTransform rect = CreateStretchChild(parent, "Panel", Vector2.zero, Vector2.zero);
            rect.SetAsFirstSibling();
            TerminalPanelGraphic panel = rect.gameObject.AddComponent<TerminalPanelGraphic>();
            panel.raycastTarget = false;
            return panel;
        }

        internal static TextMeshProUGUI AddText(RectTransform rect, TMP_FontAsset font,
            float fontSize, TextAlignmentOptions alignment, Color color, string value)
        {
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.text = value;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        internal static Image CreateImage(RectTransform parent, string name, Vector2 anchor,
            Vector2 pivot, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            RectTransform rect = CreateChild(parent, name, anchor, pivot, anchoredPosition, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        internal static Sprite RequireFilledImageSprite()
        {
            Sprite sprite =
                AssetDatabase.GetBuiltinExtraResource<Sprite>(FilledImageSpritePath);
            if (sprite == null)
            {
                throw new InvalidOperationException(
                    $"Built-in UI sprite '{FilledImageSpritePath}' is unavailable.");
            }

            return sprite;
        }

        internal static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        internal static void AssignSerialized(UnityEngine.Object target, string fieldName,
            object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"{target.GetType().Name} has no serialized field '{fieldName}'.");
            }

            switch (value)
            {
                case UnityEngine.Object unityObject:
                    property.objectReferenceValue = unityObject;
                    break;
                case Vector2 vector:
                    property.vector2Value = vector;
                    break;
                case UnityEngine.Object[] array:
                    property.arraySize = array.Length;
                    for (int i = 0; i < array.Length; i++)
                    {
                        property.GetArrayElementAtIndex(i).objectReferenceValue = array[i];
                    }
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported serialized value type for '{fieldName}': " +
                        $"{value?.GetType().FullName ?? "null"}.");
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool ValidateFontAsset(TMP_FontAsset fontAsset, out string error)
        {
            if (fontAsset.faceInfo.familyName != "Cascadia Mono")
            {
                error = $"family is '{fontAsset.faceInfo.familyName}'";
                return false;
            }

            if (fontAsset.atlasPopulationMode != AtlasPopulationMode.Static)
            {
                error = $"atlas mode is {fontAsset.atlasPopulationMode}, expected Static";
                return false;
            }

            var serialized = new SerializedObject(fontAsset);
            SerializedProperty sourceGuidProperty =
                serialized.FindProperty("m_SourceFontFileGUID");
            string expectedSourceGuid = AssetDatabase.AssetPathToGUID(FontSourcePath);
            if (sourceGuidProperty == null ||
                sourceGuidProperty.stringValue != expectedSourceGuid)
            {
                error = "source font GUID does not match the vendored CascadiaMono.ttf";
                return false;
            }

            FontAssetCreationSettings creationSettings = fontAsset.creationSettings;
            if (creationSettings.sourceFontFileGUID != expectedSourceGuid ||
                creationSettings.pointSize != FontPointSize ||
                creationSettings.padding != FontPadding ||
                creationSettings.atlasWidth != FontAtlasSize ||
                creationSettings.atlasHeight != FontAtlasSize ||
                creationSettings.renderMode != (int)GlyphRenderMode.SDFAA)
            {
                error = "saved font creation metadata does not match the generation contract";
                return false;
            }

            if (!Mathf.Approximately(fontAsset.faceInfo.pointSize, FontPointSize))
            {
                error = $"point size is {fontAsset.faceInfo.pointSize}, expected {FontPointSize}";
                return false;
            }

            if (fontAsset.atlasWidth != FontAtlasSize ||
                fontAsset.atlasHeight != FontAtlasSize ||
                fontAsset.atlasPadding != FontPadding ||
                fontAsset.atlasRenderMode != GlyphRenderMode.SDFAA)
            {
                error =
                    $"atlas contract is {fontAsset.atlasWidth}x{fontAsset.atlasHeight}, " +
                    $"padding {fontAsset.atlasPadding}, mode {fontAsset.atlasRenderMode}";
                return false;
            }

            if (fontAsset.atlasTextures == null || fontAsset.atlasTextures.Length != 1 ||
                fontAsset.atlasTexture == null || fontAsset.isMultiAtlasTexturesEnabled)
            {
                error = "atlas texture is missing or multi-atlas is enabled";
                return false;
            }

            if (fontAsset.atlasTexture.width != FontAtlasSize ||
                fontAsset.atlasTexture.height != FontAtlasSize)
            {
                error =
                    $"atlas texture is {fontAsset.atlasTexture.width}x" +
                    $"{fontAsset.atlasTexture.height}, expected {FontAtlasSize}x{FontAtlasSize}";
                return false;
            }

            Material material = fontAsset.material;
            if (material == null || material.shader == null ||
                material.shader.name != FontShaderName)
            {
                error = "material is missing or does not use the expected TMP shader";
                return false;
            }

            if (material.mainTexture != fontAsset.atlasTexture ||
                !material.IsKeywordEnabled(ShaderUtilities.Keyword_Outline) ||
                !Mathf.Approximately(
                    material.GetFloat(ShaderUtilities.ID_OutlineWidth), FontOutlineWidth) ||
                !Approximately(
                    material.GetColor(ShaderUtilities.ID_OutlineColor), FontOutlineColor))
            {
                error = "material atlas or outline settings do not match the HUD contract";
                return false;
            }

            if (!fontAsset.HasCharacters(RequiredGlyphs, out uint[] missing, false, false))
            {
                error = $"required glyphs are missing ({missing.Length})";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool Approximately(Color left, Color right)
        {
            const float tolerance = 0.001f;
            return Mathf.Abs(left.r - right.r) <= tolerance &&
                   Mathf.Abs(left.g - right.g) <= tolerance &&
                   Mathf.Abs(left.b - right.b) <= tolerance &&
                   Mathf.Abs(left.a - right.a) <= tolerance;
        }
    }
}
