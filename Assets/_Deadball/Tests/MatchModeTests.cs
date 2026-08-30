using System.Collections;
using Core.Events;
using Deadball.AI;
using Deadball.Config;
using Deadball.Events;
using Deadball.HUD;
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

        [UnityTest]
        public IEnumerator MatchEndCard_AppearsAfterTheBlackout()
        {
            _settings.Mode = MatchMode.LocalVersus;

            yield return LoadArena();
            yield return WaitForFrames(2);

            var card = Object.FindFirstObjectByType<MatchEndScreen>(FindObjectsInactive.Include);
            Assert.That(card, Is.Not.Null, "Every arena needs the match-end card (21).");
            Assert.That(card.IsShowing, Is.False, "The card must stay hidden during a match.");

            EventBus<MatchEnded>.Raise(new MatchEnded(0));

            // It is deliberately delayed so the blackout lands first, so it must NOT be up yet.
            yield return WaitForFrames(2);
            Assert.That(card.IsShowing, Is.False,
                "The card should wait for the blackout rather than stepping on it (3).");

            yield return WaitUnscaled(2.0f);

            Assert.That(card.IsShowing, Is.True, "The card must appear once the delay has passed.");

            var group = card.GetComponent<CanvasGroup>();
            Assert.That(group.alpha, Is.EqualTo(1f).Within(0.001f), "It has to actually be visible.");
            Assert.That(group.blocksRaycasts, Is.True, "Its buttons have to be clickable.");
        }

        [UnityTest]
        public IEnumerator ThePauseScreenRefusesToOpenOverTheEndCard()
        {
            // The end card owns the screen once the match is over and offers the same way out.
            // A pause screen on top of it is two navigable panels fighting for one selection, and
            // the pause one wins - so the player cannot answer the card they are looking at.
            _settings.Mode = MatchMode.LocalVersus;

            yield return LoadArena();
            yield return WaitForFrames(2);

            var pause = Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);
            Assert.That(pause, Is.Not.Null, "every arena carries a pause menu");
            Assert.That(pause.IsMatchOver, Is.False, "precondition: the match is still running");

            pause.Pause();
            Assert.That(pause.IsPaused, Is.True, "it opens normally mid-match");
            pause.Resume();

            EventBus<MatchEnded>.Raise(new MatchEnded(0));
            yield return WaitForFrames(2);

            Assert.That(pause.IsMatchOver, Is.True, "the match end has to be noticed");

            pause.Pause();
            Assert.That(pause.IsPaused, Is.False,
                "the pause screen must not open once the match is over");
            Assert.That(Time.timeScale, Is.EqualTo(1f).Within(0.001f),
                "and a refused pause must not have frozen the game either");

            // A rematch runs rounds again, and the gate has to lift with it.
            EventBus<RoundStarting>.Raise(new RoundStarting(1, 0f));
            yield return WaitForFrames(2);

            Assert.That(pause.IsMatchOver, Is.False, "a new round clears the gate");

            pause.Pause();
            Assert.That(pause.IsPaused, Is.True, "and pausing works again after a rematch");
            pause.Resume();
        }

        [UnityTest]
        public IEnumerator CoreCrossesTheDeckInTheReadableWindow()
        {
            yield return LoadArena();
            yield return WaitForFrames(2);

            var core = Object.FindFirstObjectByType<Deadball.Ball.BallController>();
            var arena = Object.FindFirstObjectByType<ArenaReferences>();
            MatchConfig config = core.Config;

            // The deck the core actually has to cross, not the fallback in the config.
            float span = Mathf.Max(arena.Size.x, arena.Size.y);

            float slowest = span / config.MinThrowSpeed;
            float fastest = span / config.MaxThrowSpeed;

            // Section 8.3: the core crosses the deck in 0.7s-1.6s, and that window is what makes the
            // alarm cue learnable. The Spine's long axis is allowed to run to ~2.0s (15.2). Enlarging
            // an arena without rescaling throw speed silently breaks this - it reads to a player as
            // "everything is easy to dodge", which is exactly how it was found.
            Assert.That(fastest, Is.GreaterThan(0.4f),
                $"A max-charge throw crosses {span}m in {fastest:0.00}s - too fast to react to.");
            Assert.That(slowest, Is.LessThan(2.2f),
                $"A min-charge throw crosses {span}m in {slowest:0.00}s. Past ~2s the core is a "
                + "lob nobody can fail to read, and the whole clamp game goes slack.");
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

        [UnityTest]
        public IEnumerator EveryArenaCameraSitsCloseEnoughToItsOwnDeck()
        {
            // The Spine's rig had been scaled off the longest axis, so a 40x20 deck read as 33%
            // bigger than a 30x30 one and the camera sat 44m out. On a 16:9 screen the long axis is
            // the cheap one - what binds a pitched camera is the depth, and The Spine's is 20m
            // against Greybox's 30m. It was 31% further out than it needed to see the whole deck,
            // which is what "too far from the arena" looks like.
            foreach (string scenePath in new[]
            {
                "Assets/_Deadball/Scenes/Arena_Greybox.unity",
                "Assets/_Deadball/Scenes/Arena_TheSpine.unity",
            })
            {
#if UNITY_EDITOR
                yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                    scenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
                yield break;
#endif
                yield return null;

                var arena = Object.FindFirstObjectByType<ArenaReferences>();
                var cam = Object.FindFirstObjectByType<Unity.Cinemachine.CinemachineCamera>();
                var framing = cam.GetComponent<Unity.Cinemachine.CinemachineGroupFraming>();

                string name = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                float rest = cam.transform.position.magnitude;
                float fullDeck = DistanceThatHoldsTheDeck(
                    arena.Size, cam.Lens.FieldOfView, cam.transform.eulerAngles.x);

                Assert.That(rest, Is.LessThanOrEqualTo(fullDeck),
                    $"{name}: the camera rests at {rest:0.0}m but only needs {fullDeck:0.0}m to hold "
                    + $"the whole {arena.Size.x}x{arena.Size.y} deck - any further out is empty space.");

                Assert.That(rest + framing.DollyRange.y, Is.GreaterThanOrEqualTo(fullDeck - 0.1f),
                    $"{name}: at full zoom-out it reaches {rest + framing.DollyRange.y:0.0}m, short of "
                    + $"the {fullDeck:0.0}m needed to show the deck's borders.");
            }
        }

        /// <summary>Smallest distance along the rig's pitch that still holds all four deck corners.</summary>
        /// <remarks>
        /// Bisected against a real <see cref="Camera"/> rather than a closed form: the pitch turns
        /// the ground plane into a trapezium, and hand-built matrices get Unity's flipped view Z
        /// wrong in a way that silently reports that no distance ever fits.
        /// </remarks>
        static float DistanceThatHoldsTheDeck(Vector2 size, float fov, float pitch)
        {
            var probeGo = new GameObject("~camfit") { hideFlags = HideFlags.HideAndDontSave };

            try
            {
                var probe = probeGo.AddComponent<Camera>();
                probe.enabled = false;
                probe.fieldOfView = fov;
                probe.aspect = 16f / 9f;
                probe.transform.rotation = Quaternion.Euler(pitch, 0f, 0f);

                float hx = size.x * 0.5f;
                float hz = size.y * 0.5f;
                var corners = new[]
                {
                    new Vector3(-hx, 0f, -hz), new Vector3(hx, 0f, -hz),
                    new Vector3(-hx, 0f, hz), new Vector3(hx, 0f, hz),
                };

                bool Fits(float d)
                {
                    float rad = pitch * Mathf.Deg2Rad;
                    probe.transform.position =
                        new Vector3(0f, d * Mathf.Sin(rad), -d * Mathf.Cos(rad));

                    foreach (Vector3 corner in corners)
                    {
                        Vector3 v = probe.WorldToViewportPoint(corner);
                        if (v.z <= 0.01f || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) return false;
                    }

                    return true;
                }

                float lo = 1f, hi = 200f;
                for (int i = 0; i < 50; i++)
                {
                    float mid = (lo + hi) * 0.5f;
                    if (Fits(mid)) hi = mid; else lo = mid;
                }

                return hi;
            }
            finally
            {
                Object.DestroyImmediate(probeGo);
            }
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

        static IEnumerator WaitUnscaled(float seconds)
        {
            // Unscaled, because the KO slow-mo is running and the card is timed against real time.
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until) yield return null;
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
