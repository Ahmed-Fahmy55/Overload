using System.Collections;
using Core.Events;
using Deadball.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Deadball.Presentation
{
    /// <summary>
    /// Freeze frames on catches and knocks (GDD section 20).
    /// </summary>
    /// <remarks>
    /// The catch freeze is nearly twice as long as the knock freeze on purpose. The design asks for
    /// the catch to be disproportionately loud - it is the trailer shot and the reason anyone
    /// remembers the game - so it gets the longer hold even though a knock is the bigger event.
    /// </remarks>
    public class HitstopService : MonoBehaviour
    {
        [Title("Durations")]
        [Tooltip("Perfect clamp only. A late clamp gets no freeze at all (20).")]
        [SuffixLabel("s", true), MinValue(0f), SerializeField] float _onCatch = 0.15f;
        [SuffixLabel("s", true), MinValue(0f), SerializeField] float _onKnock = 0.08f;

        [Title("KO Slow-Mo")]
        [SerializeField] bool _slowMoOnKo = true;
        [ShowIf("_slowMoOnKo"), SuffixLabel("s", true), MinValue(0f), SerializeField] float _koSlowMoDuration = 0.3f;
        [ShowIf("_slowMoOnKo"), PropertyRange(0.05f, 1f), SerializeField] float _koTimeScale = 0.35f;

        EventBinding<BallCaught> _caught;
        EventBinding<FighterKnocked> _knocked;
        EventBinding<FighterKnockedOut> _knockedOut;
        Coroutine _active;

        void OnEnable()
        {
            // Only a PERFECT clamp earns the freeze. Freezing on a late clamp would reward the
            // mercy tier with the perfect tier's punctuation and flatten the difference between them.
            _caught = new EventBinding<BallCaught>(evt =>
            {
                if (evt.Tier == Deadball.Fighters.ClampTier.Perfect) Freeze(_onCatch);
            });
            _knocked = new EventBinding<FighterKnocked>(() => Freeze(_onKnock));
            _knockedOut = new EventBinding<FighterKnockedOut>(OnKnockedOut);

            EventBus<BallCaught>.Register(_caught);
            EventBus<FighterKnocked>.Register(_knocked);
            EventBus<FighterKnockedOut>.Register(_knockedOut);
        }

        void OnDisable()
        {
            EventBus<BallCaught>.Deregister(_caught);
            EventBus<FighterKnocked>.Deregister(_knocked);
            EventBus<FighterKnockedOut>.Deregister(_knockedOut);

            RestoreTimeScale();
        }

        public void Freeze(float duration)
        {
            if (duration <= 0f) return;

            Play(Run(0f, duration));
        }

        void OnKnockedOut()
        {
            if (_slowMoOnKo)
                Play(Sequence(_onKnock, _koTimeScale, _koSlowMoDuration));
            else
                Freeze(_onKnock);
        }

        void Play(IEnumerator routine)
        {
            // A knock landing during a catch freeze must not leave the game stopped, so a new
            // request always replaces the running one and restores the scale itself.
            if (_active != null) StopCoroutine(_active);
            _active = StartCoroutine(routine);
        }

        IEnumerator Run(float scale, float duration)
        {
            Time.timeScale = scale;
            yield return new WaitForSecondsRealtime(duration);
            RestoreTimeScale();
            _active = null;
        }

        IEnumerator Sequence(float freezeDuration, float slowScale, float slowDuration)
        {
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(freezeDuration);

            Time.timeScale = slowScale;
            yield return new WaitForSecondsRealtime(slowDuration);

            RestoreTimeScale();
            _active = null;
        }

        static void RestoreTimeScale()
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }
    }
}
