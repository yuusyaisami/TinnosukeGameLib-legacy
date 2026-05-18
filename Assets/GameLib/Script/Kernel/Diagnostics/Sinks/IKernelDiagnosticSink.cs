#nullable enable

namespace Game.Kernel.Diagnostics
{
    public interface IKernelDiagnosticSink
    {
        void Emit(in KernelDiagnostic diagnostic);
        void Flush();
    }
}