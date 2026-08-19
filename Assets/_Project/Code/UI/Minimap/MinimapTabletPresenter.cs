using Game.Gameplay.Navigation;
using UnityEngine;

namespace Game.UI.Minimap
{
    [DisallowMultipleComponent]
    public sealed class MinimapTabletPresenter : MonoBehaviour
    {
        [SerializeField] private MinimapRuntimeModel runtimeModel;
        [SerializeField] private MinimapTabletGraphic mapGraphic;
        [SerializeField] private RectTransform playerIcon;
        [SerializeField] private RectTransform fuseArrow;

        [Header("Map View")]
        [SerializeField, Min(2f), Tooltip("Map units visible across the tablet at Zoom Level 1.")]
        private float visibleMapUnits = 28f;
        [SerializeField, Range(0.25f, 4f), Tooltip("1 = normal, 2 = twice as close, 0.5 = twice as far.")]
        private float zoomLevel = 1f;

        [Header("Markers")]
        [SerializeField, Range(0.03f, 0.2f)] private float playerSizeRatio = 0.075f;
        [SerializeField, Range(0.03f, 0.2f)] private float arrowSizeRatio = 0.09f;
        [SerializeField, Range(0.05f, 0.45f)] private float arrowOrbitRatio = 0.16f;

        private bool subscribed;

        public MinimapRuntimeModel RuntimeModel => runtimeModel;

        public void Bind(MinimapRuntimeModel model)
        {
            Unsubscribe();
            runtimeModel = model;
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        private void Awake()
        {
            if (runtimeModel == null)
            {
                runtimeModel = GetComponent<MinimapRuntimeModel>();
            }

            if (mapGraphic == null)
            {
                mapGraphic = GetComponentInChildren<MinimapTabletGraphic>(true);
            }

            ApplyViewSettings();
        }

        private void OnEnable()
        {
            ApplyViewSettings();
            Subscribe();
        }
        private void Start() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (subscribed || runtimeModel == null)
            {
                return;
            }

            subscribed = true;
            runtimeModel.SnapshotChanged += ApplySnapshot;
            ApplySnapshot(runtimeModel.Current);
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            runtimeModel.SnapshotChanged -= ApplySnapshot;
            subscribed = false;
        }

        private void ApplySnapshot(MinimapRuntimeSnapshot snapshot)
        {
            if (mapGraphic != null)
            {
                mapGraphic.SetSnapshot(snapshot);
            }

            bool ready = snapshot != null && snapshot.IsReady;
            if (playerIcon != null)
            {
                playerIcon.gameObject.SetActive(ready);
            }

            if (fuseArrow != null)
            {
                fuseArrow.gameObject.SetActive(ready && snapshot.HasFuseDirection);
            }

            if (!ready || mapGraphic == null)
            {
                return;
            }

            float shortSide = Mathf.Min(
                mapGraphic.rectTransform.rect.width,
                mapGraphic.rectTransform.rect.height);
            if (playerIcon != null)
            {
                float playerSize = shortSide * playerSizeRatio;
                playerIcon.sizeDelta = Vector2.one * playerSize;
                playerIcon.anchoredPosition = Vector2.zero;
                playerIcon.localRotation = Quaternion.Euler(0f, 0f, -snapshot.PlayerYawDegrees);
            }

            if (fuseArrow != null && snapshot.HasFuseDirection)
            {
                float arrowSize = shortSide * arrowSizeRatio;
                fuseArrow.sizeDelta = Vector2.one * arrowSize;
                fuseArrow.anchoredPosition = MinimapViewportProjection.ArrowLocalPosition(
                    snapshot.FuseDirection,
                    shortSide * arrowOrbitRatio);
                float angleFromUp = Mathf.Atan2(
                    snapshot.FuseDirection.x,
                    snapshot.FuseDirection.y) * Mathf.Rad2Deg;
                fuseArrow.localRotation = Quaternion.Euler(0f, 0f, -angleFromUp);
            }
        }

        private void ApplyViewSettings()
        {
            if (mapGraphic != null)
            {
                mapGraphic.SetView(visibleMapUnits, zoomLevel);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            visibleMapUnits = Mathf.Max(2f, visibleMapUnits);
            zoomLevel = Mathf.Clamp(zoomLevel, 0.25f, 4f);
            playerSizeRatio = Mathf.Clamp(playerSizeRatio, 0.03f, 0.2f);
            arrowSizeRatio = Mathf.Clamp(arrowSizeRatio, 0.03f, 0.2f);
            arrowOrbitRatio = Mathf.Clamp(arrowOrbitRatio, 0.05f, 0.45f);
            ApplyViewSettings();
        }
#endif
    }
}
