using Core.Events;
using Deadball.Config;
using Deadball.Events;
using Sirenix.OdinInspector;
using UnityEngine;
using Zone8.ImprovedTimers;

namespace Deadball.Fighters
{
    /// <summary>
    /// The catch window and its lockout (GDD section 8).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The whole mechanic is three numbers and one rule: a tap opens a short window, a window that
    /// closes empty costs you a lockout, and a lockout means mashing is strictly worse than never
    /// pressing. That last property is what keeps the dodge relevant as the default answer, and it
    /// is why the lockout is called out in the design as the single most important value to tune.
    /// </para>
    /// <para>
    /// Note that nothing here consults facing. The design is explicit that a facing cone reads as a
    /// bug rather than as a skill check, so the catch is omnidirectional (8.6).
    /// </para>
    /// </remarks>
    public class FighterCatcher : MonoBehaviour
    {
        [Required, SerializeField] MatchConfig _config;

        [Title("Runtime"), ShowInInspector, ReadOnly]
        public bool IsWindowActive => Window is { IsRunning: true, IsFinished: false };

        [ShowInInspector, ReadOnly]
        public bool IsLockedOut => Lockout is { IsRunning: true, IsFinished: false };

        /// <summary>
        /// True while a LATE clamp bars this fighter from retaking the core (8.2).
        /// </summary>
        /// <remarks>
        /// LATE stops the core dead and drops it loose at the clamper's feet. Without a beat where
        /// they cannot take it, they just walk over it on the next frame and the mercy tier becomes
        /// a slightly worse PERFECT. This is what makes the drop a footrace anyone can win.
        /// </remarks>
        [ShowInInspector, ReadOnly]
        public bool IsFumbling => Fumble is { IsRunning: true, IsFinished: false };

        /// <summary>0..1 fill of the lockout, for a HUD or shader tell.</summary>
        public float LockoutProgress => IsLockedOut ? 1f - Lockout.Progress : 0f;

        /// <summary>
        /// How far the clamp has recovered, 0 just after a missed window to 1 when it is available.
        /// </summary>
        /// <remarks>
        /// The same shape as the motor's dodge readiness, so one HUD icon script drives both.
        /// </remarks>
        public float ClaimReady01 => IsLockedOut ? 1f - Lockout.Progress : 1f;

        /// <summary>Seconds left on the active catch window, for AI timing and debug readouts.</summary>
        public float WindowRemaining => IsWindowActive ? Window.CurrentTime : 0f;

        /// <summary>
        /// Which tier the window is in right now (8.2).
        /// </summary>
        /// <remarks>
        /// Measured from how much of the window is left rather than how much has elapsed, because the
        /// timer counts down - the PERFECT band is the top slice of the remaining time.
        /// </remarks>
        [ShowInInspector, ReadOnly]
        public ClampTier CurrentTier
        {
            get
            {
                if (!IsWindowActive) return ClampTier.None;

                float elapsed = _config.CatchWindow - Window.CurrentTime;
                return elapsed <= _config.PerfectClampBand ? ClampTier.Perfect : ClampTier.Late;
            }
        }

        int _slot;
        bool _enabled = true;
        bool _resolved;
        CountdownTimer _window;
        CountdownTimer _lockout;
        CountdownTimer _fumble;

        // Lazily built for the same reason as the motor's timers: the join callback can reach this
        // component before its Awake has run.
        CountdownTimer Window
        {
            get
            {
                if (_window != null) return _window;

                _window = new CountdownTimer(_config.CatchWindow);
                _window.OnTimerStop += OnWindowClosed;
                return _window;
            }
        }

        CountdownTimer Lockout => _lockout ??= new CountdownTimer(_config.CatchLockout);

        CountdownTimer Fumble => _fumble ??= new CountdownTimer(
            Mathf.Max(0.01f, _config.LateClampFumble));

        void Awake()
        {
            _ = Window;
            _ = Lockout;
            _ = Fumble;
        }

        void OnDestroy()
        {
            if (_window != null) _window.OnTimerStop -= OnWindowClosed;
            _window?.Dispose();
            _lockout?.Dispose();
            _fumble?.Dispose();
        }

        public void Initialise(int slot) => _slot = slot;

        /// <summary>
        /// Handles a catch button tap. Returns true if a window opened.
        /// </summary>
        /// <remarks>
        /// A press during an open window is ignored rather than restarting it - otherwise holding the
        /// button down at 60Hz would be a permanent brace, which is exactly the stalemate the tap-only
        /// rule exists to prevent (8.1).
        /// </remarks>
        public bool TryOpenWindow()
        {
            if (!_enabled || IsLockedOut || IsWindowActive) return false;

            _resolved = false;
            Window.Reset(_config.CatchWindow);
            Window.Start();
            return true;
        }

        /// <summary>Called when the ball was actually caught, so the window closes without a penalty.</summary>
        public void NotifyCaught()
        {
            _resolved = true;
            Window.Stop();
            Lockout.Stop();
        }

        /// <summary>
        /// Called when the clamp resolved as LATE: no penalty for the press, but no free pickup.
        /// </summary>
        public void NotifyLateClamp()
        {
            NotifyCaught();

            if (_config.LateClampFumble <= 0f) return;

            Fumble.Reset(_config.LateClampFumble);
            Fumble.Start();
        }

        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            if (!enabled) ResetState();
        }

        public void ResetState()
        {
            _resolved = true;
            Window.Stop();
            Lockout.Stop();
            Fumble.Stop();
        }

        void OnWindowClosed()
        {
            if (_resolved) return;

            _resolved = true;
            Lockout.Reset(_config.CatchLockout);
            Lockout.Start();
            EventBus<CatchMissed>.Raise(new CatchMissed(_slot, _config.CatchLockout));
        }
    }
}
