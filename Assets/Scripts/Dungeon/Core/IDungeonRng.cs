namespace SBGames.Dungeon.Core
{
    /// <summary>
    /// Pure C# randomness provider contract for deterministic dungeon generation.
    /// Zero UnityEngine references.
    /// </summary>
    public interface IDungeonRng
    {
        /// <summary>
        /// Returns a random integer in the range [min, max).
        /// </summary>
        int Range(int min, int max);

        /// <summary>
        /// Returns a random float in the range [min, max).
        /// </summary>
        float Range(float min, float max);

        /// <summary>
        /// Returns true with probability chance01 [0..1].
        /// </summary>
        bool Chance(float chance01);
    }

    /// <summary>
    /// Adapter for SB4b1.GameMath.GameRng enabling deterministic generation via the project's GameRng.
    /// </summary>
    public class GameRngDungeonAdapter : IDungeonRng
    {
        private SB4b1.GameMath.GameRng _rng;

        public GameRngDungeonAdapter(uint seed)
        {
            _rng = new SB4b1.GameMath.GameRng(seed == 0 ? 1u : seed);
        }

        public GameRngDungeonAdapter(SB4b1.GameMath.GameRng rng)
        {
            _rng = rng;
        }

        public int Range(int min, int max) => _rng.Range(min, max);
        public float Range(float min, float max) => _rng.Range(min, max);
        public bool Chance(float chance01) => _rng.Chance(chance01);
    }
}
