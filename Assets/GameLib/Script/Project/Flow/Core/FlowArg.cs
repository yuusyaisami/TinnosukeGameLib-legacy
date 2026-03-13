#nullable enable

using System;
using Game.Common;
using Game.Commands.VNext;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Flow
{
    /// <summary>
    /// スクリプト実行時に使用する引数を表す構造体。
    /// <para>コンパイル時に定数や変数参照などに展開され、実行時に FlowRunner / FlowContext により評価されます。</para>
    /// </summary>
    [Serializable]
    public struct FlowArg
    {
        /// <summary>引数の種類（定数、ローカル変数、共有変数、Unityオブジェクト 等）</summary>
        [HorizontalGroup("Kind", Width = 0.25f)]
        [HideLabel]
        public FlowArgKind Kind;

        /// <summary>整数系の値（ConstInt / ConstBool / VarLocal / VarShared / ConstString のインデックス等で使用）</summary>
        [HorizontalGroup("Kind")]
        [LabelText("Int")]
        [ShowIf(nameof(ShowInt0))]
        public int Int0;

        /// <summary>浮動小数点値（ConstFloat 用）</summary>
        [HorizontalGroup("Kind")]
        [LabelText("Float")]
        [ShowIf(nameof(ShowFloat0))]
        public float Float0;

        /// <summary>ベクトル/カラー系の値（ConstVector* / ConstColor 用）</summary>
        [HorizontalGroup("Kind")]
        [LabelText("Vec4")]
        [ShowIf(nameof(ShowVec4))]
        public Vector4 Vec4;

        /// <summary>Unity オブジェクト参照（UnityObject 用）</summary>
        [HorizontalGroup("Kind")]
        [LabelText("Obj")]
        [ShowIf(nameof(ShowObj0))]
        public UnityEngine.Object? Obj0;

        /// <summary>動的値を提供するシリアライズ参照（Dynamic 用）</summary>
        [HorizontalGroup("Kind")]
        [LabelText("Dynamic")]
        [ShowIf(nameof(ShowDynamicSource))]
        [SerializeReference]
        public IDynamicSource? DynamicSource;

        /// <summary>vNext のコマンドソース参照（CommandSource 用）</summary>
        [HorizontalGroup("Kind")]
        [LabelText("CmdSrc")]
        [ShowIf(nameof(ShowCommandSource))]
        [HideReferenceObjectPicker]
        [SerializeReference]
        public ICommandSource? CommandSource;

        bool ShowInt0() => Kind == FlowArgKind.ConstInt || Kind == FlowArgKind.ConstBool || Kind == FlowArgKind.ConstString || Kind == FlowArgKind.VarLocal || Kind == FlowArgKind.VarShared;
        bool ShowFloat0() => Kind == FlowArgKind.ConstFloat;
        bool ShowVec4() => Kind == FlowArgKind.ConstVector2 || Kind == FlowArgKind.ConstVector3 || Kind == FlowArgKind.ConstVector4 || Kind == FlowArgKind.ConstColor;
        bool ShowObj0() => Kind == FlowArgKind.UnityObject;
        bool ShowDynamicSource() => Kind == FlowArgKind.Dynamic;
        bool ShowCommandSource() => Kind == FlowArgKind.CommandSource;

        /// <summary>整数定数を作成します。</summary>
        public static FlowArg ConstInt(int value) => new() { Kind = FlowArgKind.ConstInt, Int0 = value };
        /// <summary>真偽値定数を作成します（内部的に Int0=0/1 を使用）。</summary>
        public static FlowArg ConstBool(bool value) => new() { Kind = FlowArgKind.ConstBool, Int0 = value ? 1 : 0 };
        /// <summary>浮動小数点定数を作成します。</summary>
        public static FlowArg ConstFloat(float value) => new() { Kind = FlowArgKind.ConstFloat, Float0 = value };
        /// <summary>文字列テーブルのインデックスを指定する文字列定数を作成します。</summary>
        public static FlowArg ConstString(int stringTableIndex) => new() { Kind = FlowArgKind.ConstString, Int0 = stringTableIndex };
        /// <summary>ローカル変数参照を表す引数を作成します。</summary>
        public static FlowArg VarLocal(int localSlotIndex) => new() { Kind = FlowArgKind.VarLocal, Int0 = localSlotIndex };
        /// <summary>共有変数参照を表す引数を作成します。</summary>
        public static FlowArg VarShared(int varId) => new() { Kind = FlowArgKind.VarShared, Int0 = varId };
        /// <summary>Unity オブジェクト参照を作成します。</summary>
        public static FlowArg UnityObject(UnityEngine.Object? obj) => new() { Kind = FlowArgKind.UnityObject, Obj0 = obj };

        /// <summary>vNext コマンドソース参照を作成します。</summary>
        public static FlowArg VNextCommandSource(ICommandSource source) => new() { Kind = FlowArgKind.CommandSource, CommandSource = source };
    }
}
