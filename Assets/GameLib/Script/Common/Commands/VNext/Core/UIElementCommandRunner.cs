#nullable enable
using Game;
using Game.Common;
using Game.UI;
using Game.Vars.Generated;

namespace Game.Commands.VNext
{
    public sealed class UIElementCommandRunner : CommandRunner, IUIElementCommandRunner
    {
        public UIElementCommandRunner(
            IScopeNode scope,
            CommandExecutorRegistry registry,
            ICommandCatalog catalog,
            ICommandKeyResolver keyResolver,
            ICommandResolveLogger logger,
            VarStorePayload? defaultVars = null)
            : base(scope, registry, catalog, keyResolver, logger, defaultVars)
        {
        }

        protected override void InjectContextVars(CommandContext ctx, IVarStore vars)
        {
            _ = ctx;
            if (vars == null)
                return;

            var scope = Scope;
            var state = scope.GetUIElementState();

            bool isSelected = false;
            bool canNavigate = false;

            if (scope.TryResolveInAncestors<IUISelectionState>(out var selectionState) &&
                selectionState != null)
            {
                isSelected = ReferenceEquals(selectionState.CurrentElement, scope);
            }

            if (state != null)
            {
                canNavigate = state.EvaluateIsNavigationSelectable();

                if (scope.TryResolveInAncestors<IUISelectionNavigation>(out var navigation) &&
                    navigation != null)
                {
                    canNavigate = canNavigate && navigation.CanSelect(scope);
                }
            }

            TrySetBoolVar(vars, VarIds.GameLib.Base.UIElement.IsSelected, isSelected);
            TrySetBoolVar(vars, VarIds.GameLib.Base.UIElement.IsNavigationSelectable, canNavigate);
        }

        static void TrySetBoolVar(IVarStore vars, int varId, bool value)
        {
            if (varId <= 0 || vars.Contains(varId))
                return;

            vars.TrySetVariant(varId, DynamicVariant.FromBool(value));
        }
    }
}
