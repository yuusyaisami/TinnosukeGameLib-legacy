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
    /// AI 繧ｳ繝ｼ繝ｫ繝舌ャ繧ｯ縺ｫ貂｡縺輔ｌ繧九さ繝ｳ繝・く繧ｹ繝茨ｼ域ｧ矩菴薙〒 GC 蝗樣∩・峨・
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

        public IRuntimeResolver? Resolver => Scope.Resolver;
        public Transform? Transform => ScopeComponent ? ScopeComponent.transform : null;

        /// <summary>CommandContext 繧堤函謌撰ｼ・ommand 螳溯｡梧凾縺ｫ菴ｿ逕ｨ・・/summary>
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
