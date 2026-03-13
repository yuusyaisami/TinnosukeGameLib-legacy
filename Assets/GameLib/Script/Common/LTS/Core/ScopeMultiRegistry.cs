using System;
using System.Collections.Generic;

namespace Game
{
    // ================================================================
    // IScopeMultiRegistry - スコープローカル複数登録レジストリ
    // ================================================================
    //
    // ## 概要
    //
    // この LifetimeScope（=Container）ローカルに "複数登録サービス" を集約する。
    // DI の親探索に頼らず、自分のスコープで登録した分だけ取れる。
    //
    // ## 背景
    //
    // VContainer の Resolve は親スコープを自動探索するため、
    // 「このスコープで登録されたインスタンスだけ」を取得するのが難しい。
    // IScopeMultiRegistry は、複数存在し得るサービスを明示的にローカル登録し、
    // 親探索に頼らない取得を可能にする。
    //
    // ## 使用例
    //
    // ```csharp
    // // 登録時
    // builder.RegisterAsScopeMulti<IChannelHubService, AnimationSpriteHubService>(Lifetime.Singleton);
    //
    // // 取得時（ローカルのみ）
    // var registry = container.Resolve<IScopeMultiRegistry>();
    // var localHubs = registry.GetAll<IChannelHubService>();
    // ```
    //
    // ================================================================

    /// <summary>
    /// スコープローカルの複数サービス登録レジストリ。
    /// </summary>
    public interface IScopeMultiRegistry
    {
        /// <summary>
        /// サービス型とインスタンスを登録する。
        /// </summary>
        void Add(Type serviceType, object instance);

        /// <summary>
        /// 指定型で登録されたすべてのインスタンスを取得する。
        /// </summary>
        IReadOnlyList<object> GetAll(Type serviceType);

        /// <summary>
        /// サービス型とインスタンスを登録する（ジェネリック版）。
        /// </summary>
        void Add<T>(T instance) where T : class;

        /// <summary>
        /// 指定型で登録されたすべてのインスタンスを取得する（ジェネリック版）。
        /// </summary>
        IReadOnlyList<T> GetAll<T>() where T : class;

        /// <summary>
        /// 登録が1つだけの場合、その値を取得する。
        /// </summary>
        bool TryGetSingle<T>(out T value) where T : class;

        /// <summary>
        /// 指定型の登録数を取得する。
        /// </summary>
        int Count<T>() where T : class;

        /// <summary>
        /// 指定型の登録数を取得する。
        /// </summary>
        int Count(Type serviceType);
    }

    /// <summary>
    /// IScopeMultiRegistry の実装。
    /// </summary>
    public sealed class ScopeMultiRegistry : IScopeMultiRegistry
    {
        readonly Dictionary<Type, List<object>> _map = new();

        public void Add(Type serviceType, object instance)
        {
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));
            if (!serviceType.IsInstanceOfType(instance))
                throw new ArgumentException($"instance is not assignable to {serviceType.Name}");

            if (!_map.TryGetValue(serviceType, out var list))
            {
                list = new List<object>(4);
                _map.Add(serviceType, list);
            }

            // 同一インスタンス二重登録防止
            if (!list.Contains(instance))
                list.Add(instance);
        }

        public IReadOnlyList<object> GetAll(Type serviceType)
        {
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));
            return _map.TryGetValue(serviceType, out var list) ? list : Array.Empty<object>();
        }

        public void Add<T>(T instance) where T : class
        {
            Add(typeof(T), instance);
        }

        public IReadOnlyList<T> GetAll<T>() where T : class
        {
            var src = GetAll(typeof(T));
            if (src.Count == 0)
                return Array.Empty<T>();

            var dst = new T[src.Count];
            for (int i = 0; i < src.Count; i++)
            {
                dst[i] = (T)src[i];
            }
            return dst;
        }

        public bool TryGetSingle<T>(out T value) where T : class
        {
            var list = GetAll<T>();
            if (list.Count == 1)
            {
                value = list[0];
                return true;
            }
            value = null;
            return false;
        }

        public int Count<T>() where T : class
        {
            return Count(typeof(T));
        }

        public int Count(Type serviceType)
        {
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));
            return _map.TryGetValue(serviceType, out var list) ? list.Count : 0;
        }
    }
}
