using System;

[Serializable]
public struct DamageData
{
    public float Amount;
    public bool IsCritical;
    public DamageType Type;
    [NonSerialized]
    public object Source;

    public DamageData(
        float amount,
        DamageType type,
        object source = null,
        bool isCritical = false)
    {
        Amount = amount;
        Type = type;
        Source = source;
        IsCritical = isCritical;
    }
}
