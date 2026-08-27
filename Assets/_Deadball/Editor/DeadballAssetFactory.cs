using System.IO;
using Deadball.Config;
using UnityEditor;
using UnityEngine;

namespace Deadball.Editor
{
    /// <summary>
    /// Creates the project-level assets the greybox needs: layers, tuning assets, and materials.
    /// </summary>
    /// <remarks>
    /// Everything here is idempotent. The Day 1 setup is expected to be re-run repeatedly while the
    /// arena changes shape, and re-running it must never clobber a tuning value the developer has
    /// already moved - which is exactly the thing you do not want to discover at 2am.
    /// </remarks>
    public static class DeadballAssetFactory
    {
        public const string Root = "Assets/_Deadball";
        public const string DataFolder = Root + "/Data";
        public const string MaterialFolder = Root + "/Materials";
        public const string PrefabFolder = Root + "/Prefabs";
        public const string SceneFolder = Root + "/Scenes";

        const string UrpLitShader = "Universal Render Pipeline/Lit";
        const string UrpUnlitShader = "Universal Render Pipeline/Unlit";

        /// <summary>Adds any of the four Deadball layers that are not in the project yet.</summary>
        public static void EnsureLayers()
        {
            Object tagManagerAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            var tagManager = new SerializedObject(tagManagerAsset);
            SerializedProperty layers = tagManager.FindProperty("layers");

            foreach (string layerName in DeadballLayers.All)
            {
                if (LayerExists(layers, layerName)) continue;

                // 0-7 are Unity's built-ins and cannot be renamed.
                int slot = FirstFreeSlot(layers);
                if (slot < 0)
                {
                    Debug.LogError($"[Deadball] No free layer slots left for '{layerName}'.");
                    continue;
                }

                layers.GetArrayElementAtIndex(slot).stringValue = layerName;
            }

            tagManager.ApplyModifiedProperties();
        }

        public static MatchConfig EnsureMatchConfig() =>
            EnsureAsset<MatchConfig>($"{DataFolder}/MatchConfig.asset");

        public static FighterPalette EnsurePalette() =>
            EnsureAsset<FighterPalette>($"{DataFolder}/FighterPalette.asset");

        /// <summary>Fetches or creates a flat URP material. Existing materials are left alone.</summary>
        public static Material EnsureMaterial(string name, Color colour, float smoothness = 0.1f, Color? emission = null)
        {
            EnsureFolder(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find(UrpLitShader);
            if (shader == null)
            {
                Debug.LogError($"[Deadball] Shader '{UrpLitShader}' not found. Is URP active?");
                return null;
            }

            var material = new Material(shader) { color = colour };
            material.SetFloat("_Smoothness", smoothness);

            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                material.SetColor("_EmissionColor", emission.Value);
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>Unlit material for things that must not pick up arena lighting - trails, shadows.</summary>
        public static Material EnsureUnlitMaterial(string name, Color colour, bool transparent)
        {
            EnsureFolder(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find(UrpUnlitShader);
            if (shader == null) return null;

            var material = new Material(shader) { color = colour };

            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_ZWrite", 0f);
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        public static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = Path.GetFileName(folder);

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static bool LayerExists(SerializedProperty layers, string name)
        {
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == name)
                    return true;
            }

            return false;
        }

        static int FirstFreeSlot(SerializedProperty layers)
        {
            for (int i = 8; i < layers.arraySize; i++)
            {
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                    return i;
            }

            return -1;
        }
    }
}
