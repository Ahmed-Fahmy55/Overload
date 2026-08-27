using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Deadball.Editor
{
    /// <summary>
    /// The Day 1 setup entry points, in the order they need to run.
    /// </summary>
    /// <remarks>
    /// Split into steps rather than one button because the TextMesh Pro import triggers an asset
    /// database reload: the scene build has to happen after that settles, not during it. The
    /// completion callback exists so the same flow can be driven from the command line, where
    /// nothing is around to press the second button.
    /// </remarks>
    public static class DeadballSetup
    {
        const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        [MenuItem("Deadball/Setup/Run Full Day 1 Setup", priority = 0)]
        public static void RunFullSetupMenu() => RunFullSetup(null);

        /// <summary>Imports whatever is missing, then builds the greybox scene.</summary>
        /// <param name="onComplete">Invoked with the outcome once the whole chain has finished.</param>
        public static void RunFullSetup(Action<bool> onComplete)
        {
            if (TmpEssentialsMissing())
            {
                ImportTmpEssentials(success =>
                {
                    if (!success)
                    {
                        onComplete?.Invoke(false);
                        return;
                    }

                    EditorApplication.delayCall += () => onComplete?.Invoke(BuildGreyboxArena());
                });

                return;
            }

            onComplete?.Invoke(BuildGreyboxArena());
        }

        [MenuItem("Deadball/Setup/1. Import TMP Essentials", priority = 20)]
        public static void ImportTmpEssentialsMenu() => ImportTmpEssentials(null);

        [MenuItem("Deadball/Setup/2. Build Greybox Arena", priority = 21)]
        public static void BuildGreyboxArenaMenu() => BuildGreyboxArena();

        public static bool BuildGreyboxArena()
        {
            if (TmpEssentialsMissing())
            {
                Debug.LogError("[Deadball] Import TMP Essentials first - the HUD labels need them.");
                return false;
            }

            GreyboxArenaBuilder.Build();
            AssetDatabase.SaveAssets();
            return true;
        }

        [MenuItem("Deadball/Setup/Create Layers Only", priority = 40)]
        public static void CreateLayersOnly()
        {
            DeadballAssetFactory.EnsureLayers();
            Debug.Log("[Deadball] Layers ensured: " + string.Join(", ", DeadballLayers.All));
        }

        public static bool TmpEssentialsMissing() =>
            AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(TmpSettingsPath) == null;

        /// <summary>Imports TMP's essential resources without showing the import dialog.</summary>
        public static void ImportTmpEssentials(Action<bool> onComplete)
        {
            string package = FindTmpEssentialsPackage();

            if (package == null)
            {
                Debug.LogError("[Deadball] Could not find the TMP Essential Resources package in the package "
                    + "cache. Import it from Window > TextMeshPro > Import TMP Essential Resources.");
                onComplete?.Invoke(false);
                return;
            }

            AssetDatabase.importPackageCompleted += OnImported;
            AssetDatabase.importPackageFailed += OnFailed;
            AssetDatabase.ImportPackage(package, interactive: false);
            return;

            void OnImported(string name)
            {
                Unsubscribe();
                Debug.Log($"[Deadball] Imported '{name}'.");
                onComplete?.Invoke(true);
            }

            void OnFailed(string name, string error)
            {
                Unsubscribe();
                Debug.LogError($"[Deadball] TMP import failed: {error}");
                onComplete?.Invoke(false);
            }

            void Unsubscribe()
            {
                AssetDatabase.importPackageCompleted -= OnImported;
                AssetDatabase.importPackageFailed -= OnFailed;
            }
        }

        static string FindTmpEssentialsPackage()
        {
            foreach (string root in new[] { "Library/PackageCache", "Packages" })
            {
                if (!Directory.Exists(root)) continue;

                string[] found = Directory.GetFiles(root, "TMP Essential Resources.unitypackage",
                    SearchOption.AllDirectories);

                if (found.Length > 0) return found[0];
            }

            return null;
        }
    }
}
