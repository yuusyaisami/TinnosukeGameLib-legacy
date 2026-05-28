#nullable enable

using System;
using System.Reflection;
using Game.Kernel.Generation;
using Game.Kernel.IR;

namespace TinnosukeGameLib.Tests.Editor
{
    static class KernelProjectionHashingTestAdapter
    {
        delegate Hash128 ServiceGraphHashDelegate(ReadOnlySpan<ServiceIR> services);
        delegate Hash128 ScopeGraphHashDelegate(ReadOnlySpan<ScopeIR> scopes);
        delegate Hash128 ScopeGraphWithValueInitHashDelegate(ReadOnlySpan<ScopeIR> scopes, ReadOnlySpan<ValueInitPlanIR> valueInitPlans);
        delegate Hash128 LifecyclePlanHashDelegate(ReadOnlySpan<LifecycleIR> lifecycles);
        delegate Hash128 CommandCatalogHashDelegate(ReadOnlySpan<CommandIR> commands);
        delegate Hash128 CommandExecutorTableHashDelegate(ReadOnlySpan<CommandExecutorEntryPlan> entries);
        delegate Hash128 DebugMapHashDelegate(ReadOnlySpan<KernelDebugMapEntry> entries);

        static readonly Type KernelProjectionHashingType = typeof(ServiceGraphPlan).Assembly.GetType("Game.Kernel.Generation.KernelProjectionHashing", throwOnError: true)!;
        static readonly ServiceGraphHashDelegate ComputeServiceGraphHashImpl = CreateDelegate<ServiceGraphHashDelegate>("ComputeServiceGraphHash", typeof(ReadOnlySpan<ServiceIR>));
        static readonly ScopeGraphHashDelegate ComputeScopeGraphHashImpl = CreateDelegate<ScopeGraphHashDelegate>("ComputeScopeGraphHash", typeof(ReadOnlySpan<ScopeIR>));
        static readonly ScopeGraphWithValueInitHashDelegate ComputeScopeGraphHashWithValueInitImpl = CreateDelegate<ScopeGraphWithValueInitHashDelegate>("ComputeScopeGraphHash", typeof(ReadOnlySpan<ScopeIR>), typeof(ReadOnlySpan<ValueInitPlanIR>));
        static readonly LifecyclePlanHashDelegate ComputeLifecyclePlanHashImpl = CreateDelegate<LifecyclePlanHashDelegate>("ComputeLifecyclePlanHash", typeof(ReadOnlySpan<LifecycleIR>));
        static readonly CommandCatalogHashDelegate ComputeCommandCatalogHashImpl = CreateDelegate<CommandCatalogHashDelegate>("ComputeCommandCatalogHash", typeof(ReadOnlySpan<CommandIR>));
        static readonly CommandExecutorTableHashDelegate ComputeCommandExecutorTableHashImpl = CreateDelegate<CommandExecutorTableHashDelegate>("ComputeCommandExecutorTableHash", typeof(ReadOnlySpan<CommandExecutorEntryPlan>));
        static readonly DebugMapHashDelegate ComputeDebugMapHashImpl = CreateDelegate<DebugMapHashDelegate>("ComputeDebugMapHash", typeof(ReadOnlySpan<KernelDebugMapEntry>));

        public static Hash128 ComputeServiceGraphHash(ReadOnlySpan<ServiceIR> services)
        {
            return ComputeServiceGraphHashImpl(services);
        }

        public static Hash128 ComputeScopeGraphHash(ReadOnlySpan<ScopeIR> scopes)
        {
            return ComputeScopeGraphHashImpl(scopes);
        }

        public static Hash128 ComputeScopeGraphHash(ReadOnlySpan<ScopeIR> scopes, ReadOnlySpan<ValueInitPlanIR> valueInitPlans)
        {
            return ComputeScopeGraphHashWithValueInitImpl(scopes, valueInitPlans);
        }

        public static Hash128 ComputeLifecyclePlanHash(ReadOnlySpan<LifecycleIR> lifecycles)
        {
            return ComputeLifecyclePlanHashImpl(lifecycles);
        }

        public static Hash128 ComputeCommandCatalogHash(ReadOnlySpan<CommandIR> commands)
        {
            return ComputeCommandCatalogHashImpl(commands);
        }

        public static Hash128 ComputeCommandExecutorTableHash(ReadOnlySpan<CommandExecutorEntryPlan> entries)
        {
            return ComputeCommandExecutorTableHashImpl(entries);
        }

        public static Hash128 ComputeDebugMapHash(ReadOnlySpan<KernelDebugMapEntry> entries)
        {
            return ComputeDebugMapHashImpl(entries);
        }

        static TDelegate CreateDelegate<TDelegate>(string methodName, params Type[] parameterTypes)
            where TDelegate : Delegate
        {
            MethodInfo method = KernelProjectionHashingType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: parameterTypes,
                modifiers: null) ?? throw new MissingMethodException(KernelProjectionHashingType.FullName, methodName);

            return (TDelegate)method.CreateDelegate(typeof(TDelegate));
        }
    }
}