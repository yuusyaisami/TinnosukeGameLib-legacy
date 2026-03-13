#nullable enable

using System;
using Game.Commands.VNext;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Flow
{
    /// <summary>
    /// WithActorCommand実行用FlowStatement。
    /// 
    /// 指定したアクターをスコープとしてコマンドリストを実行します。
    /// </summary>
    [Serializable]
    public sealed class FlowWithActorCommandStmt : FlowStatement
    {
        [BoxGroup("WithActorCommand")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(CommandData.ActorSource)")]
        [InlineProperty]
        public WithActorCommandData CommandData = new();

        public override void EnsureIntegrity()
        {
            CommandData ??= new();
        }
    }
}
