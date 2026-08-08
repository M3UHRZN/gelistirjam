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
                _ => (Color.gray, new Color(0.2f, 0.2f, 0.2f)),
            };

            bodyRenderer.GetPropertyBlock(Block);
            Block.SetColor(BaseColorId, baseColor);
            Block.SetColor(EmissionColorId, emission);
            bodyRenderer.SetPropertyBlock(Block);
        }
    }
}
