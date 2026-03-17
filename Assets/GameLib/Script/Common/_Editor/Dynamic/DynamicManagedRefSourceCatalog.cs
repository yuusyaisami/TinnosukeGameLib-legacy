// DynamicManagedRefSourceCatalog
//
// ManagedRef 系の generic source 登録を自動化する editor catalog。
// TypedDynamicValueDrawer から利用される。

#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Game.Common;
using Game.Profile;

namespace Game.Common.Editor
{
    /// <summary>
    /// target type に対して ManagedRef 系の generic source type を自動解決する catalog。
    /// </summary>
    internal static class DynamicManagedRefSourceCatalog
    {
        static readonly Dictionary<Type, Type> s_literalSourceCache = new();
        static readonly Dictionary<Type, Type[]> s_assetSourceCache = new();
        static bool s_initialized;

        /// <summary>
        /// targetType に対応する <see cref="ManagedRefLiteralSource{TValue}"/> の closed generic 型を返す。
        /// </summary>
        public static bool TryGetLiteralSourceType(Type targetType, out Type sourceType)
        {
            EnsureInitialized();
            return s_literalSourceCache.TryGetValue(targetType, out sourceType);
        }

        /// <summary>
        /// targetType に対応する <see cref="ManagedRefAssetSource{TAsset,TValue}"/> の closed generic 型を追加する。
        /// </summary>
        public static void AppendAssetSourceTypes(Type targetType, List<Type> dest)
        {
            EnsureInitialized();
            if (s_assetSourceCache.TryGetValue(targetType, out var types))
            {
                dest.AddRange(types);
            }
        }

        static void EnsureInitialized()
        {
            if (s_initialized) return;
            s_initialized = true;

            BuildLiteralSourceCache();
            BuildAssetSourceCache();
        }

        static void BuildLiteralSourceCache()
        {
            // BaseProfileData 自体を登録（DynamicValue<BaseProfileData> で polymorphic Preset 選択を可能にする）
            s_literalSourceCache[typeof(BaseProfileData)] =
                typeof(ManagedRefLiteralSource<>).MakeGenericType(typeof(BaseProfileData));

            // BaseProfileData 派生型を収集
            var profileDataTypes = TypeCache.GetTypesDerivedFrom<BaseProfileData>();
            foreach (var t in profileDataTypes)
            {
                if (t.IsAbstract || t.IsGenericTypeDefinition) continue;
                var closedType = typeof(ManagedRefLiteralSource<>).MakeGenericType(t);
                s_literalSourceCache[t] = closedType;
            }

            // IDynamicManagedRefValue 実装型を収集
            var markerTypes = TypeCache.GetTypesDerivedFrom<IDynamicManagedRefValue>();
            foreach (var t in markerTypes)
            {
                if (t.IsAbstract || t.IsGenericTypeDefinition || t.IsInterface) continue;
                if (s_literalSourceCache.ContainsKey(t)) continue; // 既に登録済み
                var closedType = typeof(ManagedRefLiteralSource<>).MakeGenericType(t);
                s_literalSourceCache[t] = closedType;
            }
        }

        static void BuildAssetSourceCache()
        {
            var assetSourceMap = new Dictionary<Type, List<Type>>();

            // IDynamicValueAsset<T> を実装する ScriptableObject を収集
            var allSOTypes = TypeCache.GetTypesDerivedFrom<ScriptableObject>();
            foreach (var soType in allSOTypes)
            {
                if (soType.IsAbstract || soType.IsGenericTypeDefinition) continue;

                foreach (var iface in soType.GetInterfaces())
                {
                    if (!iface.IsGenericType) continue;
                    if (iface.GetGenericTypeDefinition() != typeof(IDynamicValueAsset<>)) continue;

                    var valueType = iface.GetGenericArguments()[0];
                    var closedSourceType = typeof(ManagedRefAssetSource<,>).MakeGenericType(soType, valueType);

                    if (!assetSourceMap.TryGetValue(valueType, out var list))
                    {
                        list = new List<Type>();
                        assetSourceMap[valueType] = list;
                    }
                    list.Add(closedSourceType);
                }
            }

            foreach (var kvp in assetSourceMap)
            {
                s_assetSourceCache[kvp.Key] = kvp.Value.ToArray();
            }

            // BaseProfileData 用の汎用 asset source を登録
            // IDynamicValueAsset<T> の共変性により、任意の ProfileSO wrapper から Preset を取得可能
            if (!s_assetSourceCache.ContainsKey(typeof(BaseProfileData)))
            {
                s_assetSourceCache[typeof(BaseProfileData)] = new[] { typeof(BindingPresetAssetSource) };
            }
            else
            {
                var existing = s_assetSourceCache[typeof(BaseProfileData)];
                var extended = new Type[existing.Length + 1];
                existing.CopyTo(extended, 0);
                extended[existing.Length] = typeof(BindingPresetAssetSource);
                s_assetSourceCache[typeof(BaseProfileData)] = extended;
            }
        }
    }
}

#endif
