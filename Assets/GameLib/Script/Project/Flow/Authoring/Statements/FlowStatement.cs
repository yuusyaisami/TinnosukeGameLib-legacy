#nullable enable

using System;

namespace Game.Flow
{
    [Serializable]
    public abstract class FlowStatement
    {
        public virtual void EnsureIntegrity() { }
    }
}
