#nullable enable
// Game.Targeting
// ================================================================================
// TargetChannelRuntime - 1つの TargetChannel を実行・キャッシュするクラス
// ================================================================================
//
// ・同一フレーム内の複数リクエストはキャッシュを返す（Time.frameCount ベース）
// ・RefreshIntervalFrames を超えない限りキャッシュを再利用して軽量化
// ・円 / 円錐検索に対応し、EntitySearchService を利用してヒットを取得
// ・必要に応じて自分自身（Owner）を結果から除外
//
// コメントを多く入れ、意図と挙動を明確にしている。
// ================================================================================

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Game.Common;
using Game.Entity;
using Game.Search;

namespace Game.Targeting
{
    /// <summary>
    /// TargetChannel のランタイム。
    /// 「一フレーム内の複数リクエストはキャッシュ」を保証する（Time.frameCount）。
    /// </summary>
    public sealed class TargetChannelRuntime : ITargetChannelRuntime
    {
        readonly IDynamicSearchService _search;   // Dynamic 検索サービス（DI）
        readonly TargetChannelOwner _owner;      // 検索実行時のオーナー情報
        readonly TargetChannelDef _def;          // チャンネル定義（設定値）
        readonly List<DynamicSearchHit> _hits;    // キャッシュされた検索結果

        int _lastUpdatedFrame = int.MinValue;    // 最後にキャッシュを更新したフレーム

        public TargetChannelRuntime(IDynamicSearchService search, in TargetChannelOwner owner, TargetChannelDef def)
        {
            _search = search ?? throw new ArgumentNullException(nameof(search));
            _owner = owner;
            _def = def ?? throw new ArgumentNullException(nameof(def));

            // 初期容量は定義の ExpectedResultCount を利用（足りなければ List が自動拡張）
            _hits = new List<DynamicSearchHit>(Mathf.Max(0, def.ExpectedResultCount));
        }

        public string Tag => _def.Tag;

        public bool Enabled
        {
            get => _def.Enabled;
            set => _def.Enabled = value;
        }

        public int LastUpdatedFrame => _lastUpdatedFrame;

        /// <summary>
        /// 外部は基本これを読むだけ。必要なら内部で Query してキャッシュ更新する。
        /// </summary>
        public List<DynamicSearchHit> Hits
        {
            get
            {
                EnsureUpdated(ignoreInterval: false);
                return _hits;
            }
        }

        /// <summary>次回アクセスで必ず再検索させたいとき。</summary>
        public void Invalidate()
        {
            _lastUpdatedFrame = int.MinValue;
        }

        /// <summary>Interval を無視して即時更新。</summary>
        public void ForceRefresh()
        {
            EnsureUpdated(ignoreInterval: true);
        }

        // --------------------------------------------------------------------
        // Internal helpers
        // --------------------------------------------------------------------

        void EnsureUpdated(bool ignoreInterval)
        {
            MainThread.AssertMainThread(); // メインスレッド専用の設計

            if (!_def.Enabled)
            {
                _hits.Clear();
                return;
            }

            int frame = Time.frameCount;

            // 1) 同一フレームなら必ずキャッシュを返す
            if (frame == _lastUpdatedFrame)
                return;

            // 2) Interval が設定されていればそれを尊重
            if (!ignoreInterval && _def.RefreshIntervalFrames > 1)
            {
                int delta = frame - _lastUpdatedFrame;
                if (delta > 0 && delta < _def.RefreshIntervalFrames)
                    return;
            }

            _lastUpdatedFrame = frame;

            // ------------------------------------------------------------
            // Build query
            // ------------------------------------------------------------
            float2 origin = ResolveOrigin();
            float radius = Mathf.Max(0.01f, _def.Radius);

            if (_def.Kind == TargetQueryKind.Cone)
            {
                float2 forward = ResolveForward();
                float cosHalf = _def.CosHalfAngle;

                var q = new DynamicSearchQuery(
                    origin, radius,
                    forward, cosHalf,
                    kindMask: _def.KindMask,
                    requireActive: true,
                    filterId: _def.FilterId,
                    filterCategory: _def.FilterCategory);

                _search.Query(in q, _hits);
            }
            else
            {
                var q = new DynamicSearchQuery(
                    origin, radius,
                    kindMask: _def.KindMask,
                    requireActive: true,
                    filterId: _def.FilterId,
                    filterCategory: _def.FilterCategory);

                _search.Query(in q, _hits);
            }

            // ------------------------------------------------------------
            // Post filter (self)
            // ------------------------------------------------------------
            if (_def.ExcludeSelf && _owner.OwnerScope != null)
            {
                var self = _owner.OwnerScope;
                for (int i = _hits.Count - 1; i >= 0; i--)
                {
                    if (ReferenceEquals(_hits[i].Scope, self))
                        _hits.RemoveAt(i); // 自分自身を除外
                }
            }
        }

        // 原点を決定する。OwnerFoot が優先され、Fallback で Transform.position。
        float2 ResolveOrigin()
        {
            switch (_def.OriginSource)
            {
                case TargetOriginSource.OwnerFoot:
                    {
                        var foot = _owner.ResolveFootTransform();
                        if (foot != null)
                        {
                            var p = foot.FootWorldPosition;
                            return new float2(p.x, p.y);
                        }
                        var t = _owner.OwnerTransform.position;
                        return new float2(t.x, t.y);
                    }

                case TargetOriginSource.CustomTransform:
                    {
                        var tr = _def.CustomOriginTransform != null ? _def.CustomOriginTransform : _owner.OwnerTransform;
                        var p = tr.position;
                        return new float2(p.x, p.y);
                    }

                case TargetOriginSource.OwnerTransformPosition:
                default:
                    {
                        var t = _owner.OwnerTransform.position;
                        return new float2(t.x, t.y);
                    }
            }
        }

        // 円錐の Forward を決定する。正規化して返す。
        float2 ResolveForward()
        {
            Vector2 f;
            switch (_def.ForwardSource)
            {
                case TargetForwardSource.OwnerTransformRight:
                    f = _owner.OwnerTransform.right;
                    break;

                case TargetForwardSource.CustomTransformUp:
                    {
                        var tr = _def.CustomForwardTransform != null ? _def.CustomForwardTransform : _owner.OwnerTransform;
                        f = tr.up;
                        break;
                    }

                case TargetForwardSource.CustomTransformRight:
                    {
                        var tr = _def.CustomForwardTransform != null ? _def.CustomForwardTransform : _owner.OwnerTransform;
                        f = tr.right;
                        break;
                    }

                case TargetForwardSource.CustomVector:
                    f = _def.CustomForwardVector;
                    break;

                case TargetForwardSource.OwnerTransformUp:
                default:
                    f = _owner.OwnerTransform.up;
                    break;
            }

            float lenSq = f.sqrMagnitude;
            if (lenSq < 0.000001f)
                f = Vector2.up; // 万が一ゼロベクトルなら Up にフォールバック
            else
                f /= Mathf.Sqrt(lenSq); // 正規化

            return new float2(f.x, f.y);
        }
    }
}
