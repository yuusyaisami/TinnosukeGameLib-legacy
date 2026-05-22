#nullable enable
using System.Collections.Generic;

namespace Game.UI
{
    // ================================================================
    // UIInputConsumerHub - 褁E��のIUIInputConsumerを集紁E��るハチE
    // ================================================================
    //
    // ## 概要E
    //
    // UIInputConsumerHubは、UIElementに登録された褁E��のIUIInputConsumerめE
    // 雁E��E��琁E��るハブクラス、E
    //
    // ## 忁E��性
    //
    // UIElementには褁E��の入力機�Eが付与される可能性があめE
    // - ボタン押丁E(ButtonChannel consumer)
    // - スクロール (UIScrollConsumer)
    // - ドラチE��&ドロチE�E (UIDragConsumer)
    // - カスタム入劁E(Custom IUIInputConsumer)
    //
    // VContainerで同一インターフェースを褁E��登録する場合、E
    // IEnumerable<T>で取得する方法もあるが、E
    // こ�EHubを使ぁE��とで以下�E利点があめE
    //
    // 1. 優先度によるソート済みリスト�E提侁E
    // 2. 動的な登録/解除
    // 3. 入力イベント�E転送ロジチE��の統一
    //
    // ## 使用方況E
    //
    // FeatureInstallerで各ConsumerをHubに登録する:
    // ```csharp
    // public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
    // {
    //     builder.Register<MyConsumer>(RuntimeLifetime.Singleton);
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
    /// 褁E��のIUIInputConsumerを集紁E��琁E��るハブ�Eインターフェース、E
    /// </summary>
    public interface IUIInputConsumerHub
    {
        /// <summary>
        /// 登録されてぁE��Consumerの数
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
        /// 登録されてぁE��すべてのConsumerを取得する！Eriority頁E��E
        /// </summary>
        /// <param name="results">結果を格納するリスチE/param>
        void GetAllConsumers(List<IUIInputConsumer> results);

        /// <summary>
        /// 入力イベントを登録されてぁE��すべてのConsumerに転送する、E
        /// Priority頁E��処琁E��れ、消費されたら停止する、E
        /// </summary>
        /// <param name="inputEvent">入力イベンチE/param>
        /// <returns>ぁE��れかのConsumerが消費した場吁Erue</returns>
        bool Dispatch(in UIInputEvent inputEvent);

        /// <summary>
        /// すべてのConsumerを登録解除する
        /// </summary>
        void Clear();
    }

    // ================================================================
    // UIInputConsumerHub: メイン実裁E
    // ================================================================

    /// <summary>
    /// UIInputConsumerHubの実裁E��E
    /// 
    /// ## 実裁E��細
    /// 
    /// - Consumerリスト�E登録時にPriority頁E��ソートされる
    /// - Dispatchは高Priority�E�大きい値�E�から頁E��処琁E��れる
    /// - 同一Priorityの場合�E登録頁E
    /// </summary>
    public sealed class UIInputConsumerHub : IUIInputConsumerHub
    {
        // ----------------------------------------------------------------
        // フィールチE
        // ----------------------------------------------------------------

        /// <summary>登録されたConsumerのリスト！Eriority降頁E��E/summary>
        readonly List<IUIInputConsumer> _consumers = new();

        /// <summary>ソートが忁E��かどぁE��のフラグ</summary>
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
        // Consumer取征E
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
        // イベント転送E
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
        // 冁E��メソチE��
        // ----------------------------------------------------------------

        /// <summary>
        /// 忁E��に応じてPriority頁E��ソートすめE
        /// </summary>
        void EnsureSorted()
        {
            if (!_needsSort)
            {
                return;
            }

            // Priority降頁E��大きい方が�E�E�E
            _consumers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            _needsSort = false;
        }
    }
}

