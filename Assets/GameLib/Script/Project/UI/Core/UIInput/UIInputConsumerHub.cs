#nullable enable
using System.Collections.Generic;

namespace Game.UI
{
    // ================================================================
    // UIInputConsumerHub - 複数のIUIInputConsumerを集約するハブ
    // ================================================================
    //
    // ## 概要
    //
    // UIInputConsumerHubは、UIElementに登録された複数のIUIInputConsumerを
    // 集約管理するハブクラス。
    //
    // ## 必要性
    //
    // UIElementには複数の入力機能が付与される可能性がある:
    // - ボタン押下 (ButtonChannel consumer)
    // - スクロール (UIScrollConsumer)
    // - ドラッグ&ドロップ (UIDragConsumer)
    // - カスタム入力 (Custom IUIInputConsumer)
    //
    // VContainerで同一インターフェースを複数登録する場合、
    // IEnumerable<T>で取得する方法もあるが、
    // このHubを使うことで以下の利点がある:
    //
    // 1. 優先度によるソート済みリストの提供
    // 2. 動的な登録/解除
    // 3. 入力イベントの転送ロジックの統一
    //
    // ## 使用方法
    //
    // FeatureInstallerで各ConsumerをHubに登録する:
    // ```csharp
    // public void InstallFeature(IContainerBuilder builder, BaseLifetimeScope scope)
    // {
    //     builder.Register<MyConsumer>(Lifetime.Singleton);
    //     builder.RegisterBuildCallback(container => {
    //         var hub = container.Resolve<IUIInputConsumerHub>();
    //         var consumer = container.Resolve<MyConsumer>();
    //         hub.Register(consumer);
    //     });
    // }
    // ```
    //
    // ================================================================

    // ================================================================
    // IUIInputConsumerHub: ハブの公開API
    // ================================================================

    /// <summary>
    /// 複数のIUIInputConsumerを集約管理するハブのインターフェース。
    /// </summary>
    public interface IUIInputConsumerHub
    {
        /// <summary>
        /// 登録されているConsumerの数
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Consumerを登録する
        /// </summary>
        /// <param name="consumer">登録するConsumer</param>
        void Register(IUIInputConsumer consumer);

        /// <summary>
        /// Consumerを登録解除する
        /// </summary>
        /// <param name="consumer">解除するConsumer</param>
        void Unregister(IUIInputConsumer consumer);

        /// <summary>
        /// 登録されているすべてのConsumerを取得する（Priority順）
        /// </summary>
        /// <param name="results">結果を格納するリスト</param>
        void GetAllConsumers(List<IUIInputConsumer> results);

        /// <summary>
        /// 入力イベントを登録されているすべてのConsumerに転送する。
        /// Priority順に処理され、消費されたら停止する。
        /// </summary>
        /// <param name="inputEvent">入力イベント</param>
        /// <returns>いずれかのConsumerが消費した場合true</returns>
        bool Dispatch(in UIInputEvent inputEvent);

        /// <summary>
        /// すべてのConsumerを登録解除する
        /// </summary>
        void Clear();
    }

    // ================================================================
    // UIInputConsumerHub: メイン実装
    // ================================================================

    /// <summary>
    /// UIInputConsumerHubの実装。
    /// 
    /// ## 実装詳細
    /// 
    /// - Consumerリストは登録時にPriority順でソートされる
    /// - Dispatchは高Priority（大きい値）から順に処理される
    /// - 同一Priorityの場合は登録順
    /// </summary>
    public sealed class UIInputConsumerHub : IUIInputConsumerHub
    {
        // ----------------------------------------------------------------
        // フィールド
        // ----------------------------------------------------------------

        /// <summary>登録されたConsumerのリスト（Priority降順）</summary>
        readonly List<IUIInputConsumer> _consumers = new();

        /// <summary>ソートが必要かどうかのフラグ</summary>
        bool _needsSort;

        // ----------------------------------------------------------------
        // プロパティ
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public int Count => _consumers.Count;

        // ----------------------------------------------------------------
        // 登録/解除
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public void Register(IUIInputConsumer consumer)
        {
            if (consumer == null)
            {
                return;
            }

            if (!_consumers.Contains(consumer))
            {
                _consumers.Add(consumer);
                _needsSort = true;
            }
        }

        /// <inheritdoc/>
        public void Unregister(IUIInputConsumer consumer)
        {
            if (consumer == null)
            {
                return;
            }

            _consumers.Remove(consumer);
        }

        /// <inheritdoc/>
        public void Clear()
        {
            _consumers.Clear();
            _needsSort = false;
        }

        // ----------------------------------------------------------------
        // Consumer取得
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public void GetAllConsumers(List<IUIInputConsumer> results)
        {
            EnsureSorted();

            foreach (var consumer in _consumers)
            {
                results.Add(consumer);
            }
        }

        // ----------------------------------------------------------------
        // イベント転送
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public bool Dispatch(in UIInputEvent inputEvent)
        {
            EnsureSorted();

            foreach (var consumer in _consumers)
            {
                if (consumer.Consume(in inputEvent))
                {
                    return true;
                }
            }

            return false;
        }

        // ----------------------------------------------------------------
        // 内部メソッド
        // ----------------------------------------------------------------

        /// <summary>
        /// 必要に応じてPriority順でソートする
        /// </summary>
        void EnsureSorted()
        {
            if (!_needsSort)
            {
                return;
            }

            // Priority降順（大きい方が先）
            _consumers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            _needsSort = false;
        }
    }
}
