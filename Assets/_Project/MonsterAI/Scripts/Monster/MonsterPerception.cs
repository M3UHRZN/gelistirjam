using UnityEngine;

namespace Game.MonsterAI
{
    /// <summary>Reads the player's position and vision model on the monster's behalf (TGD section 13).</summary>
    public class MonsterPerception : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private PlayerVisionModel playerVision;

        public float DistanceToPlayer => player != null
            ? Vector3.Distance(transform.position, player.position)
            : Mathf.Infinity;

        public void Bind(Transform playerTransform, PlayerVisionModel visionModel)
        {
            player = playerTransform;
            playerVision = visionModel;
        }

        public bool IsPointActuallyVisibleToPlayer(Vector3 point) =>
            playerVision != null && playerVision.IsActuallyVisible(point);
    }
}
