using UnityEngine;

namespace Game.MonsterAI
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

        public float DistanceToPlayer => player != null ? Vector3.Distance(transform.position, player.position) : Mathf.Infinity;
        public bool IsPlayerWithinRadius(float radius) => DistanceToPlayer <= radius;

        public void Bind(Transform playerTransform) => player = playerTransform;
    }
}
