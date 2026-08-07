using System;

namespace Gloomveil.MapGeneration
{
    public sealed class MapGenerationException : Exception
    {
        public MapGenerationException(string message)
            : base(message)
        {
        }

        public MapGenerationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
