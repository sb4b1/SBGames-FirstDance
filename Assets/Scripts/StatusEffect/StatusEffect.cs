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

    public void Apply(CharacterStats stats, object source)
    {
        RemoveModifiers(stats, source);

        foreach (var effect in Data.statEffects)
        {
            float value = effect.valuePerStack * Stacks;
            var modifier = new StatModifier(value, effect.modType, source);

            stats.GetStat(effect.stat).AddModifier(modifier);
            appliedModifiers.Add(modifier);
        }
    }

    public void AddStack(CharacterStats stats, object source)
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

    public void Remove(CharacterStats stats, object source)
    {
        RemoveModifiers(stats, source);
        Data.OnRemove(stats.gameObject, Stacks);
    }

    private void RemoveModifiers(CharacterStats stats, object source)
    {
        foreach (var mod in appliedModifiers)
        {
            stats.GetStatByModifier(mod).RemoveModifier(mod);
        }
        appliedModifiers.Clear();
    }
}
