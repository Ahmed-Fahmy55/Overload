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

            // The arena's mode bootstrap reads the shared MatchSettings asset and can activate the
            // solo roster, whose spawn coroutine lands mid-test and adds a house runner this fixture
            // never asked for. Tests must not depend on the mode a developer last left set.
            StripSceneDriver<Deadball.Match.MatchModeBootstrap>();
            StripSceneDriver<Deadball.AI.SoloRoster>();
            StripSceneDriver<FighterJoinManager>();
            StripSceneDriver<PlayerInputManager>();
            StripSceneDriver<MatchManager>();
            StripSceneDriver<RoundManager>();
            StripSceneDriver<HitstopService>();
            StripFeedbacks();
            ClearSpawnedFighters();
            KeepOneCore();

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
            using var clamps = new ClampRecorder();

            _heat.Add(40f);
            float before = _heat.Heat;

            yield return GiveCoreTo(_p1);
            yield return ChargeFor(_input1, _config.MaxChargeTime * 0.5f);
            _input1.ThrowHeld = false;

            yield return WaitUntil(() => flashes.Count > 0, 120, "the alarm never fired");

            // Pressing earlier opens the window sooner, so the core arrives in the LATE remainder -
            // the panic press is the mercy tier.
            //
            // Aimed at the middle of the LATE band rather than at a fixed lead. A whole window
            // ahead lands on the closing edge, and the frame it takes to consume the press pushes
            // it past: the window has already shut, the tier reads None, and the failure looks
            // like a clamp bug rather than a stopwatch that overshot. Half a window lands inside
            // PERFECT instead. The midpoint is the only lead that stays in LATE however the window
            // and the perfect band are tuned - at 0.20s/0.12s it leaves 0.04s of slack either way.
            float lateMidpoint = _config.PerfectClampBand
                + (_config.CatchWindow - _config.PerfectClampBand) * 0.5f;
            yield return PressClampAtArrivalIn(_input2, _p2, lateMidpoint);

            // Waiting for the band rather than sleeping towards it. The clamp's own resolution
            // reports its tier, so there is no window to sample and no race to lose. Polling
            // ClampTier meant catching an 0.08s slice between fixed steps.
            yield return WaitUntil(() => clamps.Count > 0, 90, "the clamp never resolved");

            Assert.That(clamps.LastTier, Is.EqualTo(ClampTier.Late),
                $"Past the perfect band it is LATE (window={_config.CatchWindow}s, "
                + $"band={_config.PerfectClampBand}s).");

            // No second assert on _p2.ClampTier: that property is live window state, and the clamp
            // that just resolved is what closed the window. The event carries the tier the clamp
            // was actually judged at, which is the thing under test.

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

            // This test is about what a critical core does on contact. The hold fuse would also be
            // burning during the wind-up and could KO the thrower first, so it is taken out of the
            // way - otherwise the two mechanics race and the assertion measures whichever won.
            var fuse = Object.FindFirstObjectByType<CoreFuse>();
            if (fuse != null) fuse.enabled = false;

            // Topped right up rather than nudged one point over the line. The core is loose here,
            // so heat is bleeding at 25/s and a single frame costs more than a point - a margin
            // that thin dropped back under the threshold before the next line could read it.
            _heat.Add(_config.MaxHeat);
            yield return null;

            Assert.That(_heat.IsCritical, Is.True,
                $"heat={_heat.Heat} critical at {_config.CriticalHeat} max={_config.MaxHeat} "
                + $"decay={_config.HeatDecayPerSecond}/s cores={Deadball.Ball.CoreRegistry.Cores.Count} "
                + $"coreState={_core.State}");
            Assert.That(_core.IsCritical, Is.True, "The core carries the critical flag itself (9).");

            // A soft throw that would normally cost one of two knocks.
            yield return GiveCoreTo(_p1);
            yield return ChargeFor(_input1, _config.MaxChargeTime * 0.25f);
            _input1.ThrowHeld = false;

            // Specifically the target. Waiting for "someone was knocked out" also passes when the
            // thrower eats their own ricochet, and then the real assert fails for a reason the
            // message does not explain.
            yield return WaitUntil(() => _p2.Knocks.IsOut, 200,
                "the critical core never took P2 out");

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

        [UnityTest]
        public IEnumerator HoardingTheCore_DetonatesItOnTheHolder()
        {
            // The stall this closes: whoever is ahead used to be able to pick the core up and run
            // out the round, because nobody can take it off them and nobody can hurt them while
            // they hold it. Possession is a timer now, not a shelter.
            var fuse = Object.FindFirstObjectByType<CoreFuse>();
            Assert.That(fuse, Is.Not.Null, "the core should carry a fuse");

            yield return GiveCoreTo(_p1);

            Assert.That(fuse.IsArmed, Is.True, "picking the core up arms the fuse.");
            Assert.That(_p1.IsInPlay, Is.True, "precondition: still standing");

            // WaitUntil here counts fixed steps, so the budget is converted rather than guessed.
            float budget = _config.HoldFuseFor(0f) + 1.5f;
            int steps = Mathf.CeilToInt(budget / Time.fixedDeltaTime);
            yield return WaitUntil(() => !_p1.IsInPlay, steps,
                $"held the core for {budget:0.0}s and nothing happened");

            Assert.That(_core.HolderSlot, Is.Not.EqualTo(_p1.Slot),
                "A detonated holder must not still be carrying the core.");
        }

        [UnityTest]
        public IEnumerator ThrowingTheCore_ResetsTheFuseForTheNextCarrier()
        {
            var fuse = Object.FindFirstObjectByType<CoreFuse>();

            yield return GiveCoreTo(_p1);
            yield return Seconds(_config.HoldFuseFor(0f) * 0.6f);

            Assert.That(fuse.Remaining01, Is.LessThan(0.75f), "precondition: the fuse has burned down");

            // Let it go, and the next carrier should start from a full fuse rather than inheriting
            // whatever the last one left on the clock.
            _core.Throw(Vector3.forward, 0.4f);
            yield return null;

            Assert.That(fuse.IsArmed, Is.False, "a thrown core is not burning anyone's fuse");

            yield return GiveCoreTo(_p2);

            Assert.That(fuse.IsArmed, Is.True);
            Assert.That(fuse.Remaining01, Is.GreaterThan(0.9f),
                "The new carrier gets a fresh fuse.");
            Assert.That(_p1.IsInPlay, Is.True, "Letting go in time has to actually save you.");
        }

        [UnityTest]
        public IEnumerator AHotCoreBurnsItsFuseFasterThanAColdOne()
        {
            // The fuse scales with heat rather than switching at CRITICAL, so a long rally squeezes
            // the holder before the threshold is ever crossed.
            float cold = _config.HoldFuseFor(0f);
            float hot = _config.HoldFuseFor(1f);

            Assert.That(hot, Is.LessThan(cold),
                "A critical core has to give the holder less time, not the same.");
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

        /// <summary>Keeps the tier the game resolved, so a test never has to guess it.</summary>
        // Each test here follows one named core from the throw to the clamp. The arena spawns as
        // many as the shared settings asset asks for, and a spare sitting on the deck is a second
        // thing that can reach a fighter - it lands its own contact and the fixture reads the
        // wrong one.
        static void KeepOneCore()
        {
            StripSceneDriver<Deadball.Ball.CoreSpawner>();

            bool kept = false;
            foreach (BallController core in Object.FindObjectsByType<BallController>(
                FindObjectsSortMode.None))
            {
                if (!kept) { kept = true; continue; }
                Object.DestroyImmediate(core.gameObject);
            }
        }

        class ClampRecorder : System.IDisposable
        {
            readonly EventBinding<BallCaught> _binding;

            public int Count { get; private set; }
            public ClampTier LastTier { get; private set; } = ClampTier.None;

            public ClampRecorder()
            {
                _binding = new EventBinding<BallCaught>(evt =>
                {
                    Count++;
                    LastTier = evt.Tier;
                });
                EventBus<BallCaught>.Register(_binding);
            }

            public void Dispose() => EventBus<BallCaught>.Deregister(_binding);
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
