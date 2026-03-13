#nullable enable
using Game.Common;
using Game.Vars.Generated;
using VContainer;

namespace Game.Trait
{
    public partial class TraitHolderService
    {
        string _holderKey = string.Empty;

        internal string HolderKey => _holderKey;

        void WriteHolderVarsToBlackboard()
        {
            if (!TryResolveBlackboard(_scope, out var blackboard) || blackboard == null)
                return;

            var vars = blackboard.LocalVars;
            vars.TrySetVariant(VarIds.GameLib.Base.Trait.Holder.holderKey, DynamicVariant.FromString(_holderKey ?? string.Empty));
            vars.TrySetVariant(VarIds.GameLib.Base.Trait.Holder.holderId, DynamicVariant.FromString(_holderId ?? string.Empty));
            vars.TrySetVariant(VarIds.GameLib.Base.Trait.Holder.traitCount, DynamicVariant.FromInt(_traits.Count));
            vars.TrySetVariant(VarIds.GameLib.Base.Trait.Holder.heldCount, DynamicVariant.FromInt(_held.Count));
        }

        static bool TryResolveBlackboard(IScopeNode? scope, out IBlackboardService? blackboard)
        {
            blackboard = null;
            var resolver = scope?.Resolver;
            if (resolver != null && resolver.TryResolve<IBlackboardService>(out var bb) && bb != null)
            {
                blackboard = bb;
                return true;
            }

            return false;
        }
    }
}
