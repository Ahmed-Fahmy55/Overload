using UnityEditor;
using UnityEngine;

namespace Deadball.Editor
{
    /// <summary>
    /// Command-line entry points, for driving setup and compile checks from the Unity CLI.
    /// </summary>
    /// <remarks>
    /// These deliberately do not rely on <c>-quit</c>. The TMP import completes on a later editor
    /// tick, and <c>-quit</c> would end the process before that tick ever runs, so each entry point
    /// keeps the batch editor alive and exits itself once the work has actually finished.
    /// </remarks>
    public static class DeadballBatch
    {
        /// <summary>
        /// Runs the whole Day 1 setup and exits with 0 on success, 1 on failure.
        /// </summary>
        /// <example>
        /// unity run "&lt;project&gt;" -- -batchmode -nographics -executeMethod Deadball.Editor.DeadballBatch.RunDay1Setup
        /// </example>
        public static void RunDay1Setup()
        {
            Debug.Log("[Deadball] Batch: starting Day 1 setup.");

            DeadballSetup.RunFullSetup(success =>
            {
                Debug.Log($"[Deadball] Batch: setup {(success ? "succeeded" : "FAILED")}.");
                EditorApplication.Exit(success ? 0 : 1);
            });
        }

        /// <summary>
        /// Does nothing except force a compile and exit, so the CLI can be used as a build check.
        /// </summary>
        /// <remarks>
        /// Reaching this method at all means every assembly compiled - Unity does not run
        /// <c>-executeMethod</c> against a project that failed to build its scripts.
        /// </remarks>
        public static void CompileCheck()
        {
            Debug.Log("[Deadball] Batch: all assemblies compiled.");
            EditorApplication.Exit(0);
        }
    }
}
