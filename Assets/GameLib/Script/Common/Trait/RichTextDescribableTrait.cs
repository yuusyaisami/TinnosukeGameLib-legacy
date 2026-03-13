#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Trait
{
    public interface IRichTextDescribableTrait
    {
        RichTextTemplateData? Name { get; }
        RichTextTemplateData? Description { get; }
    }

    [Serializable]
    public sealed class RichTextTemplateData
    {
        [ShowInInspector, ReadOnly, LabelText("Name")]
        [PropertyOrder(-10)]
        public string InspectorName => Template;

        [LabelText("Template")]
        [TextArea(3, 10)]
        public string Template = string.Empty;

        [LabelText("Variables")]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = false)]
        public List<ExpressionVariable> Variables = new();
    }
}
