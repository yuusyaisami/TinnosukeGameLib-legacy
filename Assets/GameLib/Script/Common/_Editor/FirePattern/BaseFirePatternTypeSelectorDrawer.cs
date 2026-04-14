#if UNITY_EDITOR
#nullable enable

using System;
using Game.Common.Editor;
using Game.Fire;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;

namespace Game.Fire.Editor
{
    [DrawerPriority(DrawerPriorityLevel.SuperPriority)]
    public sealed class BaseFirePatternTypeSelectorDrawer : ManagedReferenceTypeSelectorDrawerBase<BaseFirePattern>
    {
        protected override Type DefaultType => typeof(FirePattern);

        protected override string BuildSummary(BaseFirePattern? value, InspectorProperty property)
            => FirePatternInspectorLabelUtility.Build(value);
    }
}
#endif