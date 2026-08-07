using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// Distance-only player awareness — Bracken is explicitly omniscient about player positions
    /// ("fully aware of the locations of every single employee"), no FOV/line-of-sight gating here.
    /// Gaze detection (whether the *player* is looking back at the monster) is a separate concern
    /// handled by PlayerGazeDetector/MonsterGazeReceiver.
    /// </summary>
    public class MonsterPerception : MonoBehaviour
    {
        [SerializeField] private Transform player;

        // The player CharacterController's pivot is at chest height while the NavMeshAgent pivot
        // is at floor height. AI range and contact decisions are horizontal gameplay distances.
        public float DistanceToPlayer
        {
            get
            {
                if (player == null) return Mathf.Infinity;

                Vector3 delta = player.position - transform.position;
                delta.y = 0f;
                return delta.magnitude;
            }
        }
        public bool IsPlayerWithinRadius(float radius) => DistanceToPlayer <= radius;

        public void Bind(Transform playerTransform) => player = playerTransform;
    }
}
