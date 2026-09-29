using System;

namespace SBGames.Shop.Core
{
    /// <summary>
    /// Contract for random number generation within the shop system.
    /// Enables 100% deterministic simulation, replayability, and automated testing.
    /// </summary>
    public interface IRandomProvider
    {
        /// <summary>
        /// Returns a non-negative random integer that is less than the specified maximum.
        /// </summary>
        int Next(int maxValue);

        /// <summary>
        /// Returns a random integer within a specified range [minValue, maxValue).
        /// </summary>
        int Next(int minValue, int maxValue);

        /// <summary>
        /// Returns a random floating-point number that is greater than or equal to 0.0, and less than 1.0.
        /// </summary>
        float NextFloat();
    }

    /// <summary>
    /// Default System.Random implementation of IRandomProvider.
    /// </summary>
    public class SystemRandomProvider : IRandomProvider
    {
        private readonly Random _random;

        public SystemRandomProvider()
        {
            _random = new Random();
        }

        public SystemRandomProvider(int seed)
        {
            _random = new Random(seed);
        }

        public int Next(int maxValue) => _random.Next(maxValue);
        public int Next(int minValue, int maxValue) => _random.Next(minValue, maxValue);
        public float NextFloat() => (float)_random.NextDouble();
    }
}
