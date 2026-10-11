using UnityEngine;

/// <summary>
/// Governs the AI strategic reaction time and decision frequency.
/// Throttles Utility AI goal evaluation while allowing movement,
/// physics, animations, and perception wrappers to run smoothly at full frame rate.
/// </summary>
[RequireComponent(typeof(BaseAI))]
[DisallowMultipleComponent]
public class AIDecisionThrottler : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private BaseAI baseAI;
    [SerializeField] private CharacterBrain characterBrain;
    [SerializeField] private CharacterStatsManager statsManager;

    [Header("Fallback Defaults (if CharacterStatsManager is not present)")]
    [SerializeField, Min(0.05f)] private float defaultDecisionInterval = 0.3f;
    [SerializeField, Range(0f, 0.5f)] private float defaultIntervalJitter = 0.05f;

    [Header("Runtime Status (Read-Only)")]
    [SerializeField] private float nextDecisionTime;
    [SerializeField] private bool isDecisionLocked;
    private float lastMemoryTickTime;

    public float DecisionInterval => GetStatValue(statsManager?.DecisionInterval, defaultDecisionInterval);
    public float IntervalJitter => GetStatValue(statsManager?.IntervalJitter, defaultIntervalJitter);

    public bool IsActionLocked => isDecisionLocked || (characterBrain != null && characterBrain.IsBusy);

    private void Awake()
    {
        ResolveDependencies();
    }

    private void OnEnable()
    {
        if (statsManager != null)
        {
            statsManager.OnCharacterDeath += HandleCharacterDeath;
        }

        // Stagger initial execution randomly across agents to eliminate frame spikes on spawn
        float initialInterval = Mathf.Max(0.05f, DecisionInterval);
        nextDecisionTime = Time.time + Random.Range(0f, initialInterval);
        lastMemoryTickTime = Time.time;
        isDecisionLocked = false;
    }

    private void OnDisable()
    {
        if (statsManager != null)
        {
            statsManager.OnCharacterDeath -= HandleCharacterDeath;
        }
    }

    private void Update()
    {
        // Verify if character is active and alive
        if (characterBrain != null && characterBrain.AgentStatus != AgentStatus.Active)
        {
            return;
        }

        // Check if it is time to perform a decision cycle
        if (Time.time < nextDecisionTime)
        {
            return;
        }

        // Synchronized throttled memory decay (runs every 0.2s - 0.4s instead of per-frame Update)
        float elapsedMemoryTime = Time.time - lastMemoryTickTime;
        lastMemoryTickTime = Time.time;
        if (baseAI != null && baseAI.AIMemory != null)
        {
            baseAI.AIMemory.TickMemoryDecay(elapsedMemoryTime);
        }

        // If the agent is currently executing an attack or skill, defer strategic re-evaluation
        if (IsActionLocked)
        {
            nextDecisionTime = Time.time + 0.05f;
            return;
        }

        // Execute the strategic decision cycle
        if (baseAI != null)
        {
            baseAI.PerformStrategicDecision();
        }

        // Calculate next interval with jitter to prevent agent synchronization
        float baseInterval = Mathf.Max(0.05f, DecisionInterval);
        float jitter = Mathf.Clamp(IntervalJitter, 0f, baseInterval * 0.5f);
        float randomJitter = Random.Range(-jitter, jitter);
        float randomizedInterval = Mathf.Max(0.05f, baseInterval + randomJitter);

        nextDecisionTime = Time.time + randomizedInterval;
    }

    /// <summary>
    /// Immediately resets decision timing and interrupts current active plan.
    /// Triggered by combat events like stuns, knockbacks, or interruptions.
    /// </summary>
    public void InterruptDecisionLock()
    {
        isDecisionLocked = false;
        if (baseAI != null)
        {
            baseAI.InterruptCurrentPlan();
        }
        nextDecisionTime = Time.time;
    }

    private void HandleCharacterDeath(CharacterStatsManager stats)
    {
        isDecisionLocked = true;
        if (baseAI != null)
        {
            baseAI.InterruptCurrentPlan();
        }
    }

    private float GetStatValue(Stat stat, float fallback)
    {
        return (statsManager != null && stat != null && stat.Value > 0f) ? stat.Value : fallback;
    }

    private void ResolveDependencies()
    {
        if (baseAI == null) baseAI = GetComponent<BaseAI>();
        if (characterBrain == null) characterBrain = GetComponentInParent<CharacterBrain>();
        if (statsManager == null) statsManager = GetComponentInParent<CharacterStatsManager>();
    }
}
