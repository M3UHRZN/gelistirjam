using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// Placeholder visual feedback until real art/animations exist. Tints the body via a
    /// MaterialPropertyBlock so the shared material is never instanced (MASTER_PROMPT.md section 22).
    /// </summary>
    public class MonsterVisualDriver : MonoBehaviour
    {
        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private MonsterBrain brain;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock block;
        private MaterialPropertyBlock Block => block ??= new MaterialPropertyBlock();

        public void Bind(Renderer renderer, MonsterBrain monsterBrain)
        {
            bodyRenderer = renderer;
            brain = monsterBrain;
        }

        private void OnEnable()
        {
            if (brain != null) brain.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (brain != null) brain.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(string stateName)
        {
            if (bodyRenderer == null) return;

            // Base color for lit conditions; emission so the state still reads clearly in the
            // scene's near-black ambient (a flat albedo tint was invisible in the dark — see chat).
            (Color baseColor, Color emission) = stateName switch
            {
                nameof(PatrolState) => (new Color(0.12f, 0.12f, 0.16f), new Color(0.05f, 0.05f, 0.08f)),
                nameof(InterestedState) => (Color.yellow, new Color(1.2f, 1f, 0f)),
                nameof(ShyState) => (new Color(0.2f, 0.4f, 1f), new Color(0.25f, 0.5f, 1.4f)),
                nameof(AggressiveState) => (Color.red, new Color(1.6f, 0f, 0f)),
                nameof(KillState) => (Color.black, Color.black),

                // TGD section 12/28 expansion (see chat: TGD comparison) — stealthy approach states
                // read as a dim, focused purple; Observed (caught mid-plan) is a sharp orange "startled"
                // flash distinct from Interested's yellow; Commit is the most saturated red in the set,
                // more intense than plain Aggressive since it's the point of no return.
                nameof(RepositionState) or nameof(FlankState) or nameof(AmbushState) or nameof(CutOffState)
                    => (new Color(0.35f, 0.1f, 0.5f), new Color(0.6f, 0.15f, 0.9f)),
                nameof(ObservedState) => (new Color(1f, 0.45f, 0f), new Color(1.8f, 0.6f, 0f)),
                nameof(FakeRetreatState) => (new Color(0.2f, 0.4f, 1f), new Color(0.25f, 0.5f, 1.4f)),
                nameof(SafeZoneCampState) => (new Color(0.15f, 0.5f, 0.45f), new Color(0.2f, 0.9f, 0.8f)),
                nameof(CommitState) => (new Color(0.6f, 0f, 0f), new Color(2f, 0f, 0f)),

                _ => (Color.gray, new Color(0.2f, 0.2f, 0.2f)),
            };

            bodyRenderer.GetPropertyBlock(Block);
            Block.SetColor(BaseColorId, baseColor);
            Block.SetColor(EmissionColorId, emission);
            bodyRenderer.SetPropertyBlock(Block);
        }
    }
}
