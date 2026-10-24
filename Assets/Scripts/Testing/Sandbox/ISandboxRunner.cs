using System;
using System.Collections;
using System.Collections.Generic;

namespace SBGames.Testing.Sandbox
{
    public interface ISandboxRunner
    {
        string SandboxId { get; }
        bool IsRunning { get; }
        float TimeScale { get; set; }
        float SafetyTimeoutSeconds { get; set; }

        IReadOnlyList<SandboxScenarioSO> AvailableScenarios { get; }
        SandboxPassReport LastReport { get; }

        IEnumerator RunScenarioRoutine(SandboxScenarioSO scenario, Action<ScenarioRunResult> onComplete);
        IEnumerator RunAllScenariosRoutine(Action<SandboxPassReport> onComplete);
        void ResetSandbox();
    }
}
