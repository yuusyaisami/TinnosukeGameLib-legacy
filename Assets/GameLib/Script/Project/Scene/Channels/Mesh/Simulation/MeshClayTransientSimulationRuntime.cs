#nullable enable

namespace Game.Channel
{
    sealed class MeshClayTransientSimulationRuntime : MeshBaseClaySimulationRuntime
    {
        readonly MeshClayTransientSimulationPreset _preset;

        public MeshClayTransientSimulationRuntime(MeshClayTransientSimulationPreset preset)
        {
            _preset = preset;
        }

        protected override float Radius => _preset.Radius;
        protected override float Strength => _preset.ImpactStrength;
        protected override float RecoverSpeed => _preset.RecoverSpeed;
    }
}
