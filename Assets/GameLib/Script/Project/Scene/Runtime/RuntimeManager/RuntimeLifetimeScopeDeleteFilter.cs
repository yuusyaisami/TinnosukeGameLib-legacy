#nullable enable

using System;
using Game.Commands;
using Sirenix.OdinInspector;

namespace Game.Project.Scene.Runtime
{
    [Serializable]
    public struct RuntimeLifetimeScopeDeleteFilter
    {
        [ToggleLeft]
        public bool UseInclude;

        [ShowIf(nameof(UseInclude))]
        [InlineProperty]
        [LabelText("Include")]
        public CommandTargetIdentityFilter Include;

        [ToggleLeft]
        public bool UseExclude;

        [ShowIf(nameof(UseExclude))]
        [InlineProperty]
        [LabelText("Exclude")]
        public CommandTargetIdentityFilter Exclude;

        [ToggleLeft]
        [LabelText("Include Inactive")]
        public bool IncludeInactive;

        public static RuntimeLifetimeScopeDeleteFilter Default => new()
        {
            UseInclude = false,
            UseExclude = false,
            IncludeInactive = true,
        };
    }
}
