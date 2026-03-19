#nullable enable

using System.Collections.Generic;
using Game.Commands.VNext;
using Game.Common;

namespace Game.StatusEffect
{
    public interface IStatusEffectService
    {
        int ActiveEffectCount { get; }

        bool TryApply(StatusEffectApplyRequest request, IDynamicContext? evaluationContext, out string instanceId);
        int Remove(StatusEffectRuntimeFilter filter);
        int SetEnabled(StatusEffectRuntimeFilter filter, bool enabled);
        int Use(StatusEffectRuntimeFilter filter, IScopeNode? userScope = null, CommandContext? sourceContext = null);
        int Reset(StatusEffectRuntimeFilter filter);
        void ClearAll();

        bool HasEffect(string definitionId);
        void GetActiveEffectStates(List<EffectState> output);
        void GetStates(List<EffectState> output, StatusEffectRuntimeFilter filter);
    }
}
