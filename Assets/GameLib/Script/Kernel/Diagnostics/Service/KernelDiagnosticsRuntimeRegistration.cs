#nullable enable
using System;

namespace Game.Kernel.Diagnostics
{
    public readonly struct KernelDiagnosticsRegistrationOptions
    {
        public KernelDiagnosticsRegistrationOptions(
            DiagnosticProfileKind profileKind,
            bool enableUnityLogSink = true,
            bool enableInMemorySink = false,
            int inMemoryCapacity = 256,
            bool enableTrace = false)
        {
            if (inMemoryCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(inMemoryCapacity), inMemoryCapacity, "In-memory capacity must be positive.");

            ProfileKind = profileKind;
            EnableUnityLogSink = enableUnityLogSink;
            EnableInMemorySink = enableInMemorySink;
            EnableTrace = enableTrace;
            InMemoryCapacity = inMemoryCapacity;
        }

        public DiagnosticProfileKind ProfileKind { get; }
        public bool EnableUnityLogSink { get; }
        public bool EnableInMemorySink { get; }
        public bool EnableTrace { get; }
        public int InMemoryCapacity { get; }
    }

    public static class KernelDiagnosticsRuntimeRegistration
    {
        public static void Register(
            IRuntimeContainerBuilder builder,
            KernelDiagnosticsRegistrationOptions options,
            params IKernelDiagnosticSink[] additionalSinks)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            bool enableTestSink = options.ProfileKind == DiagnosticProfileKind.Test;

            if (options.EnableInMemorySink)
            {
                builder.Register<InMemoryDiagnosticSink>(resolver => new InMemoryDiagnosticSink(options.InMemoryCapacity), RuntimeLifetime.Singleton)
                    .AsSelf();
            }

            if (options.EnableUnityLogSink)
            {
                builder.Register<UnityLogDiagnosticSink>(resolver => new UnityLogDiagnosticSink(options.ProfileKind, options.EnableTrace), RuntimeLifetime.Singleton)
                    .AsSelf();
            }

            if (enableTestSink)
            {
                builder.Register<TestDiagnosticSink>(RuntimeLifetime.Singleton)
                    .AsSelf();
            }

            builder.Register<IKernelDiagnosticService>(resolver =>
            {
                int additionalCount = additionalSinks != null ? additionalSinks.Length : 0;
                int sinkCount = additionalCount;
                if (options.EnableInMemorySink)
                    sinkCount++;
                if (options.EnableUnityLogSink)
                    sinkCount++;
                if (enableTestSink)
                    sinkCount++;
                var sinks = new IKernelDiagnosticSink[sinkCount];
                int index = 0;

                if (options.EnableInMemorySink)
                {
                    sinks[index++] = resolver.Resolve<InMemoryDiagnosticSink>();
                }

                if (options.EnableUnityLogSink)
                {
                    sinks[index++] = resolver.Resolve<UnityLogDiagnosticSink>();
                }

                if (enableTestSink)
                {
                    sinks[index++] = resolver.Resolve<TestDiagnosticSink>();
                }

                for (int i = 0; i < additionalCount; i++)
                {
                    sinks[index++] = additionalSinks[i] ?? throw new ArgumentException("Additional sink must not be null.", nameof(additionalSinks));
                }

                return new KernelDiagnosticService(sinks);
            }, RuntimeLifetime.Singleton);
        }
    }
}