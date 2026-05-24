#nullable enable
using System;
using Game.Kernel.Boot;

namespace Game.Kernel.V21.Composition
{
    public sealed class ApplicationKernelBootBoundaryAdapter
    {
        readonly IKernelBootRuntimeSurfaceFactory runtimeSurfaceFactory;

        public ApplicationKernelBootBoundaryAdapter(IKernelBootRuntimeSurfaceFactory runtimeSurfaceFactory)
        {
            this.runtimeSurfaceFactory = runtimeSurfaceFactory ?? throw new ArgumentNullException(nameof(runtimeSurfaceFactory));
        }

        public IKernelBootRuntimeSurfaceFactory RuntimeSurfaceFactory => runtimeSurfaceFactory;

        public KernelBootBoundaryResult Execute(BootValidationInput input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            return KernelBootBoundary.Execute(input, runtimeSurfaceFactory);
        }
    }
}
