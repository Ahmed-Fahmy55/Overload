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
    /// The Day 1 loop, exercised headlessly: grab, charge, throw, catch, lockout, knock, KO.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The design's Day 1 gate is that the core mechanic has to feel right before any art exists.
    /// Feel is not testable, but the rules underneath it are, and those are the things that quietly
    /// break while you are busy tuning three numbers at 2am.
    /// </para>
    /// <para>
    /// Every test drives real fighters through the real ball in the real generated arena. The only
    /// substitution is the input source, which is the seam the AI will use on Day 2.
    /// </para>
    /// </remarks>
    public class DeadballCoreLoopTests
    {
        const string ScenePath = "Assets/_Deadball/Scenes/Arena_Greybox.unity";
        const string FighterPrefabPath = "Assets/_Deadball/Prefabs/Fighter.prefab";

        BallController _ball;
        MatchConfig _config;
        ArenaReferences _arena;
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

            // The join flow and the match director would fight the test for control of the fighters,
            // and hitstop would freeze the clock the assertions are waiting on.
            StripSceneDriver<FighterJoinManager>();
            StripSceneDriver<PlayerInputManager>();
            StripSceneDriver<MatchManager>();
            StripSceneDriver<RoundManager>();
            StripSceneDriver<HitstopService>();
            StripFeedbacks();
            ClearSpawnedFighters();

            Time.timeScale = 1f;

            _ball = Object.FindFirstObjectByType<BallController>();
            _arena = Object.FindFirstObjectByType<ArenaReferences>();
            Assert.That(_ball, Is.Not.Null, "Greybox scene has no ball. Run Deadball > Setup first.");

            _config = _ball.Config;

            (_p1, _input1) = SpawnFighter(0, new Vector3(-4f, 0f, 0f), Quaternion.LookRotation(Vector3.right));
            (_p2, _input2) = SpawnFighter(1, new Vector3(4f, 0f, 0f), Quaternion.LookRotation(Vector3.left));

            _ball.ResetForRound(new Vector3(0f, 0f, 8f));
            yield return null;
        }

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [UnityTest]
        public IEnumerator LooseBall_IsGrabbedByWalkingOverIt()
        {
            _ball.ResetForRound(_p1.transform.position);
            yield return Steps(4);

            Assert.That(_ball.State, Is.EqualTo(BallState.Held));
            Assert.That(_ball.HolderSlot, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator HoldingTheBall_SlowsTheHolder()
        {
            yield return GiveBallTo(_p1);

            Assert.That(_p1.Motor.Slowed, Is.True,
                "Possession has to cost speed, or the holder simply kites forever.");
        }

        [UnityTest]
        public IEnumerator HeldBall_StaysInTheHandWhileTheFighterMoves()
        {
            yield return GiveBallTo(_p1);

            _input1.Move = new Vector2(1f, 0.35f);

            // Sampled once per rendered frame, which is what the player actually sees - a held ball
            // that only lines up on physics steps still reads as the ball trailing the fighter.
            float worstError = 0f;
            for (int i = 0; i < 90; i++)
            {
                yield return null;
                worstError = Mathf.Max(worstError,
                    Vector3.Distance(_ball.transform.position, _p1.HandAnchor.position));
            }

            Assert.That(worstError, Is.LessThan(0.05f),
                $"The held ball drifted {worstError:F3}m from the hand anchor while carrying.");
        }

        [UnityTest]
        public IEnumerator ChargingRootsTheThrower_AndReleaseScalesBallSpeed()
        {
            yield return GiveBallTo(_p1);

            _input1.Move = Vector2.right;
            _input1.ThrowHeld = true;
            yield return Frames(3);

            Assert.That(_p1.Motor.Rooted, Is.True, "Charging must root the fighter (7.3).");

            yield return ChargeFor(_input1, _config.MaxChargeTime * 1.2f);
            float charge = _p1.Thrower.Charge01;
            Assert.That(charge, Is.EqualTo(1f).Within(0.01f));

            _input1.ThrowHeld = false;
            yield return Frames(2);

            Assert.That(_ball.State, Is.EqualTo(BallState.Flying));

            float speed = _ball.GetComponent<Rigidbody>().linearVelocity.magnitude;
            Assert.That(speed, Is.EqualTo(_config.MaxThrowSpeed).Within(1.5f),
                "A max charge has to launch at the top of the speed range.");
        }

        [UnityTest]
        public IEnumerator DodgeCancelsCharge_ButKeepsTheBall()
        {
            yield return GiveBallTo(_p1);

            yield return ChargeFor(_input1, _config.MaxChargeTime * 0.6f);
            Assert.That(_p1.Thrower.Charge01, Is.GreaterThan(0.2f));

            _input1.PressDodge();
            yield return Frames(2);

            Assert.That(_p1.Thrower.Charge01, Is.EqualTo(0f), "Dodge drops the charge to zero (7.3).");
            Assert.That(_p1.Thrower.HasBall, Is.True, "Dodge must not drop the ball.");
        }

        [UnityTest]
        public IEnumerator CatchOnTheFlashCue_FlipsPossessionAndPreservesCharge()
        {
            using var flashes = new FlashRecorder();
            using var caught = new CaughtRecorder();

            yield return GiveBallTo(_p1);
            yield return ChargeFor(_input1, _config.MaxChargeTime * 1.2f);
            _input1.ThrowHeld = false;

            // Driving off the cue rather than a fixed frame count keeps this honest: if the cue
            // stops firing at the right moment, this fails instead of quietly passing. The delay
            // after it is required - see RallyHeatTests for why the alarm is not a "press now".
            yield return WaitUntil(() => flashes.Count > 0, 120, "the flash cue never fired");

            // Pressed relative to the core's real arrival, not to the cue - see RallyHeatTests.
            var rb = _ball.GetComponent<Rigidbody>();
            yield return WaitUntil(() =>
            {
                if (_ball.State != BallState.Flying) return false;
                Vector3 to = _p2.CenterPosition - _ball.transform.position;
                float distance = to.magnitude - _p2.CatchRadius;
                float closing = Vector3.Dot(rb.linearVelocity, to.normalized);
                return closing > 0.01f && distance / closing <= 0.08f;
            }, 250, "the ball never closed on the target");

            _input2.PressCatch();

            // One frame for Fighter.Update to consume the press and open the window.
            yield return null;
            Assert.That(_p2.IsCatchWindowActive, Is.True);

            yield return WaitUntil(() => _ball.State != BallState.Flying, 60, "the ball never arrived");

            Assert.That(_ball.HolderSlot, Is.EqualTo(1), "A catch flips possession.");
            Assert.That(_ball.Charge01, Is.EqualTo(1f).Within(0.05f),
                "The ball arrives pre-charged to the level it was thrown at (8.5).");
            Assert.That(caught.Count, Is.EqualTo(1));
            Assert.That(_p2.Knocks.KnocksRemaining, Is.EqualTo(_config.KnocksToKo),
                "A caught ball must not also land a knock.");
        }

        [UnityTest]
        public IEnumerator MaxChargeThrows_NeverLeaveTheBallInsideGeometry()
        {
            int arenaMask = 1 << DeadballLayers.ArenaLayer;
            float halfArena = _config.ArenaSize * 0.5f;

            // Straight at each prop, then into two corners. A max-charge ball covers more than its
            // own diameter per physics step, so these are the shots that punch into geometry.
            var shots = new[]
            {
                new Vector3(-3.6f, 0f, 1.8f),
                new Vector3(4.4f, 0f, -2.4f),
                new Vector3(0.6f, 0f, 5.4f)
            };

            foreach (Vector3 shot in shots)
            {
                _p1.Motor.Teleport(Vector3.zero, Quaternion.identity);
                yield return GiveBallTo(_p1);

                _ball.Throw(shot.normalized, 1f);
                yield return WaitUntil(() => _ball.State == BallState.Loose, 200,
                    $"the ball never resolved after a max-charge shot at {shot}");

                Vector3 rest = _ball.transform.position;

                Assert.That(Physics.CheckSphere(rest, 0.2f, arenaMask), Is.False,
                    $"Ball came to rest inside geometry at {rest} after a shot at {shot}.");

                Assert.That(Mathf.Abs(rest.x), Is.LessThan(halfArena),
                    $"Ball escaped the lot on X at {rest} after a shot at {shot}.");
                Assert.That(Mathf.Abs(rest.z), Is.LessThan(halfArena),
                    $"Ball escaped the lot on Z at {rest} after a shot at {shot}.");
            }
        }

        [UnityTest]
        public IEnumerator MissedCatch_StartsTheLockout()
        {
            _input2.PressCatch();
            yield return Frames(1);
            Assert.That(_p2.Catcher.IsWindowActive, Is.True);

            yield return Seconds(_config.CatchWindow + 0.05f);

            Assert.That(_p2.Catcher.IsWindowActive, Is.False);
            Assert.That(_p2.Catcher.IsLockedOut, Is.True,
                "A window that closes empty must cost a lockout, or mashing is free (8.4).");

            _input2.PressCatch();
            yield return Frames(1);
            Assert.That(_p2.Catcher.IsWindowActive, Is.False, "No catch may open during the lockout.");
        }

        [UnityTest]
        public IEnumerator UncaughtBall_CostsOneKnock()
        {
            yield return GiveBallTo(_p1);
            yield return ChargeFor(_input1, _config.MaxChargeTime * 0.3f);

            int before = _p2.Knocks.KnocksRemaining;
            _input1.ThrowHeld = false;

            yield return WaitUntil(() => _p2.Knocks.KnocksRemaining < before, 120, "the ball never landed");

            Assert.That(_p2.Knocks.KnocksRemaining, Is.EqualTo(before - 1));
            Assert.That(_p2.Knocks.IsOut, Is.False, "Two knocks put you out, not one (9).");
        }

        [UnityTest]
        public IEnumerator MaxChargeHit_KnocksOutInOne()
        {
            using var knockedOut = new KnockedOutRecorder();

            yield return GiveBallTo(_p1);
            yield return ChargeFor(_input1, _config.MaxChargeTime * 1.2f);

            _input1.ThrowHeld = false;
            yield return WaitUntil(() => knockedOut.Count > 0, 120, "the ball never landed");

            Assert.That(_p2.Knocks.IsOut, Is.True, "A max-charge throw is a one-hit knockout (9).");
            Assert.That(knockedOut.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DodgeOnTheFlashCue_LetsTheBallPass()
        {
            using var flashes = new FlashRecorder();

            yield return GiveBallTo(_p1);
            yield return ChargeFor(_input1, _config.MaxChargeTime * 0.3f);

            int before = _p2.Knocks.KnocksRemaining;
            _input1.ThrowHeld = false;

            // Dodging early is useless: the i-frames are shorter than the flight time, so the roll
            // has to be timed off the same cue the catch is. That is the point of the telegraph.
            yield return WaitUntil(() => flashes.Count > 0, 120, "the flash cue never fired");

            // Roll perpendicular to the incoming ball. The i-frames are shorter than the remaining
            // flight time on the design's own numbers, so what actually saves you is the three
            // metres of displacement - a dodge straight down the barrel still eats it.
            _input2.Move = Vector2.up;
            _input2.PressDodge();
            yield return Frames(2);
            Assert.That(_p2.Motor.IsInvulnerable, Is.True);

            // Well past the point the ball would have arrived. A dodged ball is not a dead ball -
            // it carries on into the walls and stays live, which is where the ricochets come from.
            yield return Steps(45);

            Assert.That(_p2.Knocks.KnocksRemaining, Is.EqualTo(before),
                "The dodge is the safe answer to an incoming ball (7.4).");
        }

        [UnityTest]
        public IEnumerator ThrowerIsImmuneToTheirOwnBall_Briefly()
        {
            yield return GiveBallTo(_p1);

            // Facing a wall at point-blank range: without the immunity the ball would rebound into
            // the thrower inside a couple of frames.
            _p1.transform.position = new Vector3(-9f, 0f, 0f);
            _input1.Move = Vector2.left;
            yield return Frames(4);

            yield return ChargeFor(_input1, _config.MaxChargeTime * 0.2f);
            _input1.ThrowHeld = false;
            yield return Steps(4);

            Assert.That(_p1.Knocks.KnocksRemaining, Is.EqualTo(_config.KnocksToKo),
                "A ball cannot hit its thrower during the immunity window (6.4).");
        }

        [UnityTest]
        public IEnumerator FlyingBall_FiresTheFlashCueBeforeArrival()
        {
            using var flashes = new FlashRecorder();

            yield return GiveBallTo(_p1);
            yield return ChargeFor(_input1, _config.MaxChargeTime * 0.4f);

            _input1.ThrowHeld = false;
            yield return WaitUntil(() => flashes.Count > 0, 120,
                "layer 3 of the telegraph never fired, so the catch is a coin flip (8.2)");

            Assert.That(flashes.LastTargetSlot, Is.EqualTo(1));
            Assert.That(_ball.State, Is.EqualTo(BallState.Flying),
                "The cue has to arrive while the ball is still in the air.");
        }

        [UnityTest]
        public IEnumerator FullCharge_AnnouncesItselfExactlyOnce()
        {
            using var maxed = new ChargeMaxRecorder();

            yield return GiveBallTo(_p1);

            // Held well past full, so a per-frame threshold test would fire many times over.
            yield return ChargeFor(_input1, _config.MaxChargeTime * 1.8f);

            Assert.That(_p1.Thrower.Charge01, Is.EqualTo(1f).Within(0.001f), "precondition: fully charged");
            Assert.That(maxed.Count, Is.EqualTo(1),
                "Max charge is a crossing, not a state - holding at full must not retrigger the snap.");
            Assert.That(maxed.LastSlot, Is.EqualTo(0), "It must name the runner who charged it.");

            _input1.ThrowHeld = false;
        }

        [UnityTest]
        public IEnumerator ARebound_ReportsWhereTheFieldWasStruck()
        {
            using var bounces = new BounceRecorder();

            yield return GiveBallTo(_p1);

            // Aimed away from the opponent and into the containment field, so the first thing the
            // core meets is a wall rather than a runner.
            _input1.Move = new Vector2(-1f, 0f);
            yield return Seconds(0.25f);
            yield return ChargeFor(_input1, _config.MaxChargeTime * 1.2f);
            _input1.ThrowHeld = false;

            yield return WaitUntil(() => bounces.Count > 0, 400, "the core never struck the field");

            Assert.That(bounces.LastNormal.sqrMagnitude, Is.GreaterThan(0.5f),
                "The flare is placed with this normal, so it has to be a real direction.");
            Assert.That(Mathf.Abs(bounces.LastNormal.y), Is.LessThan(0.5f),
                "A floor contact is a dead ball, not a rebound - this must be a wall.");
            Assert.That(bounces.LastSpeed, Is.GreaterThan(0f),
                "Impact speed drives the bounce pitch, so zero would flatten every hit.");
        }

        // ---------------------------------------------------------------- helpers

        (Fighter, ScriptedInput) SpawnFighter(int slot, Vector3 position, Quaternion rotation)
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(FighterPrefabPath);
#else
            GameObject prefab = null;
#endif
            Assert.That(prefab, Is.Not.Null, $"Fighter prefab missing at {FighterPrefabPath}.");

            GameObject instance = Object.Instantiate(prefab, position, rotation);
            instance.name = $"TestFighter_{slot}";

            // Device-driven input is replaced wholesale; PlayerInput has nothing to bind to here.
            Object.DestroyImmediate(instance.GetComponent<PlayerInputProvider>());
            Object.DestroyImmediate(instance.GetComponent<PlayerInput>());

            var fighter = instance.GetComponent<Fighter>();
            var input = new ScriptedInput();
            fighter.Bind(slot, input);
            fighter.SetControlEnabled(true);

            return (fighter, input);
        }

        IEnumerator GiveBallTo(Fighter fighter)
        {
            _ball.ResetForRound(fighter.transform.position);

            yield return WaitUntil(() => _ball.HolderSlot == fighter.Slot, 20,
                "the fighter standing on the ball never picked it up");
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
            for (int i = 0; i < count; i++)
                yield return null;
        }

        static IEnumerator Steps(int count)
        {
            for (int i = 0; i < count; i++)
                yield return new WaitForFixedUpdate();
        }

        /// <summary>Advances physics until <paramref name="condition"/> holds, or fails the test.</summary>
        static IEnumerator WaitUntil(System.Func<bool> condition, int maxSteps, string timeoutMessage)
        {
            for (int i = 0; i < maxSteps && !condition(); i++)
                yield return new WaitForFixedUpdate();

            Assert.That(condition(), Is.True, $"Timed out after {maxSteps} steps: {timeoutMessage}.");
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

        static void StripSceneDriver<T>() where T : Component
        {
            foreach (T component in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
                Object.DestroyImmediate(component);
        }

        // Small recorders keep the event-bus subscription noise out of the tests themselves.

        class CaughtRecorder : Recorder<BallCaught>
        {
            public CaughtRecorder() : base() { }
        }

        class KnockedOutRecorder : Recorder<FighterKnockedOut>
        {
            public KnockedOutRecorder() : base() { }
        }

        class ChargeMaxRecorder : Recorder<ChargeMaxed>
        {
            public int LastSlot { get; private set; } = -1;

            protected override void OnEvent(ChargeMaxed evt) => LastSlot = evt.Slot;
        }

        class BounceRecorder : Recorder<BallBounced>
        {
            public Vector3 LastNormal { get; private set; }
            public float LastSpeed { get; private set; }

            protected override void OnEvent(BallBounced evt)
            {
                LastNormal = evt.Normal;
                LastSpeed = evt.Speed;
            }
        }

        class FlashRecorder : Recorder<BallFlashCue>
        {
            public int LastTargetSlot { get; private set; } = -1;

            protected override void OnEvent(BallFlashCue evt) => LastTargetSlot = evt.TargetSlot;
        }

        class Recorder<T> : System.IDisposable where T : IEvent
        {
            readonly EventBinding<T> _binding;

            public int Count { get; private set; }

            protected Recorder()
            {
                _binding = new EventBinding<T>(Handle);
                EventBus<T>.Register(_binding);
            }

            public void Dispose() => EventBus<T>.Deregister(_binding);

            protected virtual void OnEvent(T evt) { }

            void Handle(T evt)
            {
                Count++;
                OnEvent(evt);
            }
        }
    }
}
