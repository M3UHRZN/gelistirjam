using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace Gloomveil.MapGeneration
{
    public sealed class RuntimeMapValidator
    {
        private readonly float sampleRadius;
        private readonly NavMeshQueryFilter queryFilter;

        public RuntimeMapValidator(
            float sampleRadius = 1.5f,
            int areaMask = NavMesh.AllAreas,
            int agentTypeId = 0)
        {
            if (sampleRadius <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleRadius));
            }

            this.sampleRadius = sampleRadius;
            queryFilter = new NavMeshQueryFilter
            {
                areaMask = areaMask,
                agentTypeID = agentTypeId
            };
        }

        public RuntimeValidationReport Validate(GeneratedMapRuntimeData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var report = new RuntimeValidationReport(data.Seed, data.FloorCount);
            ValidateAnchorCoverage(data, report);
            ValidatePath(
                data,
                report,
                data.StartAnchor,
                data.ExitAnchor,
                "PlayerStart",
                "Exit");

            for (int index = 0; index < data.FuseAnchors.Count; index++)
            {
                ValidatePath(
                    data,
                    report,
                    data.StartAnchor,
                    data.FuseAnchors[index],
                    "PlayerStart",
                    $"Fuse[{index}]");
            }

            foreach (GeneratedMapModule stair in data.StairModules)
            {
                ValidatePath(
                    data,
                    report,
                    stair.BottomLanding,
                    stair.TopLanding,
                    $"Stair[{stair.PlacementId}].Bottom",
                    $"Stair[{stair.PlacementId}].Top");
            }

            return report;
        }

        private static void ValidateAnchorCoverage(
            GeneratedMapRuntimeData data,
            RuntimeValidationReport report)
        {
            if (data.StartModule == null || data.StartAnchor == null)
            {
                report.AddError(
                    Prefix(data) + " source=PlayerStart target=none status=MissingAnchor.");
            }

            if (data.ExitModule == null || data.ExitAnchor == null)
            {
                report.AddError(
                    Prefix(data) + " source=none target=Exit status=MissingAnchor.");
            }

            int fuseRoleCount = data.RoomModules.Count(module => module.RoomRole == RoomRole.Fuse);
            if (data.FuseAnchors.Count != fuseRoleCount)
            {
                report.AddError(
                    Prefix(data) +
                    $" source=PlayerStart target=Fuse status=AnchorCountMismatch " +
                    $"roles={fuseRoleCount} anchors={data.FuseAnchors.Count}.");
            }

            int safeRoleCount = data.RoomModules.Count(module => module.RoomRole == RoomRole.SafeZone);
            if (data.SafeZoneAnchors.Count != safeRoleCount)
            {
                report.AddError(
                    Prefix(data) +
                    $" source=none target=SafeZone status=AnchorCountMismatch " +
                    $"roles={safeRoleCount} anchors={data.SafeZoneAnchors.Count}.");
            }

            foreach (GeneratedMapModule stair in data.StairModules)
            {
                if (stair.BottomLanding == null || stair.TopLanding == null)
                {
                    report.AddError(
                        Prefix(data) +
                        $" source=Stair[{stair.PlacementId}].Bottom " +
                        $"target=Stair[{stair.PlacementId}].Top status=MissingAnchor.");
                }
            }
        }

        private void ValidatePath(
            GeneratedMapRuntimeData data,
            RuntimeValidationReport report,
            Transform source,
            Transform target,
            string sourceLabel,
            string targetLabel)
        {
            Vector3 sourcePosition = source != null ? source.position : Vector3.zero;
            Vector3 targetPosition = target != null ? target.position : Vector3.zero;
            if (source == null || target == null)
            {
                string missing = source == null && target == null
                    ? "MissingSourceAndTarget"
                    : source == null ? "MissingSource" : "MissingTarget";
                AddPathCheck(
                    data,
                    report,
                    sourceLabel,
                    targetLabel,
                    sourcePosition,
                    targetPosition,
                    Vector3.zero,
                    Vector3.zero,
                    missing,
                    NavMeshPathStatus.PathInvalid,
                    false);
                return;
            }

            if (!NavMesh.SamplePosition(
                    sourcePosition,
                    out NavMeshHit sourceHit,
                    sampleRadius,
                    queryFilter))
            {
                AddPathCheck(
                    data,
                    report,
                    sourceLabel,
                    targetLabel,
                    sourcePosition,
                    targetPosition,
                    Vector3.zero,
                    Vector3.zero,
                    "SourceNotOnNavMesh",
                    NavMeshPathStatus.PathInvalid,
                    false);
                return;
            }

            if (!NavMesh.SamplePosition(
                    targetPosition,
                    out NavMeshHit targetHit,
                    sampleRadius,
                    queryFilter))
            {
                AddPathCheck(
                    data,
                    report,
                    sourceLabel,
                    targetLabel,
                    sourcePosition,
                    targetPosition,
                    sourceHit.position,
                    Vector3.zero,
                    "TargetNotOnNavMesh",
                    NavMeshPathStatus.PathInvalid,
                    false);
                return;
            }

            var path = new NavMeshPath();
            bool calculated = NavMesh.CalculatePath(
                sourceHit.position,
                targetHit.position,
                queryFilter,
                path);
            bool complete = calculated && path.status == NavMeshPathStatus.PathComplete;
            AddPathCheck(
                data,
                report,
                sourceLabel,
                targetLabel,
                sourcePosition,
                targetPosition,
                sourceHit.position,
                targetHit.position,
                calculated ? path.status.ToString() : "CalculationFailed",
                calculated ? path.status : NavMeshPathStatus.PathInvalid,
                complete);
        }

        private static void AddPathCheck(
            GeneratedMapRuntimeData data,
            RuntimeValidationReport report,
            string sourceLabel,
            string targetLabel,
            Vector3 sourcePosition,
            Vector3 targetPosition,
            Vector3 sampledSourcePosition,
            Vector3 sampledTargetPosition,
            string status,
            NavMeshPathStatus pathStatus,
            bool complete)
        {
            string message = Prefix(data) +
                $" source={sourceLabel}@{Format(sourcePosition)}" +
                $" target={targetLabel}@{Format(targetPosition)}" +
                $" status={status} pathStatus={pathStatus}.";
            report.AddPathCheck(new RuntimePathCheck(
                sourceLabel,
                targetLabel,
                sourcePosition,
                targetPosition,
                sampledSourcePosition,
                sampledTargetPosition,
                status,
                pathStatus,
                complete,
                message));
        }

        private static string Prefix(GeneratedMapRuntimeData data) =>
            $"seed={data.Seed} floors={data.FloorCount}";

        private static string Format(Vector3 position) => string.Format(
            CultureInfo.InvariantCulture,
            "({0:0.###},{1:0.###},{2:0.###})",
            position.x,
            position.y,
            position.z);
    }
}
