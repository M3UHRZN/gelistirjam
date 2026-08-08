using Gloomveil.MapGeneration;
using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// Places the player at the generated map's Anchor_PlayerStart when a map is published.
    /// Map generation reaches gameplay only through GeneratedMapRegistry and neutral anchors.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerSpawnGate : MonoBehaviour
    {
        [SerializeField] private GeneratedMapRegistry registry;
        [SerializeField] private PlayerController playerController;

        private bool subscribed;

        public void Bind(GeneratedMapRegistry mapRegistry, PlayerController player)
        {
            registry = mapRegistry;
            playerController = player;
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        private void OnEnable() => Subscribe();

        private void Start() => Subscribe();

        private void OnDisable()
        {
            if (!subscribed)
            {
                return;
            }

            registry.MapReady -= OnMapReady;
            subscribed = false;
        }

        private void Subscribe()
        {
            if (subscribed || registry == null || playerController == null)
            {
                return;
            }

            subscribed = true;
            registry.MapReady += OnMapReady;
            if (registry.TryGetCurrent(out GeneratedMapRuntimeData data))
            {
                SpawnAt(data);
            }
        }

        private void OnMapReady(GeneratedMapRuntimeData data) => SpawnAt(data);

        private void SpawnAt(GeneratedMapRuntimeData data) =>
            playerController.Teleport(data.StartAnchor.position, data.StartAnchor.eulerAngles.y);
    }
}
