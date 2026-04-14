#nullable enable
using Game.Commands.VNext;

namespace Game.Common
{
    public static class VarStorePayloadDynamicExtensions
    {
        public static void ApplyTo(this VarStorePayload payload, IVarStore dest, IDynamicContext? context, bool overwrite)
        {
            payload?.ApplyTo(dest, context, overwrite);
        }

        public static VarStore ToVarStore(this VarStorePayload payload, IDynamicContext? context)
        {
            var vars = new VarStore();
            payload?.ApplyTo(vars, context, overwrite: true);
            return vars;
        }
    }
}
