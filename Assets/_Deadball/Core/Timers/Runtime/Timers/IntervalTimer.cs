using System;
using UnityEngine;

namespace Zone8.ImprovedTimers
{
    /// <summary>
    /// Countdown timer that fires an event every interval until completion.
    /// </summary>
    public class IntervalTimer : Timer
    {
        readonly float _interval;
        float _nextInterval;

        public Action OnInterval = delegate { };

        public IntervalTimer(float totalTime, float intervalSeconds) : base(totalTime)
        {
            if (intervalSeconds <= 0)
            {
                // A non-positive interval would spin the threshold loop in Tick forever.
                Debug.LogError($"[IntervalTimer] intervalSeconds must be positive, got {intervalSeconds}. Falling back to the total time.");
                intervalSeconds = Mathf.Max(totalTime, 0.01f);
            }

            _interval = intervalSeconds;
            _nextInterval = totalTime - _interval;
        }

        public override void Tick()
        {
            if (IsRunning && CurrentTime > 0)
            {
                CurrentTime -= Time.deltaTime;

                // Fire interval events as long as thresholds are crossed
                while (CurrentTime <= _nextInterval && _nextInterval >= 0)
                {
                    OnInterval.Invoke();
                    _nextInterval -= _interval;
                }
            }

            if (IsRunning && CurrentTime <= 0)
            {
                CurrentTime = 0;
                Stop();
            }
        }

        public override bool IsFinished => CurrentTime <= 0;

        public override void Reset()
        {
            base.Reset();
            _nextInterval = _initialTime - _interval;
        }

        public override void Reset(float newTime)
        {
            base.Reset(newTime);
            _nextInterval = _initialTime - _interval;
        }
    }
}