using System;

namespace Gloomveil.MapGeneration
{
    [Serializable]
    public readonly struct FootprintSpec
    {
        public string Id { get; }
        public GridSize2 CanonicalSize { get; }
        public int Weight { get; }
        public bool RequiredPerFloor { get; }

        public FootprintSpec(string id, GridSize2 canonicalSize, int weight, bool requiredPerFloor)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Footprint ID must not be blank.", nameof(id));
            }

            if (weight < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(weight), weight, "Weight must be at least 1.");
            }

            Id = id;
            CanonicalSize = canonicalSize;
            Weight = weight;
            RequiredPerFloor = requiredPerFloor;
        }
    }
}
