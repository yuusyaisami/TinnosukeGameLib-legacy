// Game.Profile.IProfileDefinition.cs
//
// Profile definition interface for ScriptableObject and inline data.

using System;
using System.Collections.Generic;

namespace Game.Profile
{
    public interface IProfileDefinition
    {
        Type ProfileType { get; }
        IEnumerable<IProfileValueBinding> EnumerateBindings();
        void CollectBindings(List<IProfileValueBinding> output);
        int GetBindingCount();
    }
}
