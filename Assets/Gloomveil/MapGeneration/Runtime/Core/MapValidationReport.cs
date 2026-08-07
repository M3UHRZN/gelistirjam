using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Gloomveil.MapGeneration
{
    public sealed class MapValidationReport
    {
        public bool IsValid => Errors.Count == 0;
        public IReadOnlyList<string> Errors { get; }

        public MapValidationReport(IEnumerable<string> errors)
        {
            Errors = new ReadOnlyCollection<string>(
                new List<string>(errors ?? throw new ArgumentNullException(nameof(errors))));
        }
    }
}
