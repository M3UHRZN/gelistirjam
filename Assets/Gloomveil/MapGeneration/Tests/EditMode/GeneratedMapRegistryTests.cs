using System;
using NUnit.Framework;
using UnityEngine;

namespace Gloomveil.MapGeneration.Tests
{
    public sealed class GeneratedMapRegistryTests
    {
        [Test]
        public void Clear_RemovesCurrentBeforeRaisingMapCleared()
        {
            var registryObject = new GameObject("Registry");
            var generatedRoot = new GameObject("GeneratedMap");
            GeneratedMapRegistry registry = registryObject.AddComponent<GeneratedMapRegistry>();
            GeneratedMapRuntimeData data = CreateRuntimeData(generatedRoot);
            GeneratedMapRuntimeData observed = null;
            bool wasReadyInsideCallback = true;

            try
            {
                registry.MapCleared += cleared =>
                {
                    observed = cleared;
                    wasReadyInsideCallback = registry.IsReady;
                };

                registry.Publish(data);
                registry.Clear();

                Assert.That(observed, Is.SameAs(data));
                Assert.That(wasReadyInsideCallback, Is.False);
                Assert.That(registry.IsReady, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(registryObject);
                UnityEngine.Object.DestroyImmediate(generatedRoot);
            }
        }

        private static GeneratedMapRuntimeData CreateRuntimeData(GameObject root) =>
            new GeneratedMapRuntimeData(
                root,
                Array.Empty<GeneratedMapModule>(),
                Array.Empty<Transform>(),
                Array.Empty<Transform>(),
                Array.Empty<Transform>(),
                Array.Empty<Transform>(),
                Array.Empty<Transform>(),
                null,
                null,
                null,
                null,
                1042,
                1,
                1,
                2,
                "registry-test");
    }
}
