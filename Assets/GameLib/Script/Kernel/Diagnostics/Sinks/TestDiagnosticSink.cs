#nullable enable
using System;
using System.Collections.Generic;

namespace Game.Kernel.Diagnostics
{
    public sealed class TestDiagnosticSink : IKernelDiagnosticSink
    {
        readonly List<KernelDiagnostic> _diagnostics = new List<KernelDiagnostic>();

        public IReadOnlyList<KernelDiagnostic> Diagnostics => _diagnostics;
        public int FlushCount { get; private set; }

        public void Emit(in KernelDiagnostic diagnostic)
        {
            _diagnostics.Add(diagnostic);
        }

        public void Flush()
        {
            FlushCount++;
        }

        public bool ContainsCode(DiagnosticCode code)
        {
            for (int i = 0; i < _diagnostics.Count; i++)
            {
                if (_diagnostics[i].Code == code)
                    return true;
            }

            return false;
        }

        public void Reset()
        {
            _diagnostics.Clear();
            FlushCount = 0;
        }
    }
}