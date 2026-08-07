using System;
using System.Collections.Generic;
using System.Text;

namespace Gloomveil.MapGeneration
{
    public sealed class SeededRandom
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private readonly Random random;

        public SeededRandom(int rootSeed, string streamName)
        {
            random = new Random(DeriveSeed(rootSeed, streamName ?? string.Empty));
        }

        public int Next(int maxExclusive) => random.Next(maxExclusive);

        public int Next(int minInclusive, int maxExclusive) => random.Next(minInclusive, maxExclusive);

        public void Shuffle<T>(IList<T> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            for (int index = values.Count - 1; index > 0; index--)
            {
                int swapIndex = random.Next(index + 1);
                T value = values[index];
                values[index] = values[swapIndex];
                values[swapIndex] = value;
            }
        }

        private static int DeriveSeed(int rootSeed, string streamName)
        {
            unchecked
            {
                uint hash = FnvOffset;
                hash ^= (uint)rootSeed;
                hash *= FnvPrime;
                byte[] bytes = Encoding.UTF8.GetBytes(streamName);
                for (int index = 0; index < bytes.Length; index++)
                {
                    hash ^= bytes[index];
                    hash *= FnvPrime;
                }

                hash += 0x9E3779B9u;
                hash ^= hash >> 16;
                hash *= 0x85EBCA6Bu;
                hash ^= hash >> 13;
                hash *= 0xC2B2AE35u;
                hash ^= hash >> 16;
                return (int)hash;
            }
        }
    }
}
