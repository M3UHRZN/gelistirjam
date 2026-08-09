using System;

namespace Game.Gameplay.Monster
{
    public readonly struct GeneratedStalkPointMetadata
    {
        private GeneratedStalkPointMetadata(
            bool isNearCorner,
            bool isJunction,
            bool isDeadEnd,
            bool ambushAllowed)
        {
            IsNearCorner = isNearCorner;
            IsJunction = isJunction;
            IsDeadEnd = isDeadEnd;
            AmbushAllowed = ambushAllowed;
        }

        public bool IsNearCorner { get; }
        public bool IsJunction { get; }
        public bool IsDeadEnd { get; }
        public bool AmbushAllowed { get; }

        public static GeneratedStalkPointMetadata Infer(string anchorName, string catalogEntryId)
        {
            bool isStairLanding =
                Contains(anchorName, "StairLanding") || Contains(catalogEntryId, "StairLanding");

            return new GeneratedStalkPointMetadata(
                Contains(anchorName, "Corner") || Contains(catalogEntryId, "Corner"),
                Contains(anchorName, "TJunction") || Contains(catalogEntryId, "TJunction")
                    || Contains(anchorName, "CrossJunction") || Contains(catalogEntryId, "CrossJunction"),
                Contains(anchorName, "DeadEnd") || Contains(catalogEntryId, "DeadEnd"),
                !isStairLanding);
        }

        private static bool Contains(string source, string value) =>
            source?.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
