using UnityEngine;

namespace Game.MonsterAI
{
    /// <summary>
    /// Runtime overlay for playtesting. Stripped from shipping builds via compile directive
    /// (MASTER_PROMPT.md section 34 — logging/HUD text allocation is an accepted debug-only cost).
    /// </summary>
    public class MonsterDebugHUD : MonoBehaviour
    {
        [SerializeField] private MonsterBrain brain;
        [SerializeField] private TestPlayerController player;

        public void Bind(MonsterBrain monsterBrain, TestPlayerController testPlayer)
        {
            brain = monsterBrain;
            player = testPlayer;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnGUI()
        {
            if (brain == null) return;

            int anger = brain.Anger != null ? brain.Anger.Anger : 0;
            int maxAnger = brain.Config != null ? brain.Config.MaxAnger : 0;
            float aggroChance = brain.Anger != null ? brain.Anger.AggroChance : 0f;
            float distance = brain.Perception != null ? brain.Perception.DistanceToPlayer : 0f;

            GUI.Label(new Rect(10, 10, 460, 20), $"State: {brain.CurrentStateName}");
            GUI.Label(new Rect(10, 30, 460, 20), $"Anger: {anger} / {maxAnger}  (aggro chance on next staredown exit: {aggroChance:P0})");
            GUI.Label(new Rect(10, 50, 460, 20), $"Distance to player: {distance:F2} m");
            GUI.Label(new Rect(10, 70, 460, 20), $"Gazed at now: {brain.IsGazedAtNow}");

            if (brain.Config != null && distance <= brain.Config.ContactKillRange)
            {
                GUI.color = Color.red;
                GUI.Label(new Rect(10, 90, 500, 20), $"CONTACT RANGE ({brain.Config.ContactKillRange:F1}m) — instant kill");
                GUI.color = Color.white;
            }

            if (player != null)
            {
                GUI.Label(new Rect(10, 115, 420, 20), $"Stamina: {player.CurrentStamina:F1} / {player.MaxStamina:F1}");
            }

            GUI.Label(new Rect(10, 145, 460, 20), "1/2/3/4 = toggle light zones   F = flashlight (visibility only, no AI effect)");
        }
#endif
    }
}
