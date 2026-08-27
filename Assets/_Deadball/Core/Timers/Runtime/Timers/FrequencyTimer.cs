using System;
using UnityEngine;

namespace Zone8.ImprovedTimers
{
    /// <summary>
    /// Timer that ticks at a specific frequency. (N times per second)
    /// </summary>
    public class FrequencyTimer : Timer
    {
        public int TicksPerSecond { get; private set; }

        public Action OnTick = delegate { };

        float _timeThreshold;

        public FrequencyTimer(int ticksPerSecond) : base(0)
        {
            CalculateTimeThreshold(ticksPerSecond);
        }

        public override void Tick()
        {
            if (!IsRunning) return;

            CurrentTime += Time.deltaTime;

            // Fire every elapsed tick, so frequencies above the frame rate don't lose ticks
            while (IsRunning && CurrentTime >= _timeThreshold)
            {
                CurrentTime -= _timeThreshold;
                OnTick.Invoke();
            }
        }

        public override bool IsFinished => !IsRunning;

        public override void Reset()
        {
            CurrentTime = 0;
        }

        public void Reset(int newTicksPerSecond)
        {
            CalculateTimeThreshold(newTicksPerSecond);
            Reset();
        }

        void CalculateTimeThreshold(int ticksPerSecond)
        {
            if (ticksPerSecond <= 0)
            {
                Debug.LogError($"[FrequencyTimer] ticksPerSecond must be positive, got {ticksPerSecond}. Falling back to 1.");
                ticksPerSecond = 1;
            }

            TicksPerSecond = ticksPerSecond;
            _timeThreshold = 1f / TicksPerSecond;
        }
    }
}