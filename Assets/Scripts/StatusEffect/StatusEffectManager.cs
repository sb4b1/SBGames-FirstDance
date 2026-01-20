using System.Collections.Generic;
using UnityEngine;

public class StatusEffectManager : MonoBehaviour
{
    private readonly Dictionary<string, StatusEffect> activeEffects = new();
    private CharacterStats stats;
    private readonly List<string> toRemove = new();

    private void Awake()
    {
        stats = GetComponent<CharacterStats>();
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

    public void ApplyEffect(StatusEffectData data)
    {
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

    private void RemoveEffect(string id)
    {
        if (!activeEffects.TryGetValue(id, out var effect))
            return;

        effect.Remove(stats, this);
        activeEffects.Remove(id);
    }
}
