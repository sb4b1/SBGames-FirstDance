using System.Collections.Generic;
using UnityEngine;

public class StatusEffectManager : MonoBehaviour
{
    private readonly Dictionary<string, StatusEffect> activeEffects = new();
    private CharacterStatsManager stats;
    private readonly List<string> toRemove = new();

    private void Awake()
    {
        stats = GetComponent<CharacterStatsManager>() ?? GetComponentInParent<CharacterStatsManager>();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        toRemove.Clear();
        foreach (var pair in activeEffects)
        {
            if (!pair.Value.Update(dt, gameObject))
                toRemove.Add(pair.Key);
        }

        foreach (var id in toRemove)
            RemoveEffect(id);
    }

    public int ActiveEffectCount => activeEffects.Count;

    public void ApplyEffect(StatusEffectData data)
    {
        if (data == null || string.IsNullOrEmpty(data.ID)) return;
        if (stats == null) stats = GetComponent<CharacterStatsManager>() ?? GetComponentInParent<CharacterStatsManager>();

        if (activeEffects.TryGetValue(data.ID, out var effect))
        {
            if (data.isStackable)
            {
                effect.AddStack(stats, this);
                if (data.refreshDurationOnStack)
                    effect.RefreshDuration();
            }
            else
            {
                effect.RefreshDuration();
            }
        }
        else
        {
            effect = new StatusEffect(data);
            activeEffects.Add(data.ID, effect);

            effect.Apply(stats, this);
            data.OnApply(gameObject, effect.Stacks);
        }
    }

    public bool HasHarmfulEffect()
    {
        if (activeEffects.Count == 0) return false;
        foreach (var pair in activeEffects)
        {
            var effect = pair.Value;
            if (effect != null && effect.Data != null && effect.Data.IsHarmfulEffect())
            {
                return true;
            }
        }
        return false;
    }

    public int RemoveHarmfulEffects()
    {
        if (activeEffects.Count == 0) return 0;
        if (stats == null) stats = GetComponent<CharacterStatsManager>() ?? GetComponentInParent<CharacterStatsManager>();

        toRemove.Clear();
        foreach (var pair in activeEffects)
        {
            var effect = pair.Value;
            if (effect != null && effect.Data != null && effect.Data.IsHarmfulEffect())
            {
                toRemove.Add(pair.Key);
            }
        }

        int removedCount = toRemove.Count;
        for (int i = 0; i < removedCount; i++)
        {
            RemoveEffect(toRemove[i]);
        }
        toRemove.Clear();
        return removedCount;
    }

    public void ClearAllEffects()
    {
        if (activeEffects.Count == 0) return;
        if (stats == null) stats = GetComponent<CharacterStatsManager>() ?? GetComponentInParent<CharacterStatsManager>();

        toRemove.Clear();
        foreach (var pair in activeEffects)
        {
            toRemove.Add(pair.Key);
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            RemoveEffect(toRemove[i]);
        }
        toRemove.Clear();
    }

    public void RemoveEffect(string id)
    {
        if (!activeEffects.TryGetValue(id, out var effect))
            return;

        effect.Remove(stats, this);
        activeEffects.Remove(id);
    }
}
