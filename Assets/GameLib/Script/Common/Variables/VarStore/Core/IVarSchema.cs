#nullable enable
using System;
using System.Collections.Generic;

namespace Game.Common
{
    [Serializable]
    public struct VarDecl
    {
        public int VarId;
        public string StableKey;
        public ValueKind Kind;
        public DynamicVariant Default;
        public bool AllowImplicitInExpression;
    }

    public interface IVarSchema
    {
        IReadOnlyList<VarDecl> Decls { get; }
        bool TryGetDecl(int varId, out VarDecl decl);
    }
}

