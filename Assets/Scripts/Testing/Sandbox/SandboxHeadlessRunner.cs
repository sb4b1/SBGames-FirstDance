using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace SBGames.Testing.Sandbox
{
    public static class SandboxHeadlessRunner
    {
        private static int _exitCode = 0;

        public static int LastExitCode => _exitCode;

        /// <summary>
        /// Unity CLI Entry Point:
        /// unity -quit -batchmode -projectPath "..." -executeMethod SBGames.Testing.Sandbox.SandboxHeadlessRunner.RunCombatSandboxHeadless
        /// </summary>
        public static void RunCombatSandboxHeadless()
        {
            RunSandboxHeadless("Combat", "Assets/Scenes/Combat_Sandbox.unity");
        }

        public static void RunSandboxHeadless(string sandboxId, string scenePath)
        {
            Debug.Log($"<color=cyan>[SandboxHeadlessRunner] Starting headless run for sandbox: {sandboxId} (Scene: {scenePath})</color>");

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (!scene.IsValid())
                {
                    Debug.LogError($"[SandboxHeadlessRunner] Failed to open scene at path: {scenePath}");
                    ExitBatch(1);
                    return;
                }
            }
#endif

            // Locate or bootstrap runner
            var runner = UnityEngine.Object.FindAnyObjectByType<SandboxRunnerBase>();
            if (runner == null)
            {
                // Try to find CombatTestEnvironmentBootstrap or BattleSimulationTestRunner to attach CombatSandboxRunnerAdapter
                var battleRunner = UnityEngine.Object.FindAnyObjectByType<BattleSimulationTestRunner>();
                if (battleRunner != null)
                {
                    runner = battleRunner.gameObject.AddComponent<CombatSandboxRunnerAdapter>();
                }
                else
                {
                    Debug.LogError($"[SandboxHeadlessRunner] No SandboxRunnerBase or BattleSimulationTestRunner found in scene {scenePath}!");
                    ExitBatch(2);
                    return;
                }
            }

            // Ensure bootstrap ran
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<CombatTestEnvironmentBootstrap>();
            if (bootstrap != null)
            {
                bootstrap.ExecuteBootstrap();
            }

            // Run all scenarios
            runner.StartCoroutine(runner.RunAllScenariosRoutine(report =>
            {
                if (report == null || report.FailedRuns > 0)
                {
                    Debug.LogError($"[SandboxHeadlessRunner] Sandbox {sandboxId} completed with FAILURES! Total: {report?.TotalRuns}, Failed: {report?.FailedRuns}");
                    ExitBatch(3);
                }
                else
                {
                    Debug.Log($"<color=green>[SandboxHeadlessRunner] Sandbox {sandboxId} completed with ALL PASS! Total: {report.TotalRuns}</color>");
                    ExitBatch(0);
                }
            }));
        }

        public static void ExitBatch(int code)
        {
            _exitCode = code;
            Debug.Log($"[SandboxHeadlessRunner] Exiting with code {code}");

#if UNITY_EDITOR
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
#endif
        }
    }
}
