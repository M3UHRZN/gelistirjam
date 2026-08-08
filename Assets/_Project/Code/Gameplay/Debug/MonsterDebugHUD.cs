using UnityEngine;
using UnityEngine.Serialization;

using Game.Core;
using Game.Gameplay.Monster;
using Game.Gameplay.Objectives;
using Game.Gameplay.Player;
namespace Game.Gameplay.DebugTools
{
    /// <summary>
    /// Runtime overlay for playtesting. Stripped from shipping builds via compile directive
    /// (MASTER_PROMPT.md section 34 — logging/HUD text allocation is an accepted debug-only cost).
    /// </summary>
    public class MonsterDebugHUD : MonoBehaviour
    {
        [SerializeField] private MonsterBrain brain;
        [SerializeField] private TestPlayerController player;
        [SerializeField, FormerlySerializedAs("gameManager")] private GameFlowController gameFlow;
        [SerializeField] private ObjectiveManager objectiveManager;

        public void Bind(MonsterBrain monsterBrain, TestPlayerController testPlayer, GameFlowController flow, ObjectiveManager objectives)
        {
            brain = monsterBrain;
            player = testPlayer;
            gameFlow = flow;
            objectiveManager = objectives;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnGUI()
        {
            if (brain == null) return;

            int anger = brain.Anger != null ? brain.Anger.Anger : 0;
            int maxAnger = brain.Config != null ? brain.Config.MaxAnger : 0;
            float aggroChance = brain.Anger != null ? brain.Anger.AggroChance : 0f;
            float distance = brain.Perception != null ? brain.Perception.DistanceToPlayer : 0f;

            GUI.Label(new Rect(10, 10, 460, 20), $"Monster state: {brain.CurrentStateName}");
            GUI.Label(new Rect(10, 30, 460, 20), $"Anger: {anger} / {maxAnger}  (aggro chance on next staredown exit: {aggroChance:P0})");
            GUI.Label(new Rect(10, 50, 460, 20), $"Distance to player: {distance:F2} m");
            GUI.Label(new Rect(10, 70, 460, 20), $"Gazed at now: {brain.IsGazedAtNow}");
            GUI.Label(new Rect(10, 90, 600, 20), $"Last decision: {brain.LastDecisionReason}");

            if (gameFlow != null)
            {
                GUI.Label(new Rect(10, 110, 460, 20), $"Game state: {gameFlow.CurrentStateName}");
            }

            if (objectiveManager != null)
            {
                GUI.Label(new Rect(10, 130, 460, 20), $"Fuses: {objectiveManager.CompletedCount} / {objectiveManager.RequiredCount}");
            }

            if (brain.Config != null && distance <= brain.Config.ContactKillRange)
            {
                GUI.color = Color.red;
                GUI.Label(new Rect(10, 150, 500, 20), $"CONTACT RANGE ({brain.Config.ContactKillRange:F1}m) — instant kill");
                GUI.color = Color.white;
            }

            if (player != null)
            {
                GUI.Label(new Rect(10, 175, 420, 20), $"Stamina: {player.CurrentStamina:F1} / {player.MaxStamina:F1}");
            }

            GUI.Label(new Rect(10, 205, 460, 20), "1-5 = toggle light zones   F = flashlight   Hold E = fuse");
        }
#endif
    }
}
