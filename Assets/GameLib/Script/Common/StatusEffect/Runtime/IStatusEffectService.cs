#nullable enable

using System.Collections.Generic;
using Game.Commands.VNext;
using Game.Common;

namespace Game.StatusEffect
{
    public sealed class StatusEffectServiceSettingsOverrideRequest
    {
        public bool ApplyGlobalLifetimeSettings;
        public StatusEffectGlobalLifetimeSettings? GlobalLifetimeSettings;
        public bool ApplyGlobalUseCooldownSettings;
        public StatusEffectGlobalUseCooldownSettings? GlobalUseCooldownSettings;
        public bool ApplyGlobalCountSettings;
        public StatusEffectGlobalCountSettings? GlobalCountSettings;
        public bool ResetGlobalState = true;
    }

    public interface IStatusEffectService
    {
        int ActiveEffectCount { get; }

        bool TryApply(StatusEffectApplyRequest request, IDynamicContext? evaluationContext, out string instanceId);
        int Remove(StatusEffectRuntimeFilter filter);
        int SetEnabled(StatusEffectRuntimeFilter filter, bool enabled);
        int SetOperationEnabled(StatusEffectRuntimeFilter filter, string operationId, bool enabled);
        int Use(StatusEffectRuntimeFilter filter, IScopeNode? userScope = null, CommandContext? sourceContext = null);
        int UseGlobal(IScopeNode? userScope = null, CommandContext? sourceContext = null);
        int RestoreState(StatusEffectRuntimeFilter filter, bool restoreGlobalState = false);
        void ClearAll();
        void RefreshServiceSettings(bool resetGlobalState = true);
        void ConfigureServiceSettings(StatusEffectServiceSettingsOverrideRequest request, IDynamicContext? evaluationContext = null);

        bool HasEffect(string definitionId);
        bool IsAnyOperationEnabled(StatusEffectRuntimeFilter filter, string operationId);
        bool TryGetRegisteredDefinition(StatusEffectRuntimeFilter filter, out BaseStatusEffectDefinitionData definition);
        void GetActiveEffectStates(List<EffectState> output);
        void GetStates(List<EffectState> output, StatusEffectRuntimeFilter filter);
    }
}
