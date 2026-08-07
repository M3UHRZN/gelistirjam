using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

using Game.Gameplay.Monster;
namespace Game.Gameplay.DebugTools
{
    /// <summary>Scene-view visualization for AI tuning (MASTER_PROMPT.md section 32). Editor-only visuals.</summary>
    public class MonsterDebugGizmos : MonoBehaviour
    {
        [SerializeField] private MonsterBrain brain;

        public void Bind(MonsterBrain monsterBrain) => brain = monsterBrain;

        private void OnDrawGizmos()
        {
            if (brain == null || brain.Config == null) return;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(brain.transform.position, brain.Config.ContactKillRange);

            Gizmos.color = new Color(1f, 0.6f, 0f, 0.4f);
            Gizmos.DrawWireSphere(brain.PatrolAnchor, brain.Config.PatrolRadius);

            Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
            Gizmos.DrawWireSphere(brain.transform.position, brain.Config.DetectionRadius);

#if UNITY_EDITOR
            int angerValue = brain.Anger != null ? brain.Anger.Anger : 0;
            Handles.Label(brain.transform.position + Vector3.up * 2.2f,
                $"State: {brain.CurrentStateName}\nAnger: {angerValue}/{brain.Config.MaxAnger}");
#endif
        }
    }
}
