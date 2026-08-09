using System.Collections;
using UnityEngine;

namespace Game.Gameplay.Objectives
{
    /// <summary>
    /// How a fuse presents itself while the player is working it. FuseBox owns fuse *state* and talks
    /// to the world through this interface only, so it never references Cinemachine, DOTween, or the
    /// tablet prefab's concrete component — the same separation that already keeps FuseBox away from
    /// MonsterBrain. The single implementation today is FuseTabletPresenter.
    /// </summary>
    public interface IFuseViewPresenter
    {
        /// <summary>Where the minigame canvas parents itself for the duration of the interaction.</summary>
        Transform ScreenAnchor { get; }

        /// <summary>Player has started working this fuse: take over the camera.</summary>
        void Activate();

        /// <summary>Player is done, one way or another: hand the camera back.</summary>
        void Deactivate();

        /// <summary>
        /// Success-only flourish, played before Deactivate. Cancelled and death-interrupted fuses skip
        /// this entirely. Returns immediately (an empty enumerator) if the implementation has nothing
        /// to play.
        /// </summary>
        IEnumerator PlayCompletion();
    }
}
