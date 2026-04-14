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
    public sealed class FireDefinitionTypeSelectorDrawer : ManagedReferenceTypeSelectorDrawerBase<FireDefinition>
    {
        protected override Type DefaultType => typeof(CircleFireDefinition);

        protected override string BuildSummary(FireDefinition? value, InspectorProperty property)
            => FirePatternInspectorLabelUtility.Build(value);
    }
}
#endif