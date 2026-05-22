using System;

namespace Game.Common
{
    internal static class BlackboardPayloadProjectionUtility
    {
        public static IVarStore ProjectCommandVars(VarStore payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            var commandVars = new VarStore(initialCapacity: 32);
            payload.MergeInto(commandVars, overwrite: true);
            return commandVars;
        }
    }
}