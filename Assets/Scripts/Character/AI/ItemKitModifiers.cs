using System;
using SBGames.Inventory.Data;
using UnityEngine;

[Serializable]
public struct FunctionKitModifierEntry
{
    [SerializeField] private AdventurerFunction function;
    [SerializeField] private int hpPotionDelta;
    [SerializeField] private int manaPotionDelta;
    [SerializeField] private int statusCureDelta;

    public AdventurerFunction Function => function;
    public int HpPotionDelta => hpPotionDelta;
    public int ManaPotionDelta => manaPotionDelta;
    public int StatusCureDelta => statusCureDelta;

    public FunctionKitModifierEntry(
        AdventurerFunction function,
        int hpPotionDelta,
        int manaPotionDelta,
        int statusCureDelta)
    {
        this.function = function;
        this.hpPotionDelta = hpPotionDelta;
        this.manaPotionDelta = manaPotionDelta;
        this.statusCureDelta = statusCureDelta;
    }
}

[Serializable]
public struct WealthKitModifierEntry
{
    [SerializeField] private AdventurerWealth wealth;
    [SerializeField] private int hpPotionDelta;
    [SerializeField] private int manaPotionDelta;
    [SerializeField] private int statusCureDelta;
    [Tooltip("Optional replacement consumable item for this wealth tier (left null by default).")]
    [SerializeField] private ItemDataSO optionalReplacementItem;

    public AdventurerWealth Wealth => wealth;
    public int HpPotionDelta => hpPotionDelta;
    public int ManaPotionDelta => manaPotionDelta;
    public int StatusCureDelta => statusCureDelta;
    public ItemDataSO OptionalReplacementItem => optionalReplacementItem;

    public WealthKitModifierEntry(
        AdventurerWealth wealth,
        int hpPotionDelta,
        int manaPotionDelta,
        int statusCureDelta,
        ItemDataSO optionalReplacementItem = null)
    {
        this.wealth = wealth;
        this.hpPotionDelta = hpPotionDelta;
        this.manaPotionDelta = manaPotionDelta;
        this.statusCureDelta = statusCureDelta;
        this.optionalReplacementItem = optionalReplacementItem;
    }
}

/// <summary>
/// Data-driven item kit count deltas indexed by <see cref="AdventurerFunction"/> and <see cref="AdventurerWealth"/>.
/// Uses O(1) enum-indexed array lookups with no switch statements.
/// </summary>
[CreateAssetMenu(fileName = "DefaultItemKitModifiers", menuName = "SBGames/AI/Item Kit Modifiers")]
public class ItemKitModifiers : ScriptableObject
{
    public const int FunctionCount = 5;
    public const int WealthCount = 3;

    private static readonly FunctionKitModifierEntry[] DefaultFunctionTable = CreateDefaultFunctionArray();
    private static readonly WealthKitModifierEntry[] DefaultWealthTable = CreateDefaultWealthArray();

    [Header("Default Item References (Used When Modifier Adds New Category)")]
    [SerializeField] private ItemDataSO defaultHpPotionItem;
    [SerializeField] private ItemDataSO defaultManaPotionItem;
    [SerializeField] private ItemDataSO defaultStatusCureItem;

    [Header("Function Modifiers (Indexed by (int)AdventurerFunction: None=0, Tank=1, DPS=2, Healer=3, Scout=4)")]
    [SerializeField] private FunctionKitModifierEntry[] functionModifiers = CreateDefaultFunctionArray();

    [Header("Wealth Modifiers (Indexed by (int)AdventurerWealth: Average=0, Poor=1, Rich=2)")]
    [SerializeField] private WealthKitModifierEntry[] wealthModifiers = CreateDefaultWealthArray();

    public ItemDataSO DefaultHpPotionItem { get => defaultHpPotionItem; set => defaultHpPotionItem = value; }
    public ItemDataSO DefaultManaPotionItem { get => defaultManaPotionItem; set => defaultManaPotionItem = value; }
    public ItemDataSO DefaultStatusCureItem { get => defaultStatusCureItem; set => defaultStatusCureItem = value; }

    public static FunctionKitModifierEntry[] CreateDefaultFunctionArray()
    {
        return new FunctionKitModifierEntry[]
        {
            new FunctionKitModifierEntry(AdventurerFunction.None,   0, 0, 0),
            new FunctionKitModifierEntry(AdventurerFunction.Tank,   1, 0, 0),
            new FunctionKitModifierEntry(AdventurerFunction.DPS,    0, 0, 0),
            new FunctionKitModifierEntry(AdventurerFunction.Healer, 0, 1, 0),
            new FunctionKitModifierEntry(AdventurerFunction.Scout,  0, 0, 0)
        };
    }

    public static WealthKitModifierEntry[] CreateDefaultWealthArray()
    {
        return new WealthKitModifierEntry[]
        {
            new WealthKitModifierEntry(AdventurerWealth.Average,  0, 0, 0, null),
            new WealthKitModifierEntry(AdventurerWealth.Poor,    -1, 0, 0, null),
            new WealthKitModifierEntry(AdventurerWealth.Rich,     1, 0, 0, null)
        };
    }

    private FunctionKitModifierEntry GetSingleFunctionModifier(AdventurerFunction singleFunction)
    {
        if (functionModifiers != null)
        {
            for (int i = 0; i < functionModifiers.Length; i++)
            {
                if (functionModifiers[i].Function == singleFunction)
                {
                    return functionModifiers[i];
                }
            }
        }
        for (int i = 0; i < DefaultFunctionTable.Length; i++)
        {
            if (DefaultFunctionTable[i].Function == singleFunction)
            {
                return DefaultFunctionTable[i];
            }
        }
        return DefaultFunctionTable[0];
    }

    private static FunctionKitModifierEntry GetDefaultSingleModifier(AdventurerFunction singleFunction)
    {
        for (int i = 0; i < DefaultFunctionTable.Length; i++)
        {
            if (DefaultFunctionTable[i].Function == singleFunction)
            {
                return DefaultFunctionTable[i];
            }
        }
        return DefaultFunctionTable[0];
    }

    public FunctionKitModifierEntry GetFunctionModifier(AdventurerFunction function)
    {
        if (function == AdventurerFunction.None)
        {
            return GetSingleFunctionModifier(AdventurerFunction.None);
        }

        if (functionModifiers != null)
        {
            for (int i = 0; i < functionModifiers.Length; i++)
            {
                if (functionModifiers[i].Function == function)
                {
                    return functionModifiers[i];
                }
            }
        }

        int hpDelta = 0;
        int manaDelta = 0;
        int cureDelta = 0;
        bool any = false;

        if ((function & AdventurerFunction.Tank) != 0)
        {
            var e = GetSingleFunctionModifier(AdventurerFunction.Tank);
            hpDelta += e.HpPotionDelta;
            manaDelta += e.ManaPotionDelta;
            cureDelta += e.StatusCureDelta;
            any = true;
        }
        if ((function & AdventurerFunction.DPS) != 0)
        {
            var e = GetSingleFunctionModifier(AdventurerFunction.DPS);
            hpDelta += e.HpPotionDelta;
            manaDelta += e.ManaPotionDelta;
            cureDelta += e.StatusCureDelta;
            any = true;
        }
        if ((function & AdventurerFunction.Healer) != 0)
        {
            var e = GetSingleFunctionModifier(AdventurerFunction.Healer);
            hpDelta += e.HpPotionDelta;
            manaDelta += e.ManaPotionDelta;
            cureDelta += e.StatusCureDelta;
            any = true;
        }
        if ((function & AdventurerFunction.Scout) != 0)
        {
            var e = GetSingleFunctionModifier(AdventurerFunction.Scout);
            hpDelta += e.HpPotionDelta;
            manaDelta += e.ManaPotionDelta;
            cureDelta += e.StatusCureDelta;
            any = true;
        }

        if (any)
        {
            return new FunctionKitModifierEntry(function, hpDelta, manaDelta, cureDelta);
        }

        return GetSingleFunctionModifier(AdventurerFunction.None);
    }

    public WealthKitModifierEntry GetWealthModifier(AdventurerWealth wealth)
    {
        int index = Mathf.Clamp((int)wealth, 0, WealthCount - 1);
        if (wealthModifiers != null && index < wealthModifiers.Length)
        {
            return wealthModifiers[index];
        }
        return DefaultWealthTable[index];
    }

    public static FunctionKitModifierEntry GetFunctionModifierOrDefault(ItemKitModifiers modifiers, AdventurerFunction function)
    {
        if (modifiers != null)
        {
            return modifiers.GetFunctionModifier(function);
        }

        if (function == AdventurerFunction.None)
        {
            return GetDefaultSingleModifier(AdventurerFunction.None);
        }

        int hpDelta = 0;
        int manaDelta = 0;
        int cureDelta = 0;
        bool any = false;

        if ((function & AdventurerFunction.Tank) != 0)
        {
            var e = GetDefaultSingleModifier(AdventurerFunction.Tank);
            hpDelta += e.HpPotionDelta;
            manaDelta += e.ManaPotionDelta;
            cureDelta += e.StatusCureDelta;
            any = true;
        }
        if ((function & AdventurerFunction.DPS) != 0)
        {
            var e = GetDefaultSingleModifier(AdventurerFunction.DPS);
            hpDelta += e.HpPotionDelta;
            manaDelta += e.ManaPotionDelta;
            cureDelta += e.StatusCureDelta;
            any = true;
        }
        if ((function & AdventurerFunction.Healer) != 0)
        {
            var e = GetDefaultSingleModifier(AdventurerFunction.Healer);
            hpDelta += e.HpPotionDelta;
            manaDelta += e.ManaPotionDelta;
            cureDelta += e.StatusCureDelta;
            any = true;
        }
        if ((function & AdventurerFunction.Scout) != 0)
        {
            var e = GetDefaultSingleModifier(AdventurerFunction.Scout);
            hpDelta += e.HpPotionDelta;
            manaDelta += e.ManaPotionDelta;
            cureDelta += e.StatusCureDelta;
            any = true;
        }

        if (any)
        {
            return new FunctionKitModifierEntry(function, hpDelta, manaDelta, cureDelta);
        }

        return GetDefaultSingleModifier(AdventurerFunction.None);
    }

    public static WealthKitModifierEntry GetWealthModifierOrDefault(ItemKitModifiers modifiers, AdventurerWealth wealth)
    {
        if (modifiers != null)
        {
            return modifiers.GetWealthModifier(wealth);
        }
        int index = Mathf.Clamp((int)wealth, 0, DefaultWealthTable.Length - 1);
        return DefaultWealthTable[index];
    }

    private void OnValidate()
    {
        if (functionModifiers == null || functionModifiers.Length < FunctionCount)
        {
            var defaults = CreateDefaultFunctionArray();
            if (functionModifiers != null)
            {
                for (int i = 0; i < functionModifiers.Length && i < defaults.Length; i++)
                {
                    defaults[i] = functionModifiers[i];
                }
            }
            functionModifiers = defaults;
        }

        if (wealthModifiers == null || wealthModifiers.Length < WealthCount)
        {
            var defaults = CreateDefaultWealthArray();
            if (wealthModifiers != null)
            {
                for (int i = 0; i < wealthModifiers.Length && i < defaults.Length; i++)
                {
                    defaults[i] = wealthModifiers[i];
                }
            }
            wealthModifiers = defaults;
        }
    }
}
