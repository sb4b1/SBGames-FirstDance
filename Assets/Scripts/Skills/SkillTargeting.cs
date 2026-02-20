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

    public GameObject GetTarget()
    {
        if (sensor == null)
            return null;

        sensor.Pulse();

        foreach (var detection in sensor.GetDetections())
        {
            return detection;
        }

        return null;
    }

    public List<GameObject> GetTargets()
    {
        if (sensor == null)
            return null;

        sensor.Pulse();

        return sensor.GetDetections();
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

        results = sensor.GetDetections();

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

        foreach (var detection in sensor.GetDetections())
            yield return detection.gameObject;
    }
}
