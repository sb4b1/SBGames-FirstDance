using System.Collections.Generic;
using Micosmo.SensorToolkit;
using UnityEngine;

public class VisionController : MonoBehaviour
{
    [SerializeField] private Sensor sensor;
    [SerializeField] private BaseAI brain;

    [InlineLabel("Current Target")]

    [SerializeField] private List<Transform> enemyTargets;
    [SerializeField] private List<Transform> allyTargets;

    public List<Transform> EnemyTargets { get => enemyTargets; private set => enemyTargets = value; }
    public List<Transform> AllyTargets { get => allyTargets; private set => allyTargets = value; }


    private float searchTimer = 0f;
    private const float SEARCH_INTERVAL = 0.2f;


    private void Update()
    {
        searchTimer -= Time.deltaTime;
        if (searchTimer <= 0f)
        {
            enemyTargets = GetTargets();
            brain.UpdateTargets();
            searchTimer = SEARCH_INTERVAL;
        }
    }
    
    

    public Transform GetTarget()
    {
        if (sensor == null)
            return null;

        sensor.Pulse();

        foreach (var detection in sensor.GetDetections())
        {
            return detection.transform;
        }

        return null;
    }

    public List<Transform> GetTargets()
    {
        if (sensor == null)
            return null;

        sensor.Pulse();

        var targets = new List<Transform>();
        foreach (var detection in sensor.GetDetections())
        {
            targets.Add(detection.transform);
        }

        return targets;
    }

    public bool TryGetTarget(SkillInstance skill, out GameObject target)
    {
        if (sensor == null)
        {
            target = null;
            return false;
        }

        sensor.Pulse();

        foreach (var detection in sensor.GetDetections())
        {
            GameObject obj = detection;

            if (skill.HitTargets.Contains(obj))
                continue;

            target = obj;
            return true;
        }

        target = null;
        return false;
    }

}