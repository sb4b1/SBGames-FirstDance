using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

[Serializable]
public class Stat
{
    // =========================
    // Base Value
    // =========================
    private float _baseValue;
    public float BaseValue
    {
        get => _baseValue;
        set
        {
            if (_baseValue != value)
            {
                _baseValue = value;
                _isValueDirty = true;
            }
        }
    }

    // =========================
    // Cached Values
    // =========================
    private float _cachedValue;
    private float _cachedModifierValue;

    private bool _isValueDirty = true;
    private bool _isModifierDirty = true;
    private bool _isSorted = true;

    // =========================
    // Modifiers
    // =========================
    protected readonly List<StatModifier> _modifiers;
    public ReadOnlyCollection<StatModifier> Modifiers { get; }

    // =========================
    // Public Values
    // =========================
    public float Value
    {
        get
        {
            if (_isValueDirty)
            {
                _cachedValue = CalculateValue(BaseValue);
                _isValueDirty = false;
            }
            return _cachedValue;
        }
    }

    public float ModifierValue
    {
        get
        {
            if (_isModifierDirty)
            {
                _cachedModifierValue = CalculateValue(1f);
                _isModifierDirty = false;
            }
            return _cachedModifierValue;
        }
    }

    // =========================
    // Constructors
    // =========================
    public Stat()
    {
        _modifiers = new List<StatModifier>();
        Modifiers = _modifiers.AsReadOnly();
    }

    public Stat(float baseValue) : this()
    {
        BaseValue = baseValue;
    }

    // =========================
    // Modifier Management
    // =========================
    public void AddModifier(StatModifier modifier)
    {
        _modifiers.Add(modifier);
        MarkDirty();
    }

    public bool RemoveModifier(StatModifier modifier)
    {
        if (_modifiers.Remove(modifier))
        {
            MarkDirty();
            return true;
        }
        return false;
    }

    public bool RemoveAllModifiersFromSource(object source)
    {
        int removed = _modifiers.RemoveAll(m => m.Source == source);
        if (removed > 0)
        {
            MarkDirty();
            return true;
        }
        return false;
    }

    public void ClearModifiers()
    {
        if (_modifiers.Count == 0) return;
        _modifiers.Clear();
        MarkDirty();
    }

    // =========================
    // Internal Helpers
    // =========================
    protected void MarkDirty()
    {
        _isValueDirty = true;
        _isModifierDirty = true;
        _isSorted = false;
    }

    protected void EnsureSorted()
    {
        if (_isSorted) return;
        _modifiers.Sort((a, b) => a.Order.CompareTo(b.Order));
        _isSorted = true;
    }

    // =========================
    // Core Calculation
    // =========================
    protected float CalculateValue(float baseValue)
    {
        EnsureSorted();

        float finalValue = baseValue;
        float percentAddSum = 0f;

        for (int i = 0; i < _modifiers.Count; i++)
        {
            var mod = _modifiers[i];

            switch (mod.Type)
            {
                case StatModType.Flat:
                    finalValue += mod.Value;
                    break;

                case StatModType.PercentAdd:
                    percentAddSum += mod.Value;

                    bool lastPercentAdd =
                        i + 1 >= _modifiers.Count ||
                        _modifiers[i + 1].Type != StatModType.PercentAdd;

                    if (lastPercentAdd)
                    {
                        finalValue *= 1f + percentAddSum;
                        percentAddSum = 0f;
                    }
                    break;

                case StatModType.PercentMult:
                    finalValue *= 1f + mod.Value;
                    break;
            }
        }

        // Float safety
        return (float)Math.Round(finalValue, 4);
    }

    public float PreviewValue(IEnumerable<StatModifier> previewModifiers)
    {
        float baseValue = BaseValue;
        float percentAddSum = 0f;

        List<StatModifier> temp = new(_modifiers);
        temp.AddRange(previewModifiers);
        temp.Sort((a, b) => a.Order.CompareTo(b.Order));

        float value = baseValue;

        for (int i = 0; i < temp.Count; i++)
        {
            var mod = temp[i];

            switch (mod.Type)
            {
                case StatModType.Flat:
                    value += mod.Value;
                    break;

                case StatModType.PercentAdd:
                    percentAddSum += mod.Value;
                    bool last =
                        i + 1 >= temp.Count ||
                        temp[i + 1].Type != StatModType.PercentAdd;
                    if (last)
                    {
                        value *= 1f + percentAddSum;
                        percentAddSum = 0f;
                    }
                    break;

                case StatModType.PercentMult:
                    value *= 1f + mod.Value;
                    break;
            }
        }

        return value;
    }
}

