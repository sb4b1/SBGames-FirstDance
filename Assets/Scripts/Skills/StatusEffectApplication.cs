using UnityEngine;

[System.Serializable]
public class StatusEffectApplication
{
    public StatusEffectData effect;

    [Range(0f, 1f)]
    public float procChance = 1f;
}