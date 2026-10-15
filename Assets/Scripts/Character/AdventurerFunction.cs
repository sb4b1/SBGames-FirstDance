/// <summary>
/// Tactical role/function of a character or monster within a party or encounter.
/// Can be combined as bitwise flags for hybrid specializations (e.g. Tank | DPS).
/// Unspecified (<see cref="None"/>) is treated as <see cref="DPS"/> by AI systems.
/// </summary>
[System.Flags]
public enum AdventurerFunction
{
    None   = 0,
    Tank   = 1 << 0, // 1
    DPS    = 1 << 1, // 2
    Healer = 1 << 2, // 4
    Scout  = 1 << 3  // 8
}
