using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// A deceptive variant of Shy (TGD section 12): flees like a normal retreat for the first half of
    /// its timer, but if the player never re-establishes eye contact by then, there's a chance it
    /// doubles back via CutOff instead of settling into Patrol — punishing a player who assumes
    /// "it ran off, I'm safe now" too early. Entered only from ShyState (see ShyState's own
    /// mid-timer roll), never a root transition target.
    /// </summary>
    public class FakeRetreatState : IMonsterState
    {
        private const float FaceTurnSpeed = 360f;

        private readonly MonsterBrain brain;
        private float fleeTimer;

        public FakeRetreatState(MonsterBrain brain) => this.brain = brain;

        public void Enter()
        {
            brain.Navigator.SetAutoRotate(false);
            brain.Navigator.Resume();
            brain.Navigator.SetSpeed(brain.Config.ShySpeed);
            brain.Navigator.SetFleeDestinationAwayFrom(brain.Player.position, brain.Config.ShyFleeRadius);
            fleeTimer = brain.Config.ShyFleeDuration * 0.5f;
        }

        public void Tick(float deltaTime)
        {
            if (brain.IsGazeConfirmed)
            {
                // Caught faking it — the deception failed, fall back to a normal observed reaction.
                brain.ChangeState(brain.ObservedState);
                return;
            }

            brain.Navigator.FaceTowards(brain.Player.position, FaceTurnSpeed, deltaTime);

            // Reached the "I ran away" spot without being seen — this is the moment the deception pays
            // off, so double back and cut the player off. (There used to be a `doubledBack` guard here,
            // but it was dead: it was set immediately before the state change and reset in Enter(), so
            // it could never be observed as true.)
            if (brain.Navigator.HasArrived)
            {
                brain.ChangeState(brain.CutOffState);
                return;
            }

            fleeTimer -= deltaTime;
            if (fleeTimer <= 0f)
            {
                brain.ChangeState(brain.IsFinalHunt ? brain.RepositionState : brain.PatrolState);
            }
        }

        public void Exit() { }
    }
}
