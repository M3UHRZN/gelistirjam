using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.AI;

namespace Gloomveil.MapGeneration
{
    public readonly struct RuntimePathCheck
    {
        public string SourceLabel { get; }
        public string TargetLabel { get; }
        public Vector3 SourcePosition { get; }
        public Vector3 TargetPosition { get; }
        public Vector3 SampledSourcePosition { get; }
        public Vector3 SampledTargetPosition { get; }
        public string Status { get; }
        public NavMeshPathStatus PathStatus { get; }
        public bool IsComplete { get; }
        public string Message { get; }

        public RuntimePathCheck(
            string sourceLabel,
            string targetLabel,
            Vector3 sourcePosition,
            Vector3 targetPosition,
            Vector3 sampledSourcePosition,
            Vector3 sampledTargetPosition,
            string status,
            NavMeshPathStatus pathStatus,
            bool isComplete,
            string message)
        {
            SourceLabel = sourceLabel ?? throw new ArgumentNullException(nameof(sourceLabel));
            TargetLabel = targetLabel ?? throw new ArgumentNullException(nameof(targetLabel));
            SourcePosition = sourcePosition;
            TargetPosition = targetPosition;
            SampledSourcePosition = sampledSourcePosition;
            SampledTargetPosition = sampledTargetPosition;
            Status = status ?? throw new ArgumentNullException(nameof(status));
            PathStatus = pathStatus;
            IsComplete = isComplete;
            Message = message ?? string.Empty;
        }
    }

    public sealed class RuntimeValidationReport
    {
        private readonly List<string> errors = new List<string>();
        private readonly List<RuntimePathCheck> pathChecks = new List<RuntimePathCheck>();

        public int Seed { get; }
        public int FloorCount { get; }
        public bool IsValid => errors.Count == 0;
        public IReadOnlyList<string> Errors =>
            new ReadOnlyCollection<string>(errors);
        public IReadOnlyList<RuntimePathCheck> PathChecks =>
            new ReadOnlyCollection<RuntimePathCheck>(pathChecks);

        public RuntimeValidationReport(int seed, int floorCount)
        {
            Seed = seed;
            FloorCount = floorCount;
        }

        internal void AddError(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                throw new ArgumentException("Runtime validation error must not be empty.", nameof(error));
            }

            errors.Add(error);
        }

        internal void AddPathCheck(RuntimePathCheck check)
        {
            pathChecks.Add(check);
            if (!check.IsComplete)
            {
                AddError(check.Message);
            }
        }
    }
}
