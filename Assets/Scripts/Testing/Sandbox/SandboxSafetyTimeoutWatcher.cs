using System;

namespace SBGames.Testing.Sandbox
{
    public class SandboxSafetyTimeoutWatcher
    {
        private readonly float _timeoutDuration;
        private float _elapsedSeconds;
        private bool _isTimedOut;

        public float TimeoutDuration => _timeoutDuration;
        public float ElapsedSeconds => _elapsedSeconds;
        public bool IsTimedOut => _isTimedOut;

        public SandboxSafetyTimeoutWatcher(float timeoutDuration)
        {
            _timeoutDuration = timeoutDuration > 0f ? timeoutDuration : 30f;
            _elapsedSeconds = 0f;
            _isTimedOut = false;
        }

        public void Reset()
        {
            _elapsedSeconds = 0f;
            _isTimedOut = false;
        }

        public bool Tick(float deltaTime)
        {
            if (_isTimedOut) return true;
            _elapsedSeconds += deltaTime;
            if (_elapsedSeconds >= _timeoutDuration)
            {
                _isTimedOut = true;
                return true;
            }
            return false;
        }

        public void ApplyTimeoutToResult(ScenarioRunResult result)
        {
            if (result == null) return;
            result.outcome = ScenarioOutcome.Timeout;
            result.durationSeconds = _elapsedSeconds;
            result.failureReason = $"Safety timeout reached after {_elapsedSeconds:F2}s (Limit: {_timeoutDuration:F2}s). Run reported as failed.";
        }
    }
}
