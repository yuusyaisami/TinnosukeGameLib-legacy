#nullable enable
// Game.Targeting
// ================================================================================
// TargetChannelDefs - Target 検索用の定義とインターフェース群
// ================================================================================
//
// 本ファイルは「ターゲット検索チャンネル」を宣言するためのデータ構造をまとめる。
// TargetChannelRuntime/Hub がこの定義をもとに検索を実行し、結果をキャッシュする。
// コメントを多めに入れて、意図と使用方法を明示している。
// ================================================================================

using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Mathematics;
using VContainer;
using UnityEngine;
using Game.Entity;
using Game.Search;
using VNext = Game.Commands.VNext;

namespace Game.Targeting
{
    /// <summary>検索クエリ種別。</summary>
    public enum TargetQueryKind
    {
        Circle = 0, // 円検索。角度指定なし。
        Cone = 1,   // 円錐検索。半角と方向ベクトルを使用。
    }

    /// <summary>Origin の取得元。</summary>
    public enum TargetOriginSource
    {
        OwnerFoot = 0,            // FootTransformMB.FootWorldPosition を使用
        OwnerTransformPosition = 1, // Transform.position を使用
        CustomTransform = 2,      // 任意の Transform から取得
    }

    /// <summary>Forward の取得元（Cone のときのみ意味を持つ）。</summary>
    public enum TargetForwardSource
    {
        OwnerTransformUp = 0,     // Transform.up
        OwnerTransformRight = 1,  // Transform.right
        CustomTransformUp = 2,    // 任意 Transform の up
        CustomTransformRight = 3, // 任意 Transform の right
        CustomVector = 4,         // 固定ベクトル（Vector2）
    }

    /// <summary>検索の実装タイプ。</summary>
    public enum TargetChannelSearchType
    {
        DynamicSearch = 0,
        ScopeSearch = 1,
    }

    /// <summary>
    /// TargetChannel の定義（Inspector / Template 用）。
    /// 外部は Tag を指定して TargetChannelRuntime へアクセスする。
    /// </summary>
    [Serializable]
    public sealed class TargetChannelDef
    {
        public bool IsDynamicSearch => SearchType == TargetChannelSearchType.DynamicSearch;
        public bool IsScopeSearch => SearchType == TargetChannelSearchType.ScopeSearch;

        // ================================================================
        // Identity
        // ================================================================

        [BoxGroup("Identity")]
        [LabelText("Tag")]
        [Required]
        public string Tag = "default"; // 識別用タグ。Hub でキーとして利用。

        [BoxGroup("Identity")]
        [LabelText("Enabled")]
        public bool Enabled = true; // false のときは検索をスキップし、結果をクリア。

        // ================================================================
        // Search
        // ================================================================

        [BoxGroup("Search")]
        [LabelText("Type")]
        [EnumToggleButtons]
        public TargetChannelSearchType SearchType = TargetChannelSearchType.DynamicSearch;

        // ================================================================
        // Query
        // ================================================================

        [BoxGroup("Query")]
        [LabelText("Kind")]
        [ShowIf(nameof(IsDynamicSearch))]
        public TargetQueryKind Kind = TargetQueryKind.Circle; // 検索形状

        [BoxGroup("Query")]
        [LabelText("Radius")]
        [ShowIf(nameof(IsDynamicSearch))]
        [MinValue(0.01f)]
        public float Radius = 5f; // 検索半径（円）または円錐の外半径

        [BoxGroup("Query")]
        [LabelText("Half Angle (deg)")]
        [ShowIf("@IsDynamicSearch && Kind == TargetQueryKind.Cone")]
        [Range(1f, 179f)]
        public float HalfAngleDeg = 60f; // 円錐の半角（度数法）

        [BoxGroup("Query")]
        [LabelText("Refresh Interval (frames)")]
        [Tooltip("同一フレーム内の複数リクエストは必ずキャッシュ。さらに軽量化したい場合は 2〜8 などに上げる。")]
        [MinValue(1)]
        public int RefreshIntervalFrames = 1; // 何フレームに1回再検索するか（キャッシュ間隔）

        [BoxGroup("Query")]
        [LabelText("Expected Results")]
        [MinValue(0)]
        [Tooltip("内部 List の Capacity を最低限この数まで確保するヒント。")]
        public int ExpectedResultCount = 32; // 結果リストの初期容量ヒント

        // ================================================================
        // Filters (LTS Identity)
        // ================================================================

        [BoxGroup("Filters")]
        [LabelText("Kind Mask")]
        [ShowIf(nameof(IsDynamicSearch))]
        [Tooltip("検索対象に含める LifetimeScopeKind のマスク。Entity だけにしたい場合は Entity のみ。Runtime も含めたい場合は Entity|Runtime。")]
        public LifetimeScopeMask KindMask = LifetimeScopeMask.Entity;

        [BoxGroup("Filters")]
        [LabelText("Filter Id")]
        [ShowIf(nameof(IsDynamicSearch))]
        [Tooltip("ILTSIdentityService.Id でフィルタ。空なら無効。")]
        public string? FilterId; // Id フィルタ（null/空で無効）

        [BoxGroup("Filters")]
        [LabelText("Filter Category")]
        [ShowIf(nameof(IsDynamicSearch))]
        [Tooltip("ILTSIdentityService.Category でフィルタ。空なら無効。")]
        public string? FilterCategory; // Category フィルタ（null/空で無効）

        [BoxGroup("Filters")]
        [LabelText("Exclude Self")]
        public bool ExcludeSelf = true; // 検索結果から自分自身（Owner）を除外するか

        // ================================================================
        // Sources
        // ================================================================

        [BoxGroup("Sources")]
        [LabelText("Origin Source")]
        [ShowIf(nameof(IsDynamicSearch))]
        public TargetOriginSource OriginSource = TargetOriginSource.OwnerFoot; // 原点の取得方法

        [BoxGroup("Sources")]
        [LabelText("Custom Origin Transform")]
        [ShowIf("@IsDynamicSearch && OriginSource == TargetOriginSource.CustomTransform")]
        public Transform? CustomOriginTransform; // 原点用の Transform（任意）

        [BoxGroup("Sources")]
        [LabelText("Forward Source")]
        [ShowIf("@IsDynamicSearch && Kind == TargetQueryKind.Cone")]
        public TargetForwardSource ForwardSource = TargetForwardSource.OwnerTransformUp; // 方向ベクトルの取得方法

        [BoxGroup("Sources")]
        [LabelText("Custom Forward Transform")]
        [ShowIf("@IsDynamicSearch && Kind == TargetQueryKind.Cone && (ForwardSource == TargetForwardSource.CustomTransformUp || ForwardSource == TargetForwardSource.CustomTransformRight)")]
        public Transform? CustomForwardTransform; // 方向用 Transform

        [BoxGroup("Sources")]
        [LabelText("Custom Forward Vector")]
        [ShowIf("@IsDynamicSearch && Kind == TargetQueryKind.Cone && ForwardSource == TargetForwardSource.CustomVector")]
        public Vector2 CustomForwardVector = Vector2.up; // 固定方向ベクトル

        // ================================================================
        // Scope Search
        // ================================================================

        [BoxGroup("Scope Search")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(ActorSource)")]
        [ShowIf(nameof(IsScopeSearch))]
        public VNext.ActorSource ActorSource;

        [BoxGroup("Scope Search")]
        [LabelText("Require Active")]
        [ShowIf("@IsScopeSearch && ActorSource.Kind != Game.Commands.VNext.ActorSourceKind.ByIdentity")]
        public bool ScopeRequireActive = true;

        // ================================================================
        // Validation / Helpers
        // ================================================================

        /// <summary>円錐の半角 cos 値（Kind が Cone のときのみ有効）</summary>
        public float CosHalfAngle
        {
            get
            {
                if (Kind != TargetQueryKind.Cone) return -1f; // 円検索では -1f を返し、フィルタ無効扱い
                var rad = HalfAngleDeg * Mathf.Deg2Rad;
                return Mathf.Cos(rad);
            }
        }
    }

    /// <summary>
    /// Channel 実行コンテキスト（Owner 情報）。
    /// 検索を行う際の「自分自身」を保持し、Origin/Forward 解決に使用する。
    /// </summary>
    public readonly struct TargetChannelOwner
    {
        public readonly Transform OwnerTransform;
        public readonly IScopeNode? OwnerScope;

        public TargetChannelOwner(Transform ownerTransform, IScopeNode? ownerScope)
        {
            OwnerTransform = ownerTransform;
            OwnerScope = ownerScope;
        }

        public FootTransformMB? ResolveFootTransform()
        {
            if (OwnerScope?.Resolver != null &&
                OwnerScope.Resolver.TryResolve<FootTransformMB>(out var resolverFoot) &&
                resolverFoot != null)
            {
                return resolverFoot;
            }

            if (OwnerTransform != null)
            {
                var parentFoot = OwnerTransform.GetComponentInParent<FootTransformMB>();
                if (parentFoot != null)
                    return parentFoot;
            }

            if (OwnerScope is Component scopeComponent)
            {
                var scopeFoot = scopeComponent.GetComponent<FootTransformMB>();
                if (scopeFoot != null)
                    return scopeFoot;
            }

            return null;
        }
    }

    /// <summary>
    /// TargetChannel のランタイム（外部は基本 Hits を読むだけ）。
    /// </summary>
    public interface ITargetChannelRuntime
    {
        string Tag { get; }
        bool Enabled { get; set; }

        /// <summary>キャッシュ更新された最終フレーム。</summary>
        int LastUpdatedFrame { get; }

        /// <summary>内部キャッシュ（必要なら取得時に自動更新される）。</summary>
        List<DynamicSearchHit> Hits { get; }

        /// <summary>次回アクセスで必ず再検索させたいとき。</summary>
        void Invalidate();

        /// <summary>Interval を無視して即時更新。</summary>
        void ForceRefresh();
    }

    /// <summary>
    /// Tag で TargetChannelRuntime を管理する Hub。
    /// </summary>
    public interface ITargetChannelHub
    {
        int ChannelCount { get; }

        bool TryGetRuntime(string tag, out ITargetChannelRuntime runtime);

        /// <summary>
        /// 既存があれば返す。なければ作って返す（柔軟な生成パターン用）。
        /// replaceIfExists = true で上書き登録。
        /// </summary>
        ITargetChannelRuntime GetOrRegister(TargetChannelDef def, bool replaceIfExists = false);

        bool Unregister(string tag);

        void Clear();
    }
}
