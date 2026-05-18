#nullable enable
using System;
using Game.Kernel.Diagnostics;

namespace TinnosukeGameLib.Tests.Editor
{
    public sealed class KernelTestArtifactSink : IKernelDiagnosticSink
    {
        public void Emit(in KernelDiagnostic diagnostic)
        {
            KernelTestArtifactCollector.RecordDiagnostic(in diagnostic);
        }

        public void Flush()
        {
        }
    }
}
