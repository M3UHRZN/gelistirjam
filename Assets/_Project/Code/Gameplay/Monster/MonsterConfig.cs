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
        }
    }
}
