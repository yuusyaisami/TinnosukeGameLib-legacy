#nullable enable

namespace Game.Flow
{
    /// <summary>
    /// Flow の引数の種別を表します。
    /// <para>コンパイル段階で使用され、実行時に FlowContext により評価されます。</para>
    /// </summary>
    public enum FlowArgKind
    {
        None = 0,

        // Const
        ConstInt = 10,
        ConstFloat = 11,
        ConstBool = 12,
        ConstString = 13, // stringTable index
        ConstVector2 = 14,
        ConstVector3 = 15,
        ConstVector4 = 16,
        ConstColor = 17,

        // Var
        VarLocal = 30,  // localSlotIndex
        VarShared = 31, // varId

        // Dynamic
        Dynamic = 50,   // SerializeReference IDynamicSource

        // Unity
        UnityObject = 60,

        // Commands
        CommandSource = 70, // SerializeReference Game.Commands.VNext.ICommandSource
    }
}
