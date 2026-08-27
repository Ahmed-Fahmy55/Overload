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
    /// Rally Heat and the two-tier clamp (OVERLOAD GDD sections 8.2 and 16).
    /// </summary>
    /// <remarks>
    /// The hook only works if three things hold together: a perfect clamp heats the core, a late
    /// clamp cools it while handing over the core, and a critical core kills in one touch. Each of
    /// those is a number that can drift silently during a tuning pass, so each gets a test.
    /// </remarks>
    public class RallyHeatTests
    {
        const string ScenePath = "Assets/_Deadball/Scenes/Arena_Greybox.unity";
        const string FighterPrefabPath = "Assets/_Deadball/Prefabs/Fighter.prefab";

        BallController _core;
        MatchConfig _config;
        RallyHeat _heat;
        Fighter _p1;
        Fighter _p2;
        ScriptedInput _input1;
        ScriptedInput _input2;

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

            StripSceneDriver<FighterJoinManager>();
            StripSceneDriver<PlayerInputManager>();
            StripSceneDriver<MatchManager>();
            StripSceneDriver<RoundManager>();
            StripSceneDriver<HitstopService>();
            ClearSpawnedFighters();

            Time.timeScale = 1f;

            _core = Object.FindFirstObjectByType<BallController>();
            _heat = Object.FindFirstObjectByType<RallyHeat>();
            Assert.That(_core, Is.Not.Null, "no core in the scene");
            Assert.That(_heat, Is.Not.Null, "no RallyHeat in the scene - run the Day 2 wiring");

            _config = _core.Config;
            _heat.ResetHeat();

            (_p1, _input1) = SpawnFighter(0, new Vector3(-4f, 0f, 0f), Quaternion.LookRotation(Vector3.right));
            (_p2, _input2) = SpawnFighter(1, new Vector3(4f, 0f, 0f), Quaternion.LookRotation(Vector3.left));

            _core.ResetForRound(new Vector3(0f, 0f, 8f));
            yield return null;
        }

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [UnityTest]
        public IEnumerator PerfectClamp_HeatsTheCoreAndStunsTheThrower()
        {
            using var flashes = new FlashRecorder();

            yield return GiveCoreTo(_p1);
            yield return ChargeFor(_input1, _config.MaxChargeTime * 1.2f);
            _input1.ThrowHeld = false;

            yield return WaitUntil(() => flashes.Count > 0, 120, "the alarm never fired");

            // The alarm is a "get ready", not a "press now": with a 0.35s lead against a 0.30s
            // window, pressing on the beat expires the window before the core lands. PERFECT needs
            // the core to arrive inside the first 0.12s of the window (8.2).
            yield return PressClampAtArrivalIn(_input2, _p2, 0.08f);
            Assert.That(_p2.ClampTier, Is.EqualTo(ClampTier.Perfect));

            yield return WaitUntil(() => _core.State != BallState.Flying, 60, "the core never arrived");

            Assert.That(_core.HolderSlot, Is.EqualTo(1), "A perfect clamp takes possession.");
            Assert.That(_core.Charge01, Is.EqualTo(1f).Within(0.05f), "Charge is preserved (8.2).");
            Assert.That(_heat.Heat, Is.EqualTo(_config.HeatPerPerfectClamp).Within(0.01f));
            Assert.That(_p1.Motor.IsStunned, Is.True, "The thrower is staggered for 0.35s (8.2).");
        }

        [UnityTest]
        public IEnumerator LateClamp_StopsTheCoreButDropsItLooseAndAddsNoHeat()
        {
            using var flashes = new FlashRecorder();

            _heat.Add(40f);
            float before = _heat.Heat;

            yield return GiveCoreTo(_p1);
            yield return ChargeFor(_input1, _config.MaxChargeTime * 0.5f);
            _input1.ThrowHeld = false;

            yield return WaitUntil(() => flashes.Count > 0, 120, "the alarm never fired");

            // Pressing earlier opens the window sooner, so the core arrives in the LATE remainder -
            // the panic press is the mercy tier.
            yield return PressClampAtArrivalIn(_input2, _p2, 0.24f);
            yield return Seconds(_config.PerfectClampBand + 0.02f);

            Assert.That(_p2.ClampTier, Is.EqualTo(ClampTier.Late), "Past the perfect band it is LATE.");

            yield return WaitUntil(() => _core.State == BallState.Loose, 90, "the core never resolved");

            Assert.That(_core.HolderSlot, Is.EqualTo(-1), "A late clamp gives no possession (8.2).");
            Assert.That(_core.Charge01, Is.EqualTo(0f), "A late clamp keeps no charge.");
            Assert.That(_p1.Motor.IsStunned, Is.False, "A late clamp does not stun the thrower.");
            Assert.That(_heat.Heat, Is.LessThanOrEqualTo(before), "A late clamp must never add heat.");
        }

        [UnityTest]
        public IEnumerator HeatBleedsOffOnlyWhileTheCoreIsLoose()
        {
            // Heat is added only once the core is actually held. Adding it first would let it bleed
            // during the pickup frames, which is correct behaviour but not what this test measures.
            yield return GiveCoreTo(_p1);

            _heat.Add(60f);
            float start = _heat.Heat;

            yield return Seconds(0.5f);
            Assert.That(_heat.Heat, Is.EqualTo(start).Within(0.01f),
                "Heat must not decay while the core is held (16).");

            _core.GoLoose(Vector3.zero);
            yield return Seconds(0.5f);

            float expected = start - _config.HeatDecayPerSecond * 0.5f;
            Assert.That(_heat.Heat, Is.EqualTo(expected).Within(6f),
                "Loose on the deck, heat bleeds at the configured rate.");
        }

        [UnityTest]
        public IEnumerator CriticalCore_KillsInOneTouch()
        {
            using var knockedOut = new Recorder<FighterKnockedOut>();

            _heat.Add(_config.CriticalHeat + 1f);
            yield return null;

            Assert.That(_heat.IsCritical, Is.True);
            Assert.That(_core.IsCritical, Is.True, "The core carries the critical flag itself (9).");

            // A soft throw that would normally cost one of two knocks.
            yield return GiveCoreTo(_p1);
            yield return ChargeFor(_input1, _config.MaxChargeTime * 0.25f);
            _input1.ThrowHeld = false;

            yield return WaitUntil(() => knockedOut.Count > 0, 150, "the core never landed");

            Assert.That(_p2.Knocks.IsOut, Is.True,
                "At CRITICAL a single touch is a KO regardless of charge (9).");
        }

        [UnityTest]
        public IEnumerator HeatResetsBetweenRounds()
        {
            _heat.Add(50f);
            Assert.That(_heat.Heat, Is.GreaterThan(0f));

            EventBus<RoundStarting>.Raise(new RoundStarting(2, 0f));
            yield return null;

            Assert.That(_heat.Heat, Is.EqualTo(0f), "Each round starts cold.");
            Assert.That(_heat.IsCritical, Is.False);
        }


        /// <summary>
        /// Presses clamp once the core is within <paramref name="seconds"/> of arrival.
        /// </summary>
        /// <remarks>
        /// Measured from the core's real closing speed rather than timed off the alarm. Which tier a
        /// clamp lands in depends on where inside the window the core arrives, so a test that wants a
        /// specific tier has to press relative to arrival, not relative to the cue.
        /// </remarks>
        IEnumerator PressClampAtArrivalIn(ScriptedInput input, Fighter target, float seconds)
        {
            var rb = _core.GetComponent<Rigidbody>();

            yield return WaitUntil(() =>
            {
                if (_core.State != BallState.Flying) return false;

                Vector3 to = target.CenterPosition - _core.transform.position;
                float distance = to.magnitude - target.CatchRadius;
                float closing = Vector3.Dot(rb.linearVelocity, to.normalized);

                return closing > 0.01f && distance / closing <= seconds;
            }, 250, $"the core never closed to within {seconds}s of {target.name}");

            input.PressCatch();

            // One frame for Fighter.Update to consume the press and open the window.
            yield return null;
        }

        // ---------------------------------------------------------------- helpers

        (Fighter, ScriptedInput) SpawnFighter(int slot, Vector3 position, Quaternion rotation)
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(FighterPrefabPath);
#else
            GameObject prefab = null;
#endif
            GameObject instance = Object.Instantiate(prefab, position, rotation);
            instance.name = $"TestRunner_{slot}";

            Object.DestroyImmediate(instance.GetComponent<PlayerInputProvider>());
            Object.DestroyImmediate(instance.GetComponent<PlayerInput>());

            var fighter = instance.GetComponent<Fighter>();
            var input = new ScriptedInput();
            fighter.Bind(slot, input);
            fighter.SetControlEnabled(true);
            return (fighter, input);
        }

        IEnumerator GiveCoreTo(Fighter fighter)
        {
            _core.ResetForRound(fighter.transform.position);
            yield return WaitUntil(() => _core.HolderSlot == fighter.Slot, 20, "pickup never happened");
        }

        IEnumerator ChargeFor(ScriptedInput input, float seconds)
        {
            input.ThrowHeld = true;

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
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

        static IEnumerator WaitUntil(System.Func<bool> condition, int maxSteps, string message)
        {
            for (int i = 0; i < maxSteps && !condition(); i++)
                yield return new WaitForFixedUpdate();

            Assert.That(condition(), Is.True, $"Timed out after {maxSteps} steps: {message}.");
        }

        static void StripSceneDriver<T>() where T : Component
        {
            foreach (T c in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
                Object.DestroyImmediate(c);
        }

        static void ClearSpawnedFighters()
        {
            foreach (Fighter f in Object.FindObjectsByType<Fighter>(FindObjectsSortMode.None))
                Object.DestroyImmediate(f.gameObject);
        }

        class FlashRecorder : Recorder<BallFlashCue> { }

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
