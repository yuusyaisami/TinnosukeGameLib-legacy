#if UNITY_EDITOR
#nullable enable

using System;
using Game.Common.Editor;
using Game.DI;
using Game.Fire;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;

namespace Game.Fire.Editor
{
    [DrawerPriority(DrawerPriorityLevel.SuperPriority)]
    public sealed class FirePatternRuntimeTemplatePresetTypeSelectorDrawer : ManagedReferenceTypeSelectorDrawerBase<FirePatternRuntimeTemplatePreset>
    {
        protected override Type DefaultType => typeof(FirePatternRuntimeTemplatePreset);

        protected override string BuildSummary(FirePatternRuntimeTemplatePreset? value, InspectorProperty property)
            => FirePatternInspectorLabelUtility.Build(value);
    }
}
#endif