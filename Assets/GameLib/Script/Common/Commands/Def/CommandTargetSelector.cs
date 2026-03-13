using Sirenix.OdinInspector;
using System;
namespace Game.Commands
{
    public enum CommandTargetMode
    {
        Self,           // 現在の Runner
        ByIdentity,     // Identity で解決
        SpecifiedActor, // Runner 実行時に指定された実行者で実行（Runnerに渡されたオプションのIScopeNodeを使用）
    }

    public enum CommandTargetSearchScope
    {
        All,            // Registry 全体
        AncestorsOnly,  // 呼び出し元の親方向のみ
        DescendantsOnly // 呼び出し元の子方向のみ
    }

    [Serializable]
    public struct CommandTargetIdentityFilter
    {
        // null / empty / None は「条件に含めない」
        public LifetimeScopeKind kind;   // e.g. Library / Scene / Entity
        public string id;                // "Player_1" など
        public string category;          // "Run", "Lobby", "Menu" など
        public bool requireActive;       // 非アクティブを除外するか
        public CommandTargetSearchScope searchScope; // 親のみ / 子のみ / 全体
    }

    [Serializable, InlineProperty]
    public struct CommandTargetSelector
    {
        public CommandTargetMode mode;
        [InlineProperty, HideLabel, ShowIf(nameof(IsShowIdentityFilter))]
        public CommandTargetIdentityFilter identity;


        // Odin用 ラベル切り替えプロパティは省略
        bool IsShowIdentityFilter() => mode == CommandTargetMode.ByIdentity;

        public static CommandTargetSelector Self()
        {
            return new CommandTargetSelector
            {
                mode = CommandTargetMode.Self
            };
        }
    }
}
