/// <summary>
/// Tactical role/function of a character or monster within a party or encounter.
/// Append-only with explicit integer values.
/// Unspecified (<see cref="None"/>) is treated as <see cref="DPS"/> by AI systems.
/// </summary>
public enum AdventurerFunction
{
    None = 0,
    Tank = 1,
    DPS = 2,
    Healer = 3,
    Scout = 4
}
