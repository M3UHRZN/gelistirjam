using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Gameplay.Objectives
{
    /// <summary>
    /// Answers one question: which minigame button is under the screen-centre crosshair right now.
    /// The cursor stays locked for the whole fuse interaction, so there is no pointer position to feed
    /// the EventSystem — this fakes one at the viewport centre and asks the world-space canvas's own
    /// GraphicRaycaster. Deliberately owns no visuals and no input handling; FuseMiniGameController
    /// decides what a hover looks like and what a click means.
    /// </summary>
    public class FuseScreenPointer : MonoBehaviour
    {
        [SerializeField, Tooltip("The GraphicRaycaster on the minigame's world-space canvas.")]
        private GraphicRaycaster raycaster;

        private readonly List<RaycastResult> results = new List<RaycastResult>();

        public void Bind(GraphicRaycaster canvasRaycaster) => raycaster = canvasRaycaster;

        /// <summary>
        /// The interactable Button under the viewport centre, or null. Non-interactable buttons are
        /// skipped rather than returned, which makes hover feedback automatically follow the
        /// minigame's own input windows — buttons are only interactable while it is accepting input.
        /// </summary>
        public Button ResolveButtonAtViewportCentre()
        {
            if (raycaster == null || EventSystem.current == null) return null;

            var pointerData = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
            };

            results.Clear();
            raycaster.Raycast(pointerData, results);

            for (int i = 0; i < results.Count; i++)
            {
                Button button = results[i].gameObject.GetComponentInParent<Button>();
                if (button != null && button.interactable) return button;
            }

            return null;
        }
    }
}
