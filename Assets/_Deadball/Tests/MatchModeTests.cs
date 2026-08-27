using System.Collections;
using Deadball.AI;
using Deadball.Fighters;
using Deadball.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Deadball.Tests
{
    /// <summary>
    /// The menu's choices actually reaching the arena (OVERLOAD GDD section 21).
    /// </summary>
    /// <remarks>
    /// Solo and Local Versus differ only in what drives player two, and that difference is decided
    /// by one component. These tests hold the seam honest: the wrong roster must never wake, and the
    /// tier chosen on the setup screen must be the tier the house runner actually plays at.
    /// </remarks>
    public class MatchModeTests
    {
        const string ScenePath = "Assets/_Deadball/Scenes/Arena_Greybox.unity";
        const string SettingsPath = "Assets/_Deadball/Data/MatchSettings.asset";
        const string GhostPath = "Assets/_Deadball/Data/AI_Ghost.asset";

        MatchSettings _settings;
        MatchMode _originalMode;
        AiProfile _originalProfile;

        [SetUp]
        public void CaptureSettings()
        {
            _settings = Load<MatchSettings>(SettingsPath);
            _originalMode = _settings.Mode;
            _originalProfile = _settings.AiProfile;
        }

        [TearDown]
        public void RestoreSettings()
        {
            // The settings asset is shared with the editor's own session, so a test must not leave
            // the project on a mode the developer did not choose.
            _settings.Mode = _originalMode;
            _settings.AiProfile = _originalProfile;
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator SoloSpawnsAHouseRunnerAtTheChosenTier()
        {
            var ghost = Load<AiProfile>(GhostPath);
            _settings.Mode = MatchMode.Solo;
            _settings.AiProfile = ghost;

            yield return LoadArena();
            yield return WaitForFrames(4);

            var brains = Object.FindObjectsByType<AiInputSource>(FindObjectsSortMode.None);
            Assert.That(brains.Length, Is.EqualTo(1),
                "Solo is exactly one human and one house runner (11).");

            var fighters = Object.FindObjectsByType<Fighter>(FindObjectsSortMode.None);
            Assert.That(fighters.Length, Is.EqualTo(2), "A match still needs two runners.");

            Assert.That(brains[0].Profile, Is.SameAs(ghost),
                "The tier picked on the setup screen must be the tier that plays (13.3).");
        }

        [UnityTest]
        public IEnumerator VersusNeverWakesTheSoloRoster()
        {
            _settings.Mode = MatchMode.LocalVersus;

            yield return LoadArena();
            yield return WaitForFrames(4);

            var roster = Object.FindFirstObjectByType<SoloRoster>(FindObjectsInactive.Include);
            Assert.That(roster, Is.Not.Null, "The arena should still carry a solo roster.");
            Assert.That(roster.gameObject.activeInHierarchy, Is.False,
                "In versus the solo roster must stay asleep, or it spawns a bot into a human match.");

            Assert.That(Object.FindObjectsByType<AiInputSource>(FindObjectsSortMode.None).Length,
                Is.Zero, "No house runner belongs in Local Versus.");
        }

        // ---------------------------------------------------------------- helpers

        static T Load<T>(string path) where T : Object
        {
#if UNITY_EDITOR
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
#else
            T asset = null;
#endif
            Assert.That(asset, Is.Not.Null, $"missing asset at {path}");
            return asset;
        }

        static IEnumerator LoadArena()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("Arena_Greybox", LoadSceneMode.Single);
#endif
            yield return null;
            StripFeedbacks();
        }

        static IEnumerator WaitForFrames(int frames)
        {
            for (int i = 0; i < frames; i++) yield return null;
        }

        /// <summary>Removes the Feel players, whose freeze feedbacks stop the clock these tests wait on.</summary>
        static void StripFeedbacks()
        {
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go != null && (go.name == "Feedbacks" || go.name == "MMTimeManager"))
                    Object.DestroyImmediate(go);
            }

            Time.timeScale = 1f;
        }
    }
}
