#nullable enable

namespace Game.Kernel.Contributions
{
    public enum ContributionKind
    {
        Unknown = 0,
        ServiceContribution = 10,
        CommandContribution = 20,
        ValueContribution = 30,
        ValueInitContribution = 40,
        DynamicEvaluationContribution = 50,
        ReactiveEvaluationContribution = 60,
        ScopeContribution = 70,
        LifecycleContribution = 80,
        RuntimeQueryContribution = 90,
        DiagnosticsContribution = 100,
        AssetBindingContribution = 110,
        CodeGenerationContribution = 120,
    }
}