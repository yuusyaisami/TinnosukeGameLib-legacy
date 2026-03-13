// Binding telemetry only: lists active scalar bindings.
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace Game.Scalar
{
    [Serializable]
    public sealed class BindingScalarDebugView
    {
        [NonSerialized] IProjectScalarService _scalar;
        [NonSerialized] IScalarBindingManager _bindingManager;
        [NonSerialized] IScalarBindingTelemetry _bindingTelemetry;

        bool _initialized;

        public void Initialize(
            IProjectScalarService scalar,
            IScalarBindingManager bindingManager,
            IScalarBindingTelemetry bindingTelemetry)
        {
            _scalar = scalar;
            _bindingManager = bindingManager;
            _bindingTelemetry = bindingTelemetry;
            _initialized = scalar != null && bindingManager != null && bindingTelemetry != null;
        }

        [FoldoutGroup("Bindings"), ShowInInspector, ReadOnly]
        [LabelText("Active Bindings")]
        public IEnumerable<ScalarBindingDebugInfo> ActiveBindings
        {
            get
            {
                if (!_initialized || _bindingTelemetry == null)
                    return Array.Empty<ScalarBindingDebugInfo>();

                return _bindingTelemetry.GetBindings();
            }
        }
    }
}
