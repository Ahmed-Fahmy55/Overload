using System.Collections;
using Core.Events;
using Deadball.AI;
using Deadball.Ball;
using Deadball.Events;
using Deadball.Config;
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
    /// The house runner's state machine and difficulty tiers (OVERLOAD GDD section 13).
    /// </summary>
    /// <remarks>
    /// The AI is tested through exactly the same seam a human uses. Nothing here reaches past
    /// <see cref="IFighterInput"/>, which is also the proof of 13.1's strongest claim: the bot cannot
    /// cheat, because there is no channel through which it could.
    /// </remarks>
    public class HouseRunnerTests
    {
        const string ScenePath = "Assets/_Deadball/Scenes/Arena_Greybox.unity";
        const string PrefabPath = "Assets/_Deadball/Prefabs/Fighter.prefab";
        const string GhostPath = "Assets/_Deadball/Data/AI_Ghost.asset";
        const string RookiePath = "Assets/_Deadball/Data/AI_Rookie.asset";

        BallController _core;
        MatchConfig _config;
        Fighter _human;
        Fighter _house;
        ScriptedInput _humanInput;
        AiInputSource _brain;

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

            // The arena now carries a mode bootstrap that reads the shared MatchSettings asset.
            // Left alone it activates the solo roster, whose spawn coroutine lands mid-test and
            // drops a house runner into a fixture that already built its own. Whatever mode a
            // developer last left the project in must not change what these tests measure.
            Strip<Deadball.Match.MatchModeBootstrap>();
            Strip<Deadball.AI.SoloRoster>();
            Strip<FighterJoinManager>();
            Strip<PlayerInputManager>();
            Strip<MatchManager>();
            Strip<RoundManager>();
            Strip<HitstopService>();
            StripFeedbacks();
            ClearFighters();

            Time.timeScale = 1f;

            _core = Object.FindFirstObjectByType<BallController>();
            _config = _core.Config;

            (_human, _humanInput) = SpawnHuman(0, new Vector3(-5f, 0f, 0f));
            (_house, _brain) = SpawnHouseRunner(1, new Vector3(5f, 0f, 0f), GhostPath);

            _core.ResetForRound(new Vector3(0f, 0f, 9f));
            yield return null;
        }

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [UnityTest]
        public IEnumerator HuntsALooseCore()
        {
            _core.GoLoose(new Vector3(0f, 0f, 0f));

            yield return WaitUntil(() => _brain.State == AiState.Hunt, 2f, "never entered HUNT");

            float before = Vector3.Distance(_house.transform.position, _core.transform.position);
            yield return Seconds(0.6f);
            float after = Vector3.Distance(_house.transform.position, _core.transform.position);

            Assert.That(after, Is.LessThan(before - 0.5f), "HUNT must actually close on the core.");
        }

        [UnityTest]
        public IEnumerator AimsWhenHoldingTheCore()
        {
            yield return GiveCoreTo(_house);
            yield return WaitUntil(() => _brain.State == AiState.Aim, 2f, "never entered AIM");

            // It should wind up on its own and launch without any external prompt.
            yield return WaitUntil(() => _core.State == BallState.Flying, 6f, "never launched");

            Assert.That(_core.Charge01, Is.GreaterThan(0.2f),
                "GHOST charges near max before launching (13.3).");
        }

        [UnityTest]
        public IEnumerator EvadesWhenTheOpponentHoldsTheCore()
        {
            yield return GiveCoreTo(_human);
            yield return WaitUntil(() => _brain.State == AiState.Evade, 2f, "never entered EVADE");

            // "Never stand still" (13.2) - sample positions and require real movement.
            Vector3 start = _house.transform.position;
            float travelled = 0f;
            Vector3 previous = start;

            for (int i = 0; i < 60; i++)
            {
                yield return null;
                travelled += Vector3.Distance(_house.transform.position, previous);
                previous = _house.transform.position;
            }

            Assert.That(travelled, Is.GreaterThan(1f), "An evading runner must keep moving.");
        }

        [UnityTest]
        public IEnumerator ReactsToAnIncomingCore()
        {
            yield return GiveCoreTo(_human);

            // Face the house runner and launch at it.
            _humanInput.Move = new Vector2(1f, 0f);
            yield return Seconds(0.2f);
            _humanInput.ThrowHeld = true;
            yield return Seconds(_config.MaxChargeTime * 0.6f);
            _humanInput.ThrowHeld = false;

            yield return WaitUntil(() => _brain.State == AiState.React, 3f, "never entered REACT");
            Assert.Pass();
        }

        [UnityTest]
        public IEnumerator DoesNotReactInstantly()
        {
            // A frame-perfect bot feels like cheating even when it is fair (13.4). The state must
            // not flip in the same frame the world changes.
            yield return GiveCoreTo(_human);
            yield return WaitUntil(() => _brain.State == AiState.Evade, 2f, "never entered EVADE");

            _core.GoLoose(Vector3.zero);
            yield return null;

            Assert.That(_brain.State, Is.EqualTo(AiState.Evade),
                "The runner should still be acting on its previous decision one frame later.");

            yield return WaitUntil(() => _brain.State == AiState.Hunt, 1f, "never caught up to HUNT");
        }

        [UnityTest]
        public IEnumerator GhostClampsHarderThanRookie()
        {
            var ghost = Load(GhostPath);
            var rookie = Load(RookiePath);

            Assert.That(ghost.ClampChance, Is.GreaterThan(rookie.ClampChance),
                "One float separates the tiers (13.3).");

            // The tier also decides how well it clamps, not just how often: GHOST aims inside the
            // PERFECT band while ROOKIE's press lands in the LATE remainder.
            Assert.That(ghost.ClampTargetArrival, Is.LessThanOrEqualTo(_config.PerfectClampBand),
                "GHOST should be aiming for a PERFECT clamp.");
            Assert.That(rookie.ClampTargetArrival, Is.GreaterThan(_config.PerfectClampBand),
                "ROOKIE should mostly land the mercy tier.");

            Assert.That(ghost.AimErrorDegrees, Is.LessThan(rookie.AimErrorDegrees),
                "Aim error shrinks with difficulty (13.4).");

            // Heat awareness: every tier gets more cautious at CRITICAL (13.5).
            Assert.That(ghost.ClampChanceFor(true), Is.LessThan(ghost.ClampChanceFor(false)));
            yield return null;
        }

        [UnityTest]
        public IEnumerator HuntsAroundAnObstacleInsteadOfStalling()
        {
            // A slab straight across the line between the runner and the core. Walking at the core
            // now means walking into this, and a runner with no steering simply leans on it.
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "TestObstacle";
            wall.transform.position = new Vector3(2.5f, 1f, 0f);
            wall.transform.localScale = new Vector3(0.6f, 2f, 5f);

            try
            {
                _core.GoLoose(new Vector3(-1f, 0f, 0f));
                yield return WaitUntil(() => _brain.State == AiState.Hunt, 2f, "never entered HUNT");

                float before = Vector3.Distance(_house.transform.position, _core.transform.position);
                yield return Seconds(2.5f);
                float after = Vector3.Distance(_house.transform.position, _core.transform.position);

                Assert.That(after, Is.LessThan(before - 1f),
                    $"The runner has to get around the slab, not stall against it "
                    + $"(closed from {before:0.00}m to {after:0.00}m).");
            }
            finally
            {
                Object.DestroyImmediate(wall);
            }
        }

        [UnityTest]
        public IEnumerator ThrowsEvenWhenTheOpponentSimplyRunsAway()
        {
            // A holder moves at 80% speed, so it can never close on someone sprinting away. Before
            // the chase cap the runner would follow forever and never let go of the core.
            var profile = Load(GhostPath);
            float rangeAtThrow = -1f;

            var binding = new EventBinding<BallThrown>(() =>
            {
                Vector3 gap = _human.transform.position - _house.transform.position;
                gap.y = 0f;
                if (rangeAtThrow < 0f) rangeAtThrow = gap.magnitude;
            });
            EventBus<BallThrown>.Register(binding);

            try
            {
                // Far enough apart that the runner cannot already be inside its throwing range.
                // Positioning happens first: PrepareForRound drops whatever the runner is holding,
                // so handing over the core before this would simply throw it on the floor.
                _house.PrepareForRound(new Vector3(-13f, 0f, 0f), Quaternion.LookRotation(Vector3.right));
                _human.PrepareForRound(new Vector3(13f, 0f, 0f), Quaternion.LookRotation(Vector3.right));
                _house.SetControlEnabled(true);
                _human.SetControlEnabled(true);
                yield return null;

                yield return GiveCoreTo(_house);

                // Keep the human running for the far wall so the gap only ever grows.
                yield return null;

                _humanInput.Move = new Vector2(1f, 0f);

                // Give up chasing, then still wind up: the throw only fires on release, so the
                // budget has to cover the cap plus a full charge.
                float budget = profile.MaxCloseSeconds + _config.MaxChargeTime + 1.0f;
                yield return WaitUntil(() => rangeAtThrow >= 0f, budget,
                    $"the house runner never threw - it chased for {budget:0.0}s");

                // Strictly outside the range it would normally close to, so this can only pass if
                // the runner gave up chasing and committed - not if it simply got there.
                Assert.That(rangeAtThrow, Is.GreaterThan(profile.PreferredRange * 1.15f),
                    "It should have committed to a throw from out of range rather than closing "
                    + "first, which against a fleeing opponent it can never do.");
            }
            finally
            {
                EventBus<BallThrown>.Deregister(binding);
                _humanInput.Move = Vector2.zero;
            }
        }

        [UnityTest]
        public IEnumerator RoutesAroundAPropToReachTheCore()
        {
            // Reactive steering stalls here: with a coolant tank directly between runner and core,
            // every heading in the probe fan is blocked, so it pushed into the prop forever. The
            // NavMesh path query is what makes the detour possible.
            Collider prop = FindProp();
            Assert.That(prop, Is.Not.Null, "The greybox deck should have props to route around.");

            Vector3 centre = prop.bounds.center;
            centre.y = 0f;
            float reach = Mathf.Max(prop.bounds.extents.x, prop.bounds.extents.z) + 2.5f;

            // Runner on one side, core directly opposite, prop squarely in between.
            _house.PrepareForRound(centre + Vector3.forward * reach, Quaternion.LookRotation(Vector3.back));
            _house.SetControlEnabled(true);
            yield return null;

            _core.GoLoose(centre - Vector3.forward * reach);
            yield return null;

            float start = FlatDistance(_house.transform.position, _core.transform.position);

            yield return WaitUntil(
                () => _core.HolderSlot == _house.Slot
                    || FlatDistance(_house.transform.position, _core.transform.position) < 1.5f,
                8f,
                "the runner never got around the prop to the core");

            float end = FlatDistance(_house.transform.position, _core.transform.position);
            Assert.That(end, Is.LessThan(start - 1f), "It has to actually close, not just wander.");
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        static Collider FindProp()
        {
            foreach (Collider col in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (col.isTrigger) continue;
                if (!col.name.StartsWith("Prop_") && !col.name.StartsWith("Pillar_")) continue;
                return col;
            }
            return null;
        }

        // ---------------------------------------------------------------- helpers

        static AiProfile Load(string path)
        {
#if UNITY_EDITOR
            var p = UnityEditor.AssetDatabase.LoadAssetAtPath<AiProfile>(path);
#else
            AiProfile p = null;
#endif
            Assert.That(p, Is.Not.Null, $"missing AI profile at {path}");
            return p;
        }

        static GameObject Prefab()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
#else
            return null;
#endif
        }

        (Fighter, ScriptedInput) SpawnHuman(int slot, Vector3 position)
        {
            GameObject go = Object.Instantiate(Prefab(), position, Quaternion.LookRotation(Vector3.right));
            go.name = "Runner_Human";
            Object.DestroyImmediate(go.GetComponent<PlayerInputProvider>());
            Object.DestroyImmediate(go.GetComponent<PlayerInput>());

            var fighter = go.GetComponent<Fighter>();
            var input = new ScriptedInput();
            fighter.Bind(slot, input);
            fighter.SetControlEnabled(true);
            return (fighter, input);
        }

        (Fighter, AiInputSource) SpawnHouseRunner(int slot, Vector3 position, string profilePath)
        {
            GameObject go = Object.Instantiate(Prefab(), position, Quaternion.LookRotation(Vector3.left));
            go.name = "Runner_House";
            Object.DestroyImmediate(go.GetComponent<PlayerInputProvider>());
            Object.DestroyImmediate(go.GetComponent<PlayerInput>());

            var fighter = go.GetComponent<Fighter>();
            var brain = go.AddComponent<AiInputSource>();
            brain.Configure(Load(profilePath), fighter, _core, Object.FindFirstObjectByType<RallyHeat>());

            fighter.Bind(slot, brain);
            fighter.SetControlEnabled(true);
            return (fighter, brain);
        }

        IEnumerator GiveCoreTo(Fighter fighter)
        {
            _core.ResetForRound(fighter.transform.position);
            yield return WaitUntil(() => _core.HolderSlot == fighter.Slot, 1f, "pickup never happened");
        }

        static IEnumerator Seconds(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
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
            foreach (T c in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
                Object.DestroyImmediate(c);
        }

        static void ClearFighters()
        {
            foreach (Fighter f in Object.FindObjectsByType<Fighter>(FindObjectsSortMode.None))
                Object.DestroyImmediate(f.gameObject);
        }
    }
}
