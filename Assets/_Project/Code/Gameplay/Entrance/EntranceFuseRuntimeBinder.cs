using Game.Core;
using Game.Gameplay.Objectives;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Gameplay.Entrance
{
    /// <summary>
    /// Rebuilds the interface-typed fuse presenter binding after scene deserialization. The regular
    /// generated-map flow binds spawned fuses at runtime; the authored Entrance fuse instead keeps
    /// these concrete scene references serialized and composes them in Awake.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EntranceFuseRuntimeBinder : MonoBehaviour
    {
        [SerializeField] private FuseBox fuseBox;
        [SerializeField] private GameFlowController gameFlow;
        [SerializeField] private ObjectiveManager objectiveManager;
        [SerializeField] private FuseMiniGameController miniGame;
        [SerializeField] private FuseTabletPresenter presenter;
        [SerializeField] private CinemachineBrain brain;

        private bool configurationErrorLogged;

        private void Awake()
        {
            BindSerializedReferences();
        }

        public bool Configure(
            FuseBox fuse,
            GameFlowController flow,
            ObjectiveManager objectives,
            FuseMiniGameController miniGameController,
            FuseTabletPresenter tabletPresenter,
            CinemachineBrain cinemachineBrain)
        {
            fuseBox = fuse;
            gameFlow = flow;
            objectiveManager = objectives;
            miniGame = miniGameController;
            presenter = tabletPresenter;
            brain = cinemachineBrain;
            configurationErrorLogged = false;
            return BindSerializedReferences();
        }

        public bool BindSerializedReferences()
        {
            if (fuseBox == null
                || gameFlow == null
                || objectiveManager == null
                || miniGame == null
                || presenter == null
                || brain == null)
            {
                if (!configurationErrorLogged)
                {
                    configurationErrorLogged = true;
                    Debug.LogError(
                        "Entrance fuse runtime binding is incomplete. Assign the FuseBox, GameFlowController, "
                        + "ObjectiveManager, FuseMiniGameController, FuseTabletPresenter, and CinemachineBrain.",
                        this);
                }

                return false;
            }

            objectiveManager.SetRequiredCount(1);
            fuseBox.SetRoundsRequired(1);
            presenter.Bind(brain);
            fuseBox.Bind(gameFlow, objectiveManager, miniGame, presenter);
            return true;
        }
    }
}
