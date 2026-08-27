using System;
using UnityEngine;

namespace Zone8.ImprovedTimers
{
    public abstract class Timer : IDisposable
    {
        public float CurrentTime { get; protected set; }
        public bool IsRunning { get; private set; }

        protected float _initialTime;
        private bool _isRegistered;
        private bool _disposed;

        /// <summary>Normalized 0..1 progress. Always 0 for timers without a duration (stopwatch, frequency).</summary>
        public float Progress => _initialTime <= 0 ? 0 : Mathf.Clamp01(CurrentTime / _initialTime);

        public Action OnTimerStart = delegate { };
        public Action OnTimerStop = delegate { };

        protected Timer(float value)
        {
            _initialTime = value;
        }

        public void Start()
        {
            CurrentTime = _initialTime;
            if (IsRunning) return;

            IsRunning = true;
            Register();
            OnTimerStart.Invoke();
        }

        public void Stop()
        {
            if (!IsRunning) return;

            IsRunning = false;
            Deregister();
            OnTimerStop.Invoke();
        }

        // Pause/Resume are a matched pair — they temporarily halt ticking without
        // removing the timer from the manager.  Resume also works after Stop() since
        // it re-registers if needed, making the pair safe regardless of call order.
        public void Pause() => IsRunning = false;

        public void Resume()
        {
            IsRunning = true;
            Register();
        }

        public abstract void Tick();
        public abstract bool IsFinished { get; }

        public virtual void Reset() => CurrentTime = _initialTime;

        public virtual void Reset(float newTime)
        {
            _initialTime = newTime;
            Reset();
        }

        private void Register()
        {
            if (_isRegistered) return;
            _isRegistered = true;
            TimerManager.RegisterTimer(this);
        }

        private void Deregister()
        {
            if (!_isRegistered) return;
            _isRegistered = false;
            TimerManager.DeregisterTimer(this);
        }

        ~Timer() => Dispose(false);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
                Deregister();

            _disposed = true;
        }
    }
}