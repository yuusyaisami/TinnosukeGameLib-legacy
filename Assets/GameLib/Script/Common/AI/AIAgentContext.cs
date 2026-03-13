#nullable enable
using System;
using VNext = Game.Commands.VNext;
using Game.Common;
using Game.Movement;
using Game.Targeting;
using UnityEngine;
using VContainer;
using Game.Commands;

namespace Game.AI
{
    /// <summary>
    /// AI コールバックに渡されるコンテキスト（構造体で GC 回避）。
    /// </summary>
    public readonly struct AIAgentContext
    {
        public readonly IScopeNode Scope;
        public readonly Component? ScopeComponent;
        public readonly IVarStore Vars;
        public readonly IMonitorChannelHub? MonitorHub;
        public readonly ITargetChannelHub? TargetHub;
        public readonly IMoveToInputPointService? MoveToInputPoint;
        public readonly IMovementChannelHub? MovementHub;
        public readonly VNext.ICommandRunner Runner;
        public readonly float DeltaTime;
        public readonly int FrameCount;

        public AIAgentContext(
            IScopeNode scope,
            Component? scopeComponent,
            IVarStore vars,
            IMonitorChannelHub? monitorHub,
            ITargetChannelHub? targetHub,
            IMoveToInputPointService? moveToPoint,
            IMovementChannelHub? movementHub,
            VNext.ICommandRunner runner,
            float deltaTime,
            int frameCount)
        {
            Scope = scope;
            ScopeComponent = scopeComponent;
            Vars = vars ?? NullVarStore.Instance;
            MonitorHub = monitorHub;
            TargetHub = targetHub;
            MoveToInputPoint = moveToPoint;
            MovementHub = movementHub;
            Runner = runner;
            DeltaTime = deltaTime;
            FrameCount = frameCount;
        }

        public IObjectResolver? Resolver => Scope.Resolver;
        public Transform? Transform => ScopeComponent ? ScopeComponent.transform : null;

        /// <summary>CommandContext を生成（Command 実行時に使用）</summary>
        public VNext.CommandContext ToCommandContext()
        {
            var options = VNext.CommandRunOptions.Default;
            var runner = Runner;
            var resolver = Scope.Resolver;
            if (resolver != null &&
                resolver.TryResolve<VNext.ICommandRunner>(out var scopedRunner) &&
                scopedRunner != null)
            {
                runner = scopedRunner;
            }

            return new VNext.CommandContext(Scope, Vars, runner, Scope, options);
        }
    }
}
