using System;
using System.Collections.Generic;
using UnityEngine;

namespace SBGames.Testing.Sandbox
{
    public enum CombatScenarioType
    {
        Duel1v1,
        Survival1v1,
        PartySkirmish,
        RankComparison,
        StressTest,
        Custom
    }

    [CreateAssetMenu(fileName = "CombatScenario_", menuName = "SBGames/Testing/Combat Scenario")]
    public class CombatScenarioSO : SandboxScenarioSO
    {
        [Header("Combat Configuration")]
        public CombatScenarioType scenarioType = CombatScenarioType.Duel1v1;
        public int seed = 100;
        public string rankLabel = "E";
        public float customTimeScale = 2f;
        public float scenarioTimeoutSeconds = 25f;
        public bool capturePerf = false;
    }
}
