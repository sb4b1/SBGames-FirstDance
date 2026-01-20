using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "SBGames/Status Effect/Status Effect Data")]
public class StatusEffectData : ScriptableObject
{
    public string ID;
    public string displayName;

     [Header("Duration")]
    public bool hasDuration = true;
    public float duration = 5f;

    [Header("Stacking")]
    public bool isStackable = false;
    public int maxStacks = 1;
    public bool refreshDurationOnStack = true;

    [Header("Tick")]
    public float tickInterval = 1f;

    [Header("Stat Modifiers")]
    public List<StatEffect> statEffects = new();
    // Hooks
    public virtual void OnApply(GameObject target, int stacks) {}
    public virtual void OnTick(GameObject target, int stacks) {}
    public virtual void OnRemove(GameObject target, int stacks) {}
}

[System.Serializable]
public class StatEffect
{
    public StatType stat;
    public StatModType modType;
    public float valuePerStack;
}
