// Game.Save.SaveEnums.cs
//
// SaveSystem v2 core enums + policies

#nullable enable
namespace Game.Save
{
    /// <summary>
    /// 保存用途のレイヤー。
    /// CalcLayer（Scalarの計算レイヤー）とは別物。
    /// </summary>
    public enum SaveLayer
    {
        Global = 0,   // ゲーム全体の永続データ
        SystemSetting = 1,  //システム設定データ 音量や画質など
        Profile = 2,  // プレイヤープロファイルデータ
        Session = 3,  // セッション一時データ
        GameLogic = 4, // ゲームロジックデータ
    }

    public enum SaveStatus : byte
    {
        Success = 0,
        NoData = 1,
        Failed = 2,
    }

    public enum SaveError : byte
    {
        None = 0,
        ScopeNotReady,
        ScopeMismatch,
        NotMainThread,
        InvalidKey,
        MissingDependency,
        SerializationError,
        StorageFull,
        IOError,
        UnknownException,
    }

    public readonly struct SaveResult
    {
        public readonly SaveStatus Status;
        public readonly SaveError Error;
        public readonly string Message;

        public SaveResult(SaveStatus status, SaveError error, string message = "")
        {
            Status = status;
            Error = error;
            Message = message ?? string.Empty;
        }

        public bool IsSuccess => Status == SaveStatus.Success;
        public bool IsNoData => Status == SaveStatus.NoData;
        public bool IsFailed => Status == SaveStatus.Failed;

        public static SaveResult Success() => new SaveResult(SaveStatus.Success, SaveError.None);
        public static SaveResult NoData() => new SaveResult(SaveStatus.NoData, SaveError.None);
        public static SaveResult Failed(SaveError error, string msg = "") => new SaveResult(SaveStatus.Failed, error, msg);

        public override string ToString() => IsSuccess ? "Success" : IsNoData ? "NoData" : $"Failed({Error}): {Message}";
    }

    /// <summary>
    /// Blackboard 変数の初期化ポリシー。
    /// </summary>
    public enum BlackboardInitPolicy
    {
        /// <summary>Bag にキーが存在しない場合のみ DefaultValue を注入。</summary>
        ApplyIfMissing = 0,
        /// <summary>常に DefaultValue で上書き。</summary>
        ForceOverwrite = 1,
    }

    /// <summary>
    /// Load時の "missing（保存データに存在しないキー）" の処理ポリシー。
    /// 
    /// v2.0 仕様: ProfileRegistry が初期値を入れるため、
    /// デフォルトは KeepCurrent（何もしない）を推奨。
    /// </summary>
    public enum MissingPolicy
    {
        /// <summary>
        /// 保存データに無いキーは何もしない。
        /// ProfileRegistry が入れた初期値が残る。
        /// 推奨: Global/SystemSetting/Profile/GameLogic 層で使用。
        /// </summary>
        KeepCurrent = 0,

        /// <summary>
        /// 保存データに無いキーはクリアする（Blackboard: Unset, Scalar: 0）。
        /// 推奨: Session 層など一時データ用途で使用。
        /// 
        /// 注意: ProfileRegistry初期値モデルと衝突しやすいため、
        /// 明示的に必要な場合のみ使用。
        /// </summary>
        Clear = 1,
    }

    /// <summary>
    /// SaveLayer ごとの Missing ポリシーを定義するインターフェース。
    /// 
    /// v2.0 仕様: Binder v2 が SaveEntry集合から Load時の挙動を決定するため、
    /// レイヤーごとにポリシーを設定可能にする。
    /// </summary>
    public interface ISaveLayerPolicy
    {
        /// <summary>
        /// Blackboard 用の missing ポリシーを取得。
        /// </summary>
        MissingPolicy GetBlackboardMissingPolicy(SaveLayer layer);

        /// <summary>
        /// Scalar 用の missing ポリシーを取得。
        /// </summary>
        MissingPolicy GetScalarMissingPolicy(SaveLayer layer);
    }

    /// <summary>
    /// ISaveLayerPolicy の簡易実装（デフォルト推奨値）。
    /// 
    /// Global/SystemSetting/Profile/GameLogic: KeepCurrent
    /// Session: Clear
    /// </summary>
    public class DefaultSaveLayerPolicy : ISaveLayerPolicy
    {
        public MissingPolicy GetBlackboardMissingPolicy(SaveLayer layer)
        {
            // TODO v2.0: Binder v2 実装時に、SaveEntry集合を参照して
            // 実際の Load 処理で此のポリシーを適用する予定。
            // 現在はスタブ。
            return layer == SaveLayer.Session ? MissingPolicy.Clear : MissingPolicy.KeepCurrent;
        }

        public MissingPolicy GetScalarMissingPolicy(SaveLayer layer)
        {
            // TODO v2.0: 同上
            return layer == SaveLayer.Session ? MissingPolicy.Clear : MissingPolicy.KeepCurrent;
        }
    }
}
