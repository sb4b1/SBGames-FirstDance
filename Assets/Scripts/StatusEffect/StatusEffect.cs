using System.Collections.Generic;
using UnityEngine;

public class StatusEffect
{
    public StatusEffectData Data { get; }
    public int Stacks { get; private set; }

    private float remainingTime;
    private float tickTimer;

    private readonly List<StatModifier> appliedModifiers = new();

    public StatusEffect(StatusEffectData data)
    {
        Data = data;
        Stacks = 1;
        remainingTime = data.duration;
        tickTimer = data.tickInterval;
    }

    public void Apply(CharacterStatsManager stats, object source)
    {
        RemoveModifiers(stats, source);
        if (stats == null) return;

        foreach (var effect in Data.statEffects)
        {
            var stat = stats.GetStat(effect.stat);
            if (stat == null) continue;

            float value = effect.valuePerStack * Stacks;
            var modifier = new StatModifier(value, effect.modType, source);

            stat.AddModifier(modifier);
            appliedModifiers.Add(modifier);
        }
    }

    public void AddStack(CharacterStatsManager stats, object source)
    {
        Stacks = Mathf.Min(Stacks + 1, Data.maxStacks);
        Apply(stats, source);
    }

    public void RefreshDuration()
    {
        remainingTime = Data.duration;
    }

    public bool Update(float deltaTime, GameObject target)
    {
        if (Data.hasDuration)
            remainingTime -= deltaTime;

        if (Data.tickInterval > 0f)
            {
            tickTimer -= deltaTime;
            if (tickTimer <= 0f)
            {
                Data.OnTick(target, Stacks);
                tickTimer = Data.tickInterval;
            }
        }

        return !Data.hasDuration || remainingTime > 0f;
    }

    public void Remove(CharacterStatsManager stats, object source)
    {
        RemoveModifiers(stats, source);
        if (stats != null)
        {
            Data.OnRemove(stats.gameObject, Stacks);
        }
    }

    private void RemoveModifiers(CharacterStatsManager stats, object source)
    {
        if (stats == null)
        {
            appliedModifiers.Clear();
            return;
        }

        foreach (var mod in appliedModifiers)
        {
            var stat = stats.GetStatByModifier(mod);
            stat?.RemoveModifier(mod);
        }
        appliedModifiers.Clear();
    }
}
