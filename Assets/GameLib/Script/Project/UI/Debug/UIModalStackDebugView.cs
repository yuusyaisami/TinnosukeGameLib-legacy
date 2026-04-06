#nullable enable
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace Game.UI
{
    [Serializable]
    public sealed class UIModalStackDebugView : IDisposable
    {
        [ShowInInspector, ReadOnly]
        public string CurrentInputRoot = "(none)";

        [ShowInInspector, ReadOnly]
        public int Depth = 0;

        [ShowInInspector, ReadOnly]
        public string[] StackModalIds = Array.Empty<string>();

        [ShowInInspector, ReadOnly]
        public string[] ActiveRoots = Array.Empty<string>();

        [ShowInInspector, ReadOnly]
        public (string RootLabel, string Selected)[] SelectionHistory = Array.Empty<(string, string)>();

        IUIModalStackTelemetry? _telemetry;

        public void Bind(IUIModalStackTelemetry telemetry)
        {
            Unbind();
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            telemetry.OnModalStackChanged += OnChanged;
            telemetry.OnActiveRootsChanged += OnRootsChanged;
            Refresh();
        }

        void OnChanged(UIModalStackChangeContext context)
        {
            Refresh();
        }

        void OnRootsChanged(UIModalStackRootsChangeContext context)
        {
            Refresh();
        }

        void Refresh()
        {
            if (_telemetry == null) return;
            CurrentInputRoot = UIModalStackDebugLabelUtility.DescribeRoot(_telemetry.CurrentInputRoot);
            Depth = _telemetry.Depth;
            StackModalIds = _telemetry.GetStackModalIds();
            var roots = _telemetry.ActiveRoots;
            if (roots != null)
            {
                var rootsArr = new string[roots.Count];
                for (int i = 0; i < roots.Count; i++)
                {
                    var r = roots[i];
                    rootsArr[i] = UIModalStackDebugLabelUtility.DescribeStackEntry(r.StackKey, r.Root);
                }
                ActiveRoots = rootsArr;
            }
            var hist = _telemetry.GetSelectionHistorySnapshot();
            var histArr = new (string, string)[hist.Count];
            for (int i = 0; i < hist.Count; i++) histArr[i] = hist[i];
            SelectionHistory = histArr;
        }

        public void Unbind()
        {
            if (_telemetry == null) return;
            _telemetry.OnModalStackChanged -= OnChanged;
            _telemetry.OnActiveRootsChanged -= OnRootsChanged;
            _telemetry = null;
        }

        public void Dispose() => Unbind();
    }
}
