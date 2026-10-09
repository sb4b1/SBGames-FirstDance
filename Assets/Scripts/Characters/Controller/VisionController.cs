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

    public List<Transform> EnemyTargets => enemyTargets;
    public List<Transform> AllyTargets => allyTargets;

    private float searchTimer;
    private const float SEARCH_INTERVAL = 0.2f;

    [SerializeField] private FactionMember factionMember;

    private void Awake()
    {
        if (brain == null) brain = GetComponent<BaseAI>();
        if (brain == null) brain = GetComponentInParent<BaseAI>();
        if (sensor == null) sensor = GetComponentInChildren<Sensor>();
        if (factionMember == null) factionMember = GetComponentInParent<FactionMember>();
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
        for (int i = 0; i < detectionBuffer.Count; i++)
        {
            var obj = detectionBuffer[i];
            if (obj == null) continue;

            if (factionMember != null)
            {
                if (factionMember.IsAllyOf(obj))
                {
                    allyTargets.Add(obj.transform);
                }
                else if (factionMember.IsHostileTo(obj))
                {
                    enemyTargets.Add(obj.transform);
                }
                else
                {
                    enemyTargets.Add(obj.transform);
                }
            }
            else
            {
                if ((CompareTag("Adventurer") && obj.CompareTag("Adventurer")) ||
                    (CompareTag("Monster") && obj.CompareTag("Monster")))
                {
                    allyTargets.Add(obj.transform);
                }
                else
                {
                    enemyTargets.Add(obj.transform);
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