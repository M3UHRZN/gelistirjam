using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.MonsterAI
{
    /// <summary>Scene-view visualization for AI tuning (MASTER_PROMPT.md section 32, TGD section 53). Editor-only visuals.</summary>
    public class MonsterDebugGizmos : MonoBehaviour
    {
        [SerializeField] private MonsterBrain brain;

        public void Bind(MonsterBrain monsterBrain) => brain = monsterBrain;

        private void OnDrawGizmos()
        {
            if (brain == null || brain.Config == null) return;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(brain.transform.position, brain.Config.CriticalDistance);

#if UNITY_EDITOR
            int angerValue = brain.Anger != null ? brain.Anger.Anger : 0;
            Handles.Label(brain.transform.position + Vector3.up * 2.2f,
                $"State: {brain.CurrentStateName}\nAnger: {angerValue}/{brain.Config.MaxAnger}");
#endif
        }
    }
}
