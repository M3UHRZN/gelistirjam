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

        private const string RequiredGlyphs =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789" +
            "ÇĞİÖŞÜçğıöşü" +
            " .,:;!?/\\|[](){}<>+-=_@#$%^&*'\"`~";

        internal static readonly Color Base = new Color32(6, 16, 12, 235);
        internal static readonly Color PanelBackground = new Color32(6, 16, 12, 190);
        internal static readonly Color Phosphor = new Color32(121, 255, 155, 235);
        internal static readonly Color MutedPhosphor = new Color32(121, 255, 155, 112);
        internal static readonly Color Danger = new Color32(255, 90, 54, 242);

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
                90,
                9,
                GlyphRenderMode.SDFAA,
                1024,
                1024,
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
            created.material.name = "CascadiaMono SDF Material";
            created.material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            created.material.SetColor(ShaderUtilities.ID_OutlineColor,
                new Color32(6, 16, 12, 220));
            created.material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.08f);

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
                Debug.LogError(
                    $"[HUD Builder] Saved Cascadia font asset failed validation: {savedError}. " +
                    "Existing HUD was not changed.");
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

            if (fontAsset.atlasTextures == null || fontAsset.atlasTextures.Length != 1 ||
                fontAsset.atlasTexture == null)
            {
                error = "atlas texture is missing or multi-atlas is enabled";
                return false;
            }

            if (fontAsset.material == null || fontAsset.material.shader == null)
            {
                error = "material or shader is missing";
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
    }
}
