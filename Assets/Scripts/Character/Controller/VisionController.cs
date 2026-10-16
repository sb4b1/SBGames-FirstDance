using System.Collections.Generic;
using Micosmo.SensorToolkit;
using UnityEngine;

public class VisionController : MonoBehaviour
{
    [SerializeField] private Sensor sensor;
    [SerializeField] private BaseAI brain;

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

    private float searchTimer;
    private float configuredVisionRadius = -1f;
    private const float SEARCH_INTERVAL = 0.2f;

    [SerializeField] private FactionMember factionMember;

    private void Awake()
    {
        if (brain == null) brain = GetComponent<BaseAI>();
        if (brain == null) brain = GetComponentInParent<BaseAI>();
        if (sensor == null) sensor = GetComponentInChildren<Sensor>();
        if (factionMember == null) factionMember = GetComponentInParent<FactionMember>();

        if (configuredVisionRadius > 0f && sensor != null)
        {
            ApplyRadiusToSensor(sensor, configuredVisionRadius);
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
        searchTimer -= Time.deltaTime;
        if (searchTimer > 0f) return;
        searchTimer = SEARCH_INTERVAL;

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