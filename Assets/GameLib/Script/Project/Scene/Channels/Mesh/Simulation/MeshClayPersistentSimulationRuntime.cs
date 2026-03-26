#nullable enable

namespace Game.Channel
{
    sealed class MeshClayPersistentSimulationRuntime : MeshBaseClaySimulationRuntime
    {
        readonly MeshClayPersistentSimulationPreset _preset;

        public MeshClayPersistentSimulationRuntime(MeshClayPersistentSimulationPreset preset)
        {
            _preset = preset;
        }

        protected override float Radius => _preset.Radius;
        protected override float Strength => _preset.ImpactStrength;
        protected override float RecoverSpeed => _preset.RecoverSpeed;
    }
}
