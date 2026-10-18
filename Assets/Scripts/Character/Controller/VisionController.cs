using System.Collections.Generic;
using Micosmo.SensorToolkit;
using SB4b1.GameMath;
using UnityEngine;

public class VisionController : MonoBehaviour
{
    [SerializeField] private Sensor sensor;
    [SerializeField] private BaseAI brain;
    [SerializeField] private PerceptionTuningSO tuning;

    // Allocated once; owned by this component; read by BaseAI/GOAP sensors
    [SerializeField] private readonly List<Transform> enemyTargets = new(16);
    [SerializeField] private readonly List<Transform> allyTargets = new(16);

    // Preallocated internal buffer for SensorToolkit zero-alloc queries
    private readonly List<GameObject> detectionBuffer = new(16);
    private static readonly HashSet<GameObject> warnedUnclassifiedObjects = new();

    public List<Transform> EnemyTargets => enemyTargets;
    public List<Transform> AllyTargets => allyTargets;
    public Sensor Sensor => sensor;
    public float VisionRadius => GetVisionRadius();
    public BaseAI Brain { get => brain ??= (GetComponent<BaseAI>() ?? GetComponentInParent<BaseAI>()); set => brain = value; }
    public PerceptionTuningSO Tuning { get => tuning; set => tuning = value; }

    // Adaptive pulse state
    public const float DEFAULT_SEARCH_INTERVAL = 0.2f;
    private float searchTimer;
    private float configuredVisionRadius = -1f;
    private PerceptionTier currentTier = PerceptionTier.Default;
    private float tierDwellTimer;
    private float lastForcePulseTime = -999f;
    private bool forcePulseRequested;
    private SquadOrderType lastKnownSquadOrder = SquadOrderType.None;

    public PerceptionTier CurrentTier => currentTier;
    public float SearchTimer => searchTimer;
    public float TierDwellTimer => tierDwellTimer;
    public float LastForcePulseTime => lastForcePulseTime;
    public bool ForcePulseRequested => forcePulseRequested;

    [SerializeField] private FactionMember factionMember;
    [SerializeField] private CharacterStatsManager statsManager;

    private void Awake()
    {
        ResolveDependencies();

        if (configuredVisionRadius > 0f && sensor != null)
        {
            ApplyRadiusToSensor(sensor, configuredVisionRadius);
        }
    }

    private void OnEnable()
    {
        ResolveDependencies();

        if (statsManager != null)
        {
            statsManager.OnDamaged -= HandleOnDamaged;
            statsManager.OnDamaged += HandleOnDamaged;
        }

        SBGames.Characters.AI.AdventurerEventHub.OnCharacterDied -= HandleCharacterDeath;
        SBGames.Characters.AI.AdventurerEventHub.OnCharacterDied += HandleCharacterDeath;

        currentTier = EvaluateTier();
        tierDwellTimer = 0f;
        InitializePhaseOffset();
    }


    private void OnDisable()
    {
        if (statsManager != null)
        {
            statsManager.OnDamaged -= HandleOnDamaged;
        }

        SBGames.Characters.AI.AdventurerEventHub.OnCharacterDied -= HandleCharacterDeath;
    }

    private void ResolveDependencies()
    {
        if (brain == null) brain = GetComponent<BaseAI>() ?? GetComponentInParent<BaseAI>();
        if (sensor == null) sensor = GetComponentInChildren<Sensor>();
        if (factionMember == null) factionMember = GetComponentInParent<FactionMember>();
        if (statsManager == null) statsManager = GetComponent<CharacterStatsManager>() ?? GetComponentInParent<CharacterStatsManager>() ?? GetComponentInChildren<CharacterStatsManager>();
    }

    /// <summary>
    /// Phase offset on activation so units' perception pulses do not synchronize across units.
    /// Uses GameRng seeded with unit instance and coordinate hash to remain reproducible per seed.
    /// </summary>
    public void InitializePhaseOffset()
    {
        float interval = GetTierInterval(currentTier);
        uint seed = (uint)(GetEntityId().GetHashCode() ^ (int)(transform.position.x * 100f) ^ ((int)(transform.position.y * 100f) << 16));
        if (seed == 0) seed = 1;
        var rng = new GameRng(seed);
        searchTimer = rng.Range(0f, Mathf.Max(0.01f, interval));
        tierDwellTimer = 0f;
    }

    public void InitializePhaseOffset(ref GameRng rng)
    {
        float interval = GetTierInterval(currentTier);
        searchTimer = rng.Range(0f, Mathf.Max(0.01f, interval));
        tierDwellTimer = 0f;
    }

    /// <summary>
    /// Triggers a rate-limited perception pulse on the next tick.
    /// Call on damage taken, ally death, squad order received, and AlertAllies signals.
    /// </summary>
    public void RequestImmediatePulse()
    {
        float minGap = tuning != null ? tuning.ForcePulseMinGap : 0.05f;
        if (Time.time - lastForcePulseTime < minGap)
        {
            return;
        }
        forcePulseRequested = true;
    }

    private void HandleOnDamaged(DamageData damage)
    {
        RequestImmediatePulse();
    }

    private void HandleCharacterDeath(SBGames.Characters.AI.CharacterDiedEvent evt)
    {
        if (evt.CharacterStats == null || evt.CharacterStats == statsManager) return;

        // Check if the deceased was an ally
        if (factionMember != null && evt.CharacterGameObject != null && factionMember.IsAllyOf(evt.CharacterGameObject))
        {
            RequestImmediatePulse();
        }
        else if (factionMember == null && evt.CharacterGameObject != null)
        {
            string myTag = gameObject.tag;
            string deadTag = evt.CharacterGameObject.tag;
            if ((myTag == "Adventurer" && deadTag == "Adventurer") ||
                (myTag == "Monster" && deadTag == "Monster"))
            {
                RequestImmediatePulse();
            }
        }
    }

    public void NotifySquadOrderReceived(SquadOrderType order)
    {
        if (order != lastKnownSquadOrder)
        {
            lastKnownSquadOrder = order;
            RequestImmediatePulse();
        }
    }

    public PerceptionTier EvaluateTier()
    {
        var b = Brain;
        if (b == null)
        {
            return PerceptionTier.Default;
        }

        // 1. Critical Tier: Low health, fleeing, Morale Shock (high fear), Survive/FallBack goal
        float critHpThresh = tuning != null ? tuning.CriticalHealthThreshold : 0.3f;
        int critFearThresh = tuning != null ? tuning.CriticalFearThreshold : 80;

        float maxLife = b.StatsManager != null && b.StatsManager.MaxLife != null && b.StatsManager.MaxLife.Value > 0f
            ? b.StatsManager.MaxLife.Value
            : 100f;
        // If StatsManager is attached, use AgentLife / maxLife; if no StatsManager is attached and agentLife is 0 (uninitialized default), treat as healthy (1.0)
        float currentHpPct = (b.StatsManager != null || b.AgentLife > 0f) ? (b.AgentLife / maxLife) : 1.0f;

        bool isLowHealth = currentHpPct <= critHpThresh;
        bool isMoraleShock = b.FearLevel >= critFearThresh;
        bool isCriticalGoal = b.ActiveGoal == EnumUtilGoal.Survive || b.ActiveGoal == EnumUtilGoal.FallBack;
        bool isCriticalOrder = b.SquadOrder == SquadOrderType.FallBack;


        if (isLowHealth || isMoraleShock || isCriticalGoal || isCriticalOrder)
        {
            return PerceptionTier.Critical;
        }

        // 2. Engaged Tier: Current target, or enemy within attack range or threat range margin
        float baseAtkRange = b.StatsManager != null && b.StatsManager.AttackRange != null
            ? b.StatsManager.AttackRange.Value
            : 1.5f;
        float threatMargin = tuning != null ? tuning.ThreatRangeMargin : 3.0f;
        float engagedDistanceLimit = baseAtkRange + threatMargin;

        bool hasTarget = b.CurrentTarget != null;
        bool hasEngagedDistance = b.CurrentTarget != null && b.TargetDistance <= engagedDistanceLimit;
        bool hasDirectEnemies = b.EnemyTargets != null && b.EnemyTargets.Count > 0;
        bool hasHighThreat = b.ThreatLevel > 0;

        if (hasTarget || hasEngagedDistance || hasDirectEnemies || hasHighThreat)
        {
            return PerceptionTier.Engaged;
        }

        // 3. Calm Tier: Idle/none, wandering, scouting, following leader with NO known enemies
        bool hasNoKnownEnemies = !b.HasKnownEnemies;
        bool isCalmGoal = b.ActiveGoal == EnumUtilGoal.None ||
                         b.ActiveGoal == EnumUtilGoal.Wander ||
                         b.ActiveGoal == EnumUtilGoal.ScoutRecon ||
                         b.ActiveGoal == EnumUtilGoal.DebriefLeader ||
                         b.ActiveGoal == EnumUtilGoal.FollowLeader ||
                         b.ActiveGoal == EnumUtilGoal.Standby;

        if (hasNoKnownEnemies && isCalmGoal && b.ThreatLevel == 0 && b.FearLevel == 0)
        {
            return PerceptionTier.Calm;
        }

        return PerceptionTier.Default;
    }


    public float GetTierInterval(PerceptionTier tier)
    {
        if (tuning != null)
        {
            return tuning.GetInterval(tier);
        }
        return DEFAULT_SEARCH_INTERVAL;
    }

    private static int GetTierPriority(PerceptionTier tier)
    {
        switch (tier)
        {
            case PerceptionTier.Critical: return 3;
            case PerceptionTier.Engaged:  return 2;
            case PerceptionTier.Default:  return 1;
            case PerceptionTier.Calm:
            default:
                return 0;
        }
    }

    public void UpdateTierSelection(float deltaTime)
    {
        PerceptionTier evaluated = EvaluateTier();

        if (evaluated == currentTier)
        {
            tierDwellTimer = 0f;
            return;
        }

        float evalInterval = GetTierInterval(evaluated);
        float currentInterval = GetTierInterval(currentTier);

        if (evalInterval < currentInterval)
        {
            // Moving to a faster tier (shorter interval) is immediate
            currentTier = evaluated;
            tierDwellTimer = 0f;
        }
        else
        {
            // Dropping to a slower tier (longer interval) requires hysteresis dwell time
            float dwellLimit = tuning != null ? tuning.HysteresisDwellTime : 0.5f;
            tierDwellTimer += deltaTime;
            if (tierDwellTimer >= dwellLimit)
            {
                currentTier = evaluated;
                tierDwellTimer = 0f;
            }
        }
    }


    /// <summary>
    /// Sets the perception sensor detection radius (e.g. from <see cref="AdventurerAIProfile"/>).
    /// Supports <see cref="RangeSensor2D"/>, <see cref="LOSSensor2D"/> (including its <see cref="LOSSensor2D.InputSensor"/>),
    /// and <see cref="RaySensor2D"/>.
    /// </summary>
    public void SetVisionRadius(float radius)
    {
        float clamped = Mathf.Max(0.1f, radius);
        configuredVisionRadius = clamped;

        if (sensor == null)
        {
            sensor = GetComponentInChildren<Sensor>();
        }

        if (sensor != null)
        {
            ApplyRadiusToSensor(sensor, clamped);
        }
    }

    public float GetVisionRadius()
    {
        if (sensor == null)
        {
            sensor = GetComponentInChildren<Sensor>();
        }

        if (sensor is RangeSensor2D range2D)
        {
            return range2D.Circle.Radius;
        }
        if (sensor is LOSSensor2D los2D)
        {
            if (los2D.LimitDistance || los2D.InputSensor == null)
            {
                return los2D.MaxDistance;
            }
            if (los2D.InputSensor is RangeSensor2D inputRange2D)
            {
                return inputRange2D.Circle.Radius;
            }
            return los2D.MaxDistance;
        }
        if (sensor is RaySensor2D ray2D)
        {
            return ray2D.Length;
        }

        return configuredVisionRadius > 0f ? configuredVisionRadius : 0f;
    }

    private static void ApplyRadiusToSensor(Sensor targetSensor, float radius)
    {
        if (targetSensor == null) return;

        if (targetSensor is RangeSensor2D range2D)
        {
            range2D.Circle.Radius = radius;
        }
        else if (targetSensor is LOSSensor2D los2D)
        {
            los2D.MaxDistance = radius;
            if (los2D.InputSensor != null && los2D.InputSensor != targetSensor)
            {
                ApplyRadiusToSensor(los2D.InputSensor, radius);
            }
        }
        else if (targetSensor is RaySensor2D ray2D)
        {
            ray2D.Length = radius;
        }
        else if (targetSensor is RangeSensor range3D)
        {
            range3D.Sphere.Radius = radius;
        }
        else if (targetSensor is LOSSensor los3D)
        {
            los3D.MaxDistance = radius;
            if (los3D.InputSensor != null && los3D.InputSensor != targetSensor)
            {
                ApplyRadiusToSensor(los3D.InputSensor, radius);
            }
        }
        else if (targetSensor is RaySensor ray3D)
        {
            ray3D.Length = radius;
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        UpdateTierSelection(dt);

        searchTimer -= dt;

        if (forcePulseRequested)
        {
            forcePulseRequested = false;
            lastForcePulseTime = Time.time;
            searchTimer = GetTierInterval(currentTier);
            TickPerception();
            return;
        }

        if (searchTimer > 0f) return;

        searchTimer = GetTierInterval(currentTier);
        TickPerception();
    }

    /// <summary>
    /// Executes a throttled perception tick: pulses sensor, updates cached target lists without allocations,
    /// and synchronizes targets with BaseAI.
    /// </summary>
    public void TickPerception()
    {
        if (sensor == null) return;

        sensor.Pulse();

        detectionBuffer.Clear();
        sensor.GetDetectionsByDistance(detectionBuffer);

        enemyTargets.Clear();
        allyTargets.Clear();
        if (factionMember == null)
        {
            factionMember = GetComponentInParent<FactionMember>();
        }

        var myBrain = brain != null ? brain.GetComponentInParent<CharacterBrain>() : GetComponentInParent<CharacterBrain>();
        var myUnit = myBrain != null ? myBrain.gameObject : (factionMember != null ? factionMember.gameObject : gameObject);

        for (int i = 0; i < detectionBuffer.Count; i++)
        {
            var obj = detectionBuffer[i];
            if (obj == null) continue;

            // Resolve target unit root (parent containing CharacterBrain or IFactionMember)
            var targetBrain = obj.GetComponentInParent<CharacterBrain>();
            var targetMember = obj.GetComponentInParent<IFactionMember>();
            var targetUnit = targetBrain != null ? targetBrain.gameObject : ((targetMember as Component)?.gameObject ?? obj);
            var targetTransform = targetUnit.transform;

            if (targetUnit == myUnit) continue; // Skip self

            if (factionMember != null)
            {
                if (factionMember.IsAllyOf(obj))
                {
                    if (!allyTargets.Contains(targetTransform))
                    {
                        allyTargets.Add(targetTransform);
                    }
                }
                else if (factionMember.IsHostileTo(obj))
                {
                    if (!enemyTargets.Contains(targetTransform))
                    {
                        enemyTargets.Add(targetTransform);
                    }
                }
                else
                {
                    // Unclassified detection: ignore and warn once per object
                    if (warnedUnclassifiedObjects.Add(obj))
                    {
                        Debug.LogWarning($"[VisionController] Unclassified detection ignored: '{obj.name}' (unit: '{targetUnit.name}', tag: '{targetUnit.tag}'). Neither ally nor hostile to faction '{factionMember.Faction}'.", this);
                    }
                }
            }
            else
            {
                string myTag = myUnit.tag;
                string targetTag = targetUnit.tag;
                if ((myTag == "Adventurer" && targetTag == "Adventurer") ||
                    (myTag == "Monster" && targetTag == "Monster"))
                {
                    if (!allyTargets.Contains(targetTransform))
                    {
                        allyTargets.Add(targetTransform);
                    }
                }
                else if ((myTag == "Adventurer" && targetTag == "Monster") ||
                         (myTag == "Monster" && targetTag == "Adventurer"))
                {
                    if (!enemyTargets.Contains(targetTransform))
                    {
                        enemyTargets.Add(targetTransform);
                    }
                }
                else
                {
                    if (warnedUnclassifiedObjects.Add(obj))
                    {
                        Debug.LogWarning($"[VisionController] Unclassified detection ignored: '{obj.name}' (unit: '{targetUnit.name}', tag: '{targetTag}'). Neither ally nor hostile to tag '{myTag}'.", this);
                    }
                }
            }
        }

        if (brain != null)
        {
            brain.UpdateTargets();
        }
    }

    /// <summary>
    /// Returns the nearest detected target transform without allocating memory.
    /// </summary>
    public Transform GetTarget()
    {
        if (sensor == null) return null;
        var nearest = sensor.GetNearestDetection();
        return nearest != null ? nearest.transform : null;
    }

    /// <summary>
    /// Returns the preallocated enemy targets list.
    /// </summary>
    public List<Transform> GetTargets()
    {
        return enemyTargets;
    }

    /// <summary>
    /// Evaluates if an unhit target exists for the given skill instance.
    /// </summary>
    public bool TryGetTarget(SkillInstance skill, out GameObject target)
    {
        if (sensor == null)
        {
            target = null;
            return false;
        }

        sensor.Pulse();

        var detections = sensor.GetDetections();
        if (detections != null)
        {
            for (int i = 0; i < detections.Count; i++)
            {
                GameObject obj = detections[i];
                if (obj == null) continue;

                if (skill != null && skill.HitTargets.Contains(obj))
                    continue;

                target = obj;
                return true;
            }
        }

        target = null;
        return false;
    }
}