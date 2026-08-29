using System;
using System.Collections.Generic;

namespace DinoRush.Core
{
    // A small, dependency-free, deterministic PRNG (SplitMix64). We do not use System.Random:
    // nothing guarantees its output sequence is identical between the CoreCLR runtime this
    // assembly is tested under and the Mono/IL2CPP runtime Unity ships on device, and a
    // deterministic seed (CLAUDE.md section 15; docs/DECISIONS.md D4) only works if every
    // runtime produces the exact same sequence for the same seed.
    public sealed class SeededRandom
    {
        private ulong _state;

        public SeededRandom(int seed)
        {
            _state = unchecked((ulong)seed * 0x9E3779B97F4A7C15UL) + 0x9E3779B97F4A7C15UL;
        }

        public ulong NextUInt64()
        {
            _state += 0x9E3779B97F4A7C15UL;
            ulong z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        // [0, 1)
        public double NextDouble()
        {
            return (NextUInt64() >> 11) * (1.0 / (1UL << 53));
        }

        // [minInclusive, maxExclusive)
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentException($"{nameof(maxExclusive)} must be greater than {nameof(minInclusive)}.");

            long range = (long)maxExclusive - minInclusive;
            return minInclusive + (int)(NextDouble() * range);
        }

        // Weighted pick from a set of (item, weight) pairs. Weights must be >= 0 and sum > 0.
        public T WeightedPick<T>(IReadOnlyList<(T item, double weight)> options)
        {
            double total = 0;
            for (int i = 0; i < options.Count; i++) total += options[i].weight;
            if (total <= 0)
                throw new ArgumentException("Total weight must be greater than zero.", nameof(options));

            double roll = NextDouble() * total;
            double cumulative = 0;
            for (int i = 0; i < options.Count; i++)
            {
                cumulative += options[i].weight;
                if (roll < cumulative) return options[i].item;
            }
            return options[options.Count - 1].item;
        }
    }
}
