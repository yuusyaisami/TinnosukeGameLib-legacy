#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Game.Kernel.Diagnostics
{
    public sealed class InMemoryDiagnosticSink : IKernelDiagnosticSink
    {
        readonly List<KernelDiagnostic> _diagnostics = new List<KernelDiagnostic>();
        readonly ReadOnlyCollection<KernelDiagnostic> _view;
        readonly int _capacity;

        public InMemoryDiagnosticSink(int capacity = 256)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be positive.");

            _capacity = capacity;
            _view = _diagnostics.AsReadOnly();
        }

        public IReadOnlyList<KernelDiagnostic> Diagnostics => _view;
        public int FlushCount { get; private set; }
        public int Capacity => _capacity;
        public bool WasTruncated { get; private set; }

        public void Emit(in KernelDiagnostic diagnostic)
        {
            if (_diagnostics.Count == _capacity)
            {
                _diagnostics.RemoveAt(0);
                WasTruncated = true;
            }

            _diagnostics.Add(diagnostic);
        }

        public void Flush()
        {
            FlushCount++;
        }

        public void Clear()
        {
            _diagnostics.Clear();
            FlushCount = 0;
            WasTruncated = false;
        }
    }
}