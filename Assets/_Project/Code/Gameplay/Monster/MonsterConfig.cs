using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// Tunable values for the Bracken-style monster AI (see chat: full rewrite based on Lethal
    /// Company's Bracken — wiki behaviour description + "Coding the Bracken" tutorial transcripts).
    /// Speed values are deliberately placed so the player is slower than the monster while walking
    /// but faster while sprinting (walk 3.75 m/s, sprint 6.5 m/s in TestPlayerController) — that gap
    /// is the entire tension of the chase, exactly like the source material.
    /// </summary>
    /// <summary>
    /// Per-anger-bracket behaviour tuning (TGD section 20). Previously AngerStage was computed but
    /// read by nothing at all, so the four documented brackets had zero behavioural effect (see chat).
    /// One instance per AngerStage value, edited in the Inspector per MASTER_PROMPT/TGD section 52.
    /// </summary>
    [System.Serializable]
    public class AngerStageTuning
    {
        [Tooltip("Scales StalkBehindDistance. TGD section 20: calm = 'daha uzaktan stalk', furious = 'yakın approach'.")]
        public float StalkDistanceMultiplier = 1f;
        [Tooltip("Scales RepositionChance — TGD's 'daha fazla flank / daha fazla ambush' as anger climbs.")]
        public float RepositionChanceMultiplier = 1f;
        [Tooltip("Scales RepositionInterval — TGD: 'daha uzun/kısa reposition cooldown'.")]
        public float RepositionIntervalMultiplier = 1f;
        [Range(0f, 1f), Tooltip("Chance a reposition becomes a CutOff instead of a Flank/Ambush. TGD: 'cut-off artar' at high anger.")]
        public float CutOffPreference;
        [Tooltip("Scales FakeRetreatChance — TGD section 20: 'daha fazla fake retreat' at 9+.")]
        public float FakeRetreatChanceMultiplier = 1f;
        [Range(0f, 1f), Tooltip("How much of the 'player can see this point' scoring penalty is ignored. TGD section 17: 'yüksek Anger seviyesinde daha riskli hareketler yapabilir'.")]
        public float VisibilityRiskTolerance;
    }

    [CreateAssetMenu(fileName = "MonsterConfig", menuName = "Gloomveil/Monster Config")]
    public class MonsterConfig : ScriptableObject
    {
        [Header("Patrol")]
        [SerializeField] private float patrolSpeed = 1.8f;
        [SerializeField, Tooltip("Radius around its patrol anchor it wanders within when no player is near.")]
        private float patrolRadius = 10f;
        [SerializeField, Tooltip("Player within this distance switches Patrol -> Interested (omniscient — no line-of-sight check, Bracken always knows where players are).")]
        private float detectionRadius = 12f;

        [Header("Interested")]
        [SerializeField] private float interestedSpeed = 4.2f;
        [SerializeField, Tooltip("How far behind the player's facing direction the stalker tries to path. It never deliberately crosses in front of the player.")]
        private float stalkBehindDistance = 3f;
        [SerializeField, Tooltip("How far the player must move/turn before the stalk route is reconsidered.")]
        private float stalkRepathInterval = 0.45f;
        [SerializeField, Tooltip("Player must be farther than this to lose interest and return to Patrol (hysteresis vs detectionRadius).")]
        private float loseInterestRadius = 16f;

        [Header("Shy (retreat)")]
        [SerializeField] private float shySpeed = 5.5f;
        [SerializeField] private float shyFleeRadius = 14f;
        [SerializeField, Tooltip("How long it keeps fleeing/hiding once gaze breaks before returning to Patrol (Bracken: 24 seconds).")]
        private float shyFleeDuration = 24f;
        [SerializeField, Tooltip("Seconds of continuous staredown (while frozen in Shy) per +1 Anger.")]
        private float staredownAngerInterval = 1f;
        [SerializeField, Tooltip("How often an observed retreating monster may stop for a staredown.")]
        private float staredownCheckInterval = 1f;
        [SerializeField, Range(0f, 1f), Tooltip("Chance of beginning a staredown at each check while the player is watching its retreat.")]
        private float staredownChance = 0.35f;

        [Header("Aggressive (chase)")]
        [SerializeField] private float aggressiveSpeed = 4.8f;
        [SerializeField, Tooltip("Losing sight of the player for longer than this while chasing reverts to Shy.")]
        private float loseAggroRadius = 20f;
        [SerializeField, Tooltip("Anger points lost per second while chasing — hitting 0 reverts to Shy (TGD/Bracken: this is the only way anger resets mid-chase).")]
        private float aggressiveAngerDecayPerSecond = 1f;
        [SerializeField, Tooltip("Distance at which physical contact kills the player, in any state (Bracken: instant neck-snap on contact).")]
        private float contactKillRange = 1f;

        [Header("Gaze")]
        [SerializeField, Range(3, 9)] private int gazeRayCount = 5;
        [SerializeField, Tooltip("Of the rays cast, how many must hit the eyes for a look to register.")]
        private int gazeRequiredHits = 3;
        [SerializeField, Tooltip("Half-angle in degrees of the ray cluster spread around screen center.")]
        private float gazeConeHalfAngleDegrees = 3f;
        [SerializeField] private float gazeMaxDistance = 25f;
        [SerializeField, Tooltip("A single continuous stare this long forces Aggressive outright, bypassing the anger-chance roll (\"staring too long WILL retaliate\").")]
        private float gazeAggroHardCap = 4f;

        [Header("Anger")]
        [SerializeField, Tooltip("Anger points added the instant a glance is first registered.")]
        private int angerPerSpotted = 1;
        [SerializeField] private int maxAnger = 12;
        [SerializeField, Tooltip("TGD section 21: speed multiplier applied at max anger (1 = no scaling). Interpolated linearly by current AggroChance.")]
        private float angerSpeedMultiplierMax = 1.75f; // TGD section 21 recommends Base x 1.7-1.8
        [SerializeField, Tooltip("TGD section 25: inside this distance, gaze/freeze reactions stop mattering — the monster commits to a final rush instead. Must stay larger than ContactKillRange.")]
        private float criticalDistance = 2.5f;
        [SerializeField, Tooltip("TGD section 24: seconds of continuous gaze required before it counts as a confirmed look (vs a single-frame accidental hit). Bracken's original design used instant single-frame reaction (see chat) — this restores the TGD's own spec.")]
        private float gazeConfirmDurationSeconds = 0.2f;

        [Header("Reposition / Flank / Ambush / CutOff (TGD section 12)")]
        [SerializeField] private float repositionSpeed = 4.5f;
        [SerializeField, Tooltip("Chance per Interested repath tick to break off into a Reposition attempt instead of plain behind-stalking, once StalkPoints are bound.")]
        [Range(0f, 1f)] private float repositionChance = 0.15f;
        [SerializeField, Tooltip("How far ahead in time to predict the player's position for CutOff/scoring.")]
        private float predictionSeconds = 1.5f;
        [SerializeField, Range(0.4f, 0.8f), Tooltip("How often a repositioning monster re-scores candidate StalkPoints (TGD section 14: 0.4-0.8s — explicitly NOT every frame).")]
        private float repositionInterval = 0.6f;
        [SerializeField, Tooltip("Lateral offset distance used when picking a Flank point relative to the player's path.")]
        private float flankOffsetDistance = 5f;
        [SerializeField, Tooltip("Radius around an Ambush point that triggers a Commit rush once the player enters it.")]
        private float ambushTriggerRadius = 6f;
        [SerializeField, Tooltip("How long an unsprung ambush waits before giving up and repositioning somewhere better.")]
        private float ambushMaxDurationSeconds = 12f;
        [SerializeField, Tooltip("If the player strays further than this while the monster is Repositioning/Flanking/Ambushing/CutOff, give up and fall back to Interested.")]
        private float stalkAbandonRadius = 22f;

        [Header("Commit (TGD section 12, 25)")]
        [SerializeField] private float commitSpeed = 7.5f;
        [SerializeField, Tooltip("Distance to the player that triggers an irreversible Commit rush from Ambush/CutOff.")]
        private float commitDistance = 5f;
        [SerializeField, Tooltip("Losing the player beyond this distance during Commit reverts to Aggressive (still chasing, just no longer irreversible).")]
        private float commitLoseRadius = 12f;

        [Header("Safe Zone Camping (TGD section 30)")]
        [SerializeField, Tooltip("How far outside a SafeZone the monster lurks while the player is sheltering inside it. Must stay outside the zone itself — the navigator rejects in-zone destinations regardless.")]
        private float safeZoneCampDistance = 6f;
        [SerializeField, Tooltip("How long it will camp a SafeZone before losing patience and returning to Patrol.")]
        private float safeZoneCampDuration = 20f;
        [SerializeField, Tooltip("How often the camping monster re-picks its waiting spot, so it drifts between exits instead of standing at one.")]
        private float safeZoneCampRepositionInterval = 3f;

        [Header("Observed / Fake Retreat")]
        [SerializeField, Tooltip("Chance, when a Shy flee's timer passes its midpoint unobserved, of doubling back into CutOff instead of returning to Patrol.")]
        [Range(0f, 1f)] private float fakeRetreatChance = 0.25f;

        [Header("Anger Stages (TGD section 20)")]
        [SerializeField, Tooltip("One entry per AngerStage: Calm (0-25%), Wary (25-50%), Agitated (50-75%), Furious (75%+).")]
        private AngerStageTuning[] angerStageTuning =
        {
            // Calm — "Monster sabırlıdır": farther stalk, low flank rate, long cooldown, no cut-off.
            new AngerStageTuning { StalkDistanceMultiplier = 1.35f, RepositionChanceMultiplier = 0.5f, RepositionIntervalMultiplier = 1.5f, CutOffPreference = 0f,    FakeRetreatChanceMultiplier = 0.5f, VisibilityRiskTolerance = 0f },
            // Wary — "daha yakın stalk, daha fazla flank, daha fazla ambush".
            new AngerStageTuning { StalkDistanceMultiplier = 1f,    RepositionChanceMultiplier = 1f,   RepositionIntervalMultiplier = 1f,   CutOffPreference = 0.15f, FakeRetreatChanceMultiplier = 1f,   VisibilityRiskTolerance = 0.2f },
            // Agitated — "cut-off artar, daha kısa reposition süresi".
            new AngerStageTuning { StalkDistanceMultiplier = 0.85f, RepositionChanceMultiplier = 1.4f, RepositionIntervalMultiplier = 0.8f, CutOffPreference = 0.45f, FakeRetreatChanceMultiplier = 1.3f, VisibilityRiskTolerance = 0.5f },
            // Furious — "yakın approach, daha riskli FOV geçişleri, daha fazla fake retreat, aggressive cut-off".
            new AngerStageTuning { StalkDistanceMultiplier = 0.7f,  RepositionChanceMultiplier = 1.8f, RepositionIntervalMultiplier = 0.6f, CutOffPreference = 0.7f,  FakeRetreatChanceMultiplier = 1.8f, VisibilityRiskTolerance = 0.85f },
        };

        [Header("Fuse Awareness (TGD section 36)")]
        [SerializeField, Tooltip("Radius around a fuse box the monster tries to loiter within while it's being worked.")]
        private float fuseApproachRadius = 8f;
        [SerializeField, Tooltip("Passive anger gain per second while the player works a fuse and the monster isn't already Aggressive.")]
        private float fuseAwarenessAngerPerSecond = 0.25f;

        public float PatrolSpeed => patrolSpeed;
        public float PatrolRadius => patrolRadius;
        public float DetectionRadius => detectionRadius;

        public float InterestedSpeed => interestedSpeed;
        public float StalkBehindDistance => stalkBehindDistance;
        public float StalkRepathInterval => stalkRepathInterval;
        public float LoseInterestRadius => loseInterestRadius;

        public float ShySpeed => shySpeed;
        public float ShyFleeRadius => shyFleeRadius;
        public float ShyFleeDuration => shyFleeDuration;
        public float StaredownAngerInterval => staredownAngerInterval;
        public float StaredownCheckInterval => staredownCheckInterval;
        public float StaredownChance => staredownChance;

        public float AggressiveSpeed => aggressiveSpeed;
        public float LoseAggroRadius => loseAggroRadius;
        public float AggressiveAngerDecayPerSecond => aggressiveAngerDecayPerSecond;
        public float ContactKillRange => contactKillRange;

        public int GazeRayCount => gazeRayCount;
        public int GazeRequiredHits => gazeRequiredHits;
        public float GazeConeHalfAngleDegrees => gazeConeHalfAngleDegrees;
        public float GazeMaxDistance => gazeMaxDistance;
        public float GazeAggroHardCap => gazeAggroHardCap;

        public int AngerPerSpotted => angerPerSpotted;
        public int MaxAnger => maxAnger;
        public float AngerSpeedMultiplierMax => angerSpeedMultiplierMax;
        public float CriticalDistance => criticalDistance;
        public float GazeConfirmDurationSeconds => gazeConfirmDurationSeconds;

        public float FuseApproachRadius => fuseApproachRadius;
        public float FuseAwarenessAngerPerSecond => fuseAwarenessAngerPerSecond;

        public float RepositionSpeed => repositionSpeed;
        public float RepositionChance => repositionChance;
        public float PredictionSeconds => predictionSeconds;
        public float RepositionInterval => repositionInterval;

        /// <summary>Tuning for the given anger bracket (TGD section 20). Falls back to neutral defaults if the array was shortened in the Inspector.</summary>
        public AngerStageTuning TuningFor(AngerStage stage)
        {
            int index = (int)stage;
            if (angerStageTuning == null || index < 0 || index >= angerStageTuning.Length) return NeutralTuning;
            return angerStageTuning[index] ?? NeutralTuning;
        }

        private static readonly AngerStageTuning NeutralTuning = new AngerStageTuning();
        public float FlankOffsetDistance => flankOffsetDistance;
        public float AmbushTriggerRadius => ambushTriggerRadius;
        public float AmbushMaxDurationSeconds => ambushMaxDurationSeconds;
        public float StalkAbandonRadius => stalkAbandonRadius;

        public float SafeZoneCampDistance => safeZoneCampDistance;
        public float SafeZoneCampDuration => safeZoneCampDuration;
        public float SafeZoneCampRepositionInterval => safeZoneCampRepositionInterval;

        public float CommitSpeed => commitSpeed;
        public float CommitDistance => commitDistance;
        public float CommitLoseRadius => commitLoseRadius;

        public float FakeRetreatChance => fakeRetreatChance;

        private void OnValidate()
        {
            patrolSpeed = Mathf.Max(0.1f, patrolSpeed);
            patrolRadius = Mathf.Max(0.5f, patrolRadius);
            detectionRadius = Mathf.Max(0.1f, detectionRadius);
            interestedSpeed = Mathf.Max(0.1f, interestedSpeed);
            stalkBehindDistance = Mathf.Max(0.5f, stalkBehindDistance);
            stalkRepathInterval = Mathf.Max(0.05f, stalkRepathInterval);
            shySpeed = Mathf.Max(0.1f, shySpeed);
            shyFleeRadius = Mathf.Max(0.5f, shyFleeRadius);
            shyFleeDuration = Mathf.Max(0.1f, shyFleeDuration);
            staredownAngerInterval = Mathf.Max(0.05f, staredownAngerInterval);
            staredownCheckInterval = Mathf.Max(0.05f, staredownCheckInterval);
            aggressiveSpeed = Mathf.Max(0.1f, aggressiveSpeed);
            loseAggroRadius = Mathf.Max(0.1f, loseAggroRadius);
            aggressiveAngerDecayPerSecond = Mathf.Max(0.01f, aggressiveAngerDecayPerSecond);
            loseInterestRadius = Mathf.Max(detectionRadius, loseInterestRadius);
            gazeRequiredHits = Mathf.Clamp(gazeRequiredHits, 1, gazeRayCount);
            gazeMaxDistance = Mathf.Max(0.1f, gazeMaxDistance);
            gazeAggroHardCap = Mathf.Max(0.1f, gazeAggroHardCap);
            angerPerSpotted = Mathf.Max(1, angerPerSpotted);
            maxAnger = Mathf.Max(1, maxAnger);
            contactKillRange = Mathf.Max(0.1f, contactKillRange);

            repositionSpeed = Mathf.Max(0.1f, repositionSpeed);
            predictionSeconds = Mathf.Max(0.1f, predictionSeconds);
            flankOffsetDistance = Mathf.Max(0.5f, flankOffsetDistance);
            ambushTriggerRadius = Mathf.Max(0.5f, ambushTriggerRadius);
            stalkAbandonRadius = Mathf.Max(loseInterestRadius, stalkAbandonRadius);
            commitSpeed = Mathf.Max(0.1f, commitSpeed);
            commitDistance = Mathf.Max(0.5f, commitDistance);
            commitLoseRadius = Mathf.Max(commitDistance, commitLoseRadius);

            angerSpeedMultiplierMax = Mathf.Max(1f, angerSpeedMultiplierMax);
            repositionInterval = Mathf.Clamp(repositionInterval, 0.4f, 0.8f); // TGD section 14's stated band
            ambushMaxDurationSeconds = Mathf.Max(1f, ambushMaxDurationSeconds);
            safeZoneCampDistance = Mathf.Max(1f, safeZoneCampDistance);
            safeZoneCampDuration = Mathf.Max(1f, safeZoneCampDuration);
            safeZoneCampRepositionInterval = Mathf.Max(0.5f, safeZoneCampRepositionInterval);
            criticalDistance = Mathf.Max(contactKillRange + 0.1f, criticalDistance);
            gazeConfirmDurationSeconds = Mathf.Max(0f, gazeConfirmDurationSeconds);
        }
    }
}
