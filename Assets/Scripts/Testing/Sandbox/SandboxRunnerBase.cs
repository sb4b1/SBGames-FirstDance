using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SBGames.Testing.Sandbox
{
    /// <summary>
    /// Base MonoBehaviour implementing ISandboxRunner. Provides speed controls, coroutine dispatching,
    /// safety timeout handling, and reporting hooks.
    /// </summary>
    public abstract class SandboxRunnerBase : MonoBehaviour, ISandboxRunner
    {
        [Header("Runner Base Settings")]
        [SerializeField] private float safetyTimeoutSeconds = 30f;
        [SerializeField] private float defaultTimeScale = 1f;

        public abstract string SandboxId { get; }
        public bool IsRunning { get; protected set; }
        public float SafetyTimeoutSeconds
        {
            get => safetyTimeoutSeconds;
            set => safetyTimeoutSeconds = value;
        }

        public float TimeScale
        {
            get => Time.timeScale;
            set => Time.timeScale = Mathf.Max(0f, value);
        }

        public abstract IReadOnlyList<SandboxScenarioSO> AvailableScenarios { get; }
        public SandboxPassReport LastReport { get; protected set; }

        public abstract IEnumerator RunScenarioRoutine(SandboxScenarioSO scenario, Action<ScenarioRunResult> onComplete);
        public abstract IEnumerator RunAllScenariosRoutine(Action<SandboxPassReport> onComplete);
        public abstract void ResetSandbox();

        protected virtual void Awake()
        {
            Time.timeScale = defaultTimeScale;
        }
    }
}
