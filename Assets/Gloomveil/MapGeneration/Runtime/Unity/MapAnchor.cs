using UnityEngine;

namespace Gloomveil.MapGeneration
{
    [DisallowMultipleComponent]
    public sealed class MapAnchor : MonoBehaviour
    {
        [SerializeField] private MapAnchorKind kind;

        public MapAnchorKind Kind => kind;

        public void Configure(MapAnchorKind anchorKind)
        {
            kind = anchorKind;
        }
    }
}
