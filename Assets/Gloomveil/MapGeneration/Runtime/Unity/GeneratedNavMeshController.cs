using System;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Gloomveil.MapGeneration
{
    [DisallowMultipleComponent]
    public sealed class GeneratedNavMeshController : MonoBehaviour
    {
        [SerializeField] private NavMeshSurface surface;
        [SerializeField, Min(0.1f)] private float sampleRadius = 1.5f;

        private NavMeshData ownedData;

        public Transform ContentRoot => surface != null ? surface.transform : transform;
        public NavMeshSurface Surface => surface;
        public RuntimeValidationReport LastReport { get; private set; }

        public void Configure(
            NavMeshSurface navMeshSurface,
            float validationSampleRadius = 1.5f)
        {
            if (navMeshSurface == null)
            {
                throw new ArgumentNullException(nameof(navMeshSurface));
            }

            if (validationSampleRadius <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(validationSampleRadius));
            }

            Clear();
            surface = navMeshSurface;
            sampleRadius = validationSampleRadius;
            ConfigureSurface();
        }

        public RuntimeValidationReport RebuildAndValidate(GeneratedMapRuntimeData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (surface == null)
            {
                throw new InvalidOperationException("Generated NavMesh has no configured surface.");
            }

            if (!surface.isActiveAndEnabled)
            {
                throw new InvalidOperationException(
                    $"NavMeshSurface '{surface.name}' must be active and enabled before baking.");
            }

            if (data.Root.transform.parent != surface.transform)
            {
                throw new InvalidOperationException(
                    $"Generated root '{data.Root.name}' must be a direct child of " +
                    $"NavMeshSurface '{surface.name}'.");
            }

            foreach (Transform child in surface.transform)
            {
                if (child != data.Root.transform && child.gameObject.activeInHierarchy)
                {
                    throw new InvalidOperationException(
                        $"NavMeshSurface '{surface.name}' has unexpected active child " +
                        $"'{child.name}'. Use it only for generated map geometry.");
                }
            }

            ConfigureSurface();
            ReleaseSurfaceData();
            surface.navMeshData = null;
            try
            {
                surface.BuildNavMesh();
            }
            finally
            {
                ownedData = surface != null ? surface.navMeshData : null;
            }

            if (ownedData == null)
            {
                var failed = new RuntimeValidationReport(data.Seed, data.FloorCount);
                failed.AddError(
                    $"seed={data.Seed} floors={data.FloorCount} " +
                    "source=GeneratedRoot target=NavMesh status=BuildProducedNoData.");
                LastReport = failed;
                return failed;
            }

            LastReport = new RuntimeMapValidator(
                sampleRadius,
                NavMesh.AllAreas,
                surface.agentTypeID).Validate(data);
            return LastReport;
        }

        public void Clear()
        {
            ReleaseSurfaceData();
            LastReport = null;
        }

        private void ConfigureSurface()
        {
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.ignoreNavMeshAgent = true;
            surface.ignoreNavMeshObstacle = true;
        }

        private void ReleaseSurfaceData()
        {
            if (surface != null)
            {
                surface.RemoveData();
                if (ownedData != null && surface.navMeshData == ownedData)
                {
                    surface.navMeshData = null;
                }
            }

            if (ownedData == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(ownedData);
            }
            else
            {
                DestroyImmediate(ownedData);
            }

            ownedData = null;
        }

        private void OnDestroy()
        {
            Clear();
        }
    }
}
