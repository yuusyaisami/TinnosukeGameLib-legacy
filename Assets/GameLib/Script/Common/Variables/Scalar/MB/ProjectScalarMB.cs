using UnityEngine;
using VContainer;
using VContainer.Unity;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Game;
using System;

namespace Game.Scalar
{
    public class ProjectScalarMB : BaseScalarMB
    {
        [FoldoutGroup("Debug")]
        [SerializeField, InlineProperty, HideLabel, ShowIf(nameof(enableDebugView))]
        private BindingScalarDebugView _bindingDebugView = new BindingScalarDebugView();

        [Inject]
        protected void Construct(
            IProjectScalarService scalar,
            IScalarBindingManager bindingManager,
            IScalarBindingTelemetry bindingTelemetry)
        {
            if (enableDebugView)
            {
                _bindingDebugView.Initialize(scalar, bindingManager, bindingTelemetry);
            }
        }

    }
}
