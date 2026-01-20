using UnityEngine;
using Micosmo.SensorToolkit;
using System.Collections.Generic;

public class SkillTargeting : MonoBehaviour
{
    [Header("SensorToolkit")]
    [SerializeField] private Sensor sensor;

    // =========================
    // Single / Chain Target
    // =========================
    public bool TryGetNextTarget(SkillInstance skill, out GameObject target)
{
    if (sensor == null)
    {
        target = null;
        return false;
    }

    sensor.Pulse();

    foreach (var detection in sensor.Detections)
    {
        GameObject obj = detection.gameObject;

        if (skill.HitTargets.Contains(obj))
            continue;

        target = obj;
        return true;
    }

    target = null;
    return false;
}

    // =========================
    // AoE Targets
    // =========================
    public List<GameObject> GetAOETargets()
    {
        var results = new List<GameObject>();

        if (sensor == null)
            return results;

        sensor.Pulse();

        foreach (var detection in sensor.Detections)
        {
            results.Add(detection.gameObject);
        }

        return results;
    }

    // =========================
    // Beam Tick Targets
    // =========================
    public IEnumerable<GameObject> GetBeamTargets()
    {
        if (sensor == null)
            yield break;

        sensor.Pulse();

        foreach (var detection in sensor.Detections)
            yield return detection.gameObject;
    }
}
