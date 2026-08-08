using System.Collections.Generic;
using Game.Gameplay.World;
using NUnit.Framework;
using UnityEngine;

namespace Game.Gameplay.Tests
{
    public sealed class LightDirectorShutdownCurveTests
    {
        [Test]
        public void OnFuseCompleted_DefaultCurve_SmallPool_MatchesExpectedActiveCountsPerStage()
        {
            var fixture = new Fixture(zoneCount: 5);
            try
            {
                var director = new LightDirector();
                director.SetShutdownOrder(fixture.Zones);

                director.OnFuseCompleted();
                Assert.That(fixture.ActiveCount, Is.EqualTo(4), "after Fuse 1 (target 75% of 5)");

                director.OnFuseCompleted();
                Assert.That(fixture.ActiveCount, Is.EqualTo(2), "after Fuse 2 (target 45% of 5)");

                director.OnFuseCompleted();
                Assert.That(fixture.ActiveCount, Is.EqualTo(1), "after Fuse 3 (target 15% of 5)");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void OnFuseCompleted_DefaultCurve_LargePool_MatchesExpectedActiveCountsPerStage()
        {
            var fixture = new Fixture(zoneCount: 61);
            try
            {
                var director = new LightDirector();
                director.SetShutdownOrder(fixture.Zones);

                director.OnFuseCompleted();
                Assert.That(fixture.ActiveCount, Is.EqualTo(46), "after Fuse 1 (target 75% of 61)");

                director.OnFuseCompleted();
                Assert.That(fixture.ActiveCount, Is.EqualTo(27), "after Fuse 2 (target 45% of 61)");

                director.OnFuseCompleted();
                Assert.That(fixture.ActiveCount, Is.EqualTo(9), "after Fuse 3 (target 15% of 61)");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void OnFuseCompleted_BeyondCurveLength_ReusesLastPercent()
        {
            var fixture = new Fixture(zoneCount: 10);
            try
            {
                var director = new LightDirector();
                director.SetShutdownOrder(fixture.Zones);
                director.SetShutdownTargetPercents(new[] { 50f });

                director.OnFuseCompleted(); // stage 1: target 50% of 10 -> 5 active
                Assert.That(fixture.ActiveCount, Is.EqualTo(5), "after Fuse 1 (only curve entry)");

                director.OnFuseCompleted(); // stage 2: curve exhausted, reuse 50% -> already at 5 active, no further shutdown
                Assert.That(fixture.ActiveCount, Is.EqualTo(5), "after Fuse 2 (curve length 1, reuses last entry)");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void SetShutdownTargetPercents_NullOrEmpty_KeepsPriorCurve()
        {
            var fixture = new Fixture(zoneCount: 5);
            try
            {
                var director = new LightDirector();
                director.SetShutdownOrder(fixture.Zones);
                director.SetShutdownTargetPercents(null);
                director.SetShutdownTargetPercents(System.Array.Empty<float>());

                director.OnFuseCompleted();
                Assert.That(fixture.ActiveCount, Is.EqualTo(4), "still using the built-in default curve (75% of 5)");
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void ShutdownAll_DisablesEveryZoneRegardlessOfCurveProgress()
        {
            var fixture = new Fixture(zoneCount: 6);
            try
            {
                var director = new LightDirector();
                director.SetShutdownOrder(fixture.Zones);

                director.OnFuseCompleted(); // partial shutdown only
                director.ShutdownAll();

                Assert.That(fixture.ActiveCount, Is.EqualTo(0));
                foreach (LightZone zone in fixture.Zones)
                {
                    Assert.That(zone.State, Is.EqualTo(LightZoneState.Disabled));
                }
            }
            finally
            {
                fixture.Dispose();
            }
        }

        /// <summary>Builds N live, Active LightZones on their own GameObjects and tears them down on Dispose.</summary>
        private sealed class Fixture
        {
            public readonly List<LightZone> Zones = new List<LightZone>();
            private readonly List<GameObject> gameObjects = new List<GameObject>();

            public Fixture(int zoneCount)
            {
                for (int i = 0; i < zoneCount; i++)
                {
                    var go = new GameObject($"Zone_{i}");
                    gameObjects.Add(go);
                    LightZone zone = go.AddComponent<LightZone>();
                    zone.SetActive(true);
                    Zones.Add(zone);
                }
            }

            public int ActiveCount
            {
                get
                {
                    int count = 0;
                    foreach (LightZone zone in Zones)
                    {
                        if (zone.State == LightZoneState.Active) count++;
                    }
                    return count;
                }
            }

            public void Dispose()
            {
                foreach (GameObject go in gameObjects)
                {
                    if (go != null) Object.DestroyImmediate(go);
                }
            }
        }
    }
}
