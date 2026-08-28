using System.Collections;
using Core.Events;
using Deadball.Ball;
using Deadball.Config;
using Deadball.Events;
using Deadball.Fighters;
using Deadball.Input;
using Deadball.Match;
using Deadball.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Deadball.Tests
{
    /// <summary>
    /// Round and match flow: knockout, the round card, the tally, and the comeback handicap.
    /// </summary>
    /// <remarks>
    /// These run the real <see cref="RoundManager"/> and <see cref="MatchManager"/> against a roster
    /// the test fills in place of the join screen, which is the seam Solo mode will use on Day 2 to
    /// hand the director one human and one AI.
    /// </remarks>
    public class DeadballRoundFlowTests
    {
        const string ScenePath = "Assets/_Deadball/Scenes/Arena_Greybox.unity";
        const string FighterPrefabPath = "Assets/_Deadball/Prefabs/Fighter.prefab";

        RoundManager _rounds;
        MatchManager _match;
        MatchConfig _config;
        ArenaReferences _arena;
        TestRoster _roster;
        Fighter _p1;
        Fighter _p2;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("Arena_Greybox", LoadSceneMode.Single);
#endif
            yield return null;

            _rounds = Object.FindFirstObjectByType<RoundManager>();
            _match = Object.FindFirstObjectByType<MatchManager>();
            _arena = Object.FindFirstObjectByType<ArenaReferences>();

            Assert.That(_rounds, Is.Not.Null, "Greybox scene has no round manager.");

            _roster = new GameObject("TestRoster").AddComponent<TestRoster>();
            _match.Roster = _roster;

            // The device-driven join flow has nothing to join in batch, and hitstop would stop the
            // clock the round timer runs on.
            // The arena now carries a mode bootstrap that reads the shared MatchSettings asset.
            // Left alone it activates the solo roster, whose spawn coroutine lands mid-test and
            // drops a house runner into a fixture that already built its own. Whatever mode a
            // developer last left the project in must not change what these tests measure.
            Strip<Deadball.Match.MatchModeBootstrap>();
            Strip<Deadball.AI.SoloRoster>();
            Strip<FighterJoinManager>();
            Strip<PlayerInputManager>();
            Strip<HitstopService>();
            StripFeedbacks();
            ClearSpawnedFighters();

            _config = Object.FindFirstObjectByType<BallController>().Config;
            Time.timeScale = 1f;

            _p1 = SpawnFighter(0);
            _p2 = SpawnFighter(1);
            _roster.Add(_p1);
            _roster.Add(_p2);

            yield return null;
        }

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [UnityTest]
        public IEnumerator StartingAMatch_PlacesFightersAtOppositeCornersBehindARoundCard()
        {
            using var starting = new Recorder<RoundStarting>();

            _match.StartMatch();
            yield return null;

            Assert.That(starting.Count, Is.EqualTo(1), "Round one must announce itself.");
            Assert.That(_rounds.IsRoundActive, Is.False, "Control is withheld during the round card.");

            float separation = Vector3.Distance(_p1.transform.position, _p2.transform.position);
            Assert.That(separation, Is.GreaterThan(_config.ArenaSize * 0.5f),
                "Fighters spawn at opposite corners (10).");

            yield return WaitUntil(() => _rounds.IsRoundActive, 8f, "the round never started");
        }

        [UnityTest]
        public IEnumerator KnockingOutAFighter_AwardsTheRoundToTheOther()
        {
            using var ended = new Recorder<RoundEnded>();

            yield return StartRoundAndWaitForControl();

            _p2.Knocks.TakeKnock(_config.KnocksToKo, Vector3.forward, 1f);
            yield return null;

            Assert.That(ended.Count, Is.EqualTo(1));
            Assert.That(_match.RoundWins(0), Is.EqualTo(1), "The survivor takes the round.");
            Assert.That(_rounds.IsRoundActive, Is.False);
        }

        [UnityTest]
        public IEnumerator TakingTwoRounds_EndsTheMatch()
        {
            using var matchEnded = new Recorder<MatchEnded>();

            for (int round = 0; round < _config.RoundWinsToTakeMatch; round++)
            {
                yield return StartRoundAndWaitForControl(round == 0);
                _p2.Knocks.TakeKnock(_config.KnocksToKo, Vector3.forward, 1f);
                yield return null;
            }

            Assert.That(_match.RoundWins(0), Is.EqualTo(_config.RoundWinsToTakeMatch));
            Assert.That(matchEnded.Count, Is.EqualTo(1), "Best of three ends at two round wins (10).");
            Assert.That(_match.WinnerSlot, Is.EqualTo(0));
            Assert.That(_match.IsMatchRunning, Is.False);
        }

        [UnityTest]
        public IEnumerator TheRoundLoser_SpawnsCloserToTheBall()
        {
            yield return StartRoundAndWaitForControl();

            _p2.Knocks.TakeKnock(_config.KnocksToKo, Vector3.forward, 1f);
            yield return null;

            _arena.GetSpawn(1, handicapped: false, out Vector3 normalSpawn, out _);
            yield return WaitUntil(() => !_rounds.IsRoundActive && _rounds.RoundNumber == 2, 8f,
                "round two never began");
            yield return null;

            float normalDistance = Vector3.Distance(normalSpawn, _arena.Centre);
            float actualDistance = Vector3.Distance(_p2.transform.position, _arena.Centre);

            Assert.That(actualDistance, Is.LessThan(normalDistance - 0.1f),
                "The fighter who lost the previous round starts closer to the centre ball (10).");
            Assert.That(normalDistance - actualDistance, Is.EqualTo(_config.ComebackHandicap).Within(0.2f));
        }

        [UnityTest]
        public IEnumerator BallReturnsToTheCentre_AtTheStartOfEveryRound()
        {
            var ball = Object.FindFirstObjectByType<BallController>();

            yield return StartRoundAndWaitForControl();

            Assert.That(ball.State, Is.EqualTo(BallState.Loose));

            Vector3 flat = ball.transform.position - _arena.Centre;
            flat.y = 0f;
            Assert.That(flat.magnitude, Is.LessThan(0.5f), "The ball spawns dead centre (10).");
        }

        // ---------------------------------------------------------------- helpers

        IEnumerator StartRoundAndWaitForControl(bool startMatch = true)
        {
            if (startMatch) _match.StartMatch();

            yield return WaitUntil(() => _rounds.IsRoundActive, 10f, "the round never became active");
        }

        Fighter SpawnFighter(int slot)
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(FighterPrefabPath);
#else
            GameObject prefab = null;
#endif
            GameObject instance = Object.Instantiate(prefab);
            instance.name = $"TestFighter_{slot}";

            Object.DestroyImmediate(instance.GetComponent<PlayerInputProvider>());
            Object.DestroyImmediate(instance.GetComponent<PlayerInput>());

            var fighter = instance.GetComponent<Fighter>();
            fighter.Bind(slot, new ScriptedInput());
            return fighter;
        }


        /// <summary>
        /// Removes fighters the scene's own join flow may have spawned.
        /// </summary>
        /// <remarks>
        /// When these run inside the editor a real keyboard exists, so the keyboard-split fallback
        /// claims both slots at scene load. The test needs the arena empty before it installs its
        /// own scripted fighters, or two fighters end up sharing each slot.
        /// </remarks>
        static void ClearSpawnedFighters()
        {
            foreach (Fighter fighter in Object.FindObjectsByType<Fighter>(FindObjectsSortMode.None))
                Object.DestroyImmediate(fighter.gameObject);
        }


        /// <summary>
        /// Removes the Feel players and the time manager before a test runs.
        /// </summary>
        /// <remarks>
        /// Freeze-frame and timescale feedbacks stop the clock, and every step-based wait in these
        /// tests advances on that clock - so left in, they turn assertions into timeouts. Stripped by
        /// name rather than by type because Feel ships no assembly definition, which puts the
        /// feedback bridge in Assembly-CSharp where this assembly cannot see it.
        /// </remarks>
        static void StripFeedbacks()
        {
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go != null && (go.name == "Feedbacks" || go.name == "MMTimeManager"))
                    Object.DestroyImmediate(go);
            }

            Time.timeScale = 1f;
        }

        static void Strip<T>() where T : Component
        {
            foreach (T component in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
                Object.DestroyImmediate(component);
        }

        static IEnumerator WaitUntil(System.Func<bool> condition, float timeoutSeconds, string message)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds && !condition())
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.That(condition(), Is.True, $"Timed out after {timeoutSeconds}s: {message}.");
        }

        class Recorder<T> : System.IDisposable where T : IEvent
        {
            readonly EventBinding<T> _binding;

            public int Count { get; private set; }

            public Recorder()
            {
                _binding = new EventBinding<T>(() => Count++);
                EventBus<T>.Register(_binding);
            }

            public void Dispose() => EventBus<T>.Deregister(_binding);
        }
    }
}
