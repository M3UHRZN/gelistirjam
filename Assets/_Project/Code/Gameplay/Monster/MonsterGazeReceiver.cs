using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// Marker placed on the monster's eye collider (a small trigger). Player gaze raycasts look for this.
    /// </summary>
    public class MonsterGazeReceiver : MonoBehaviour
    {
        [SerializeField] private MonsterBrain owner;
        public MonsterBrain Owner => owner;

        public void Bind(MonsterBrain monsterBrain) => owner = monsterBrain;
    }
}
