// Game.Profile.BaseProfileSO.cs
//
// BaseProfileSO - Profile SO の基底クラス
// リフレクションベースのフィールドバインディング列挙を提供

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Game.Kernel.IR;
using Game.Scalar;
using Game.VarStoreKeys;

namespace Game.Profile
{
    /// <summary>
    /// Profile SO の基底クラス。
    /// IProfileValueBinding を実装したフィールドを自動的に列挙し、
    /// Blackboard/Scalar へのバインディングを適用する。
    /// </summary>
    public abstract class BaseProfileSO : ScriptableObject, IProfileDefinition
    {
        // ================================================================
        // Static Reflection Cache
        // ================================================================

        // 型ごとのフィールドキャッシュ（static で共有）
        static readonly Dictionary<Type, FieldInfo[]> _fieldCache = new();
        static readonly object _cacheLock = new object();

        // ================================================================
        // Public API
        // ================================================================

        /// <summary>
        /// このプロファイルに定義された全てのバインディングを列挙する。
        /// リフレクションでフィールドをスキャンし、IProfileValueBinding を実装した
        /// フィールドの値を返す。
        /// </summary>
        public virtual IEnumerable<IProfileValueBinding> EnumerateBindings()
        {
            var fields = GetBindingFields();
            for (int i = 0; i < fields.Length; i++)
            {
                var value = fields[i].GetValue(this);
                if (value is IProfileValueBinding binding)
                {
                    yield return binding;
                }
            }
        }

        /// <summary>
        /// バインディングをリストに収集する（アロケーション制御用）。
        /// </summary>
        public virtual void CollectBindings(List<IProfileValueBinding> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            var fields = GetBindingFields();
            for (int i = 0; i < fields.Length; i++)
            {
                var value = fields[i].GetValue(this);
                if (value is IProfileValueBinding binding)
                {
                    output.Add(binding);
                }
            }
        }

        /// <summary>
        /// バインディングの数を取得（アロケーションなし）。
        /// </summary>
        public virtual int GetBindingCount()
        {
            var fields = GetBindingFields();
            int count = 0;
            for (int i = 0; i < fields.Length; i++)
            {
                var value = fields[i].GetValue(this);
                if (value is IProfileValueBinding)
                {
                    count++;
                }
            }
            return count;
        }

        public Type ProfileType => GetType();

        public bool TryCreateScalarDeclarations(ScalarOwnerIdentity owner, SourceLocationIR source, out ScalarDeclarationInput[] declarations, out string failureReason)
        {
            return ProfileScalarDeclarationProjection.TryCreateScalarDeclarations(EnumerateBindings(), owner, ProfileType.Name, source, out declarations, out failureReason);
        }

        // ================================================================
        // Internal
        // ================================================================

        FieldInfo[] GetBindingFields()
        {
            var type = GetType();

            lock (_cacheLock)
            {
                if (_fieldCache.TryGetValue(type, out var cached))
                    return cached;
            }

            // キャッシュがない場合は構築
            var fields = BuildFieldCache(type);

            lock (_cacheLock)
            {
                _fieldCache[type] = fields;
            }

            return fields;
        }

        static FieldInfo[] BuildFieldCache(Type type)
        {
            var bindingType = typeof(IProfileValueBinding);
            var result = new List<FieldInfo>();

            // 継承階層を遡ってフィールドを収集
            var currentType = type;
            while (currentType != null && currentType != typeof(ScriptableObject))
            {
                var fields = currentType.GetFields(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly
                );

                for (int i = 0; i < fields.Length; i++)
                {
                    var field = fields[i];
                    var fieldType = field.FieldType;

                    // IProfileValueBinding を実装しているかチェック
                    if (bindingType.IsAssignableFrom(fieldType))
                    {
                        result.Add(field);
                    }
                }

                currentType = currentType.BaseType;
            }

            return result.ToArray();
        }

        // ================================================================
        // Debug
        // ================================================================

#if UNITY_EDITOR
        /// <summary>
        /// Editor 用: バインディングフィールド情報をデバッグ出力。
        /// </summary>
        [ContextMenu("Debug: Log Binding Fields")]
        protected void DebugLogBindingFields()
        {
            var fields = GetBindingFields();
            Debug.Log($"[{GetType().Name}] Found {fields.Length} binding fields:");

            for (int i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                var value = field.GetValue(this);

                if (value is IProfileValueBinding binding)
                {
                    var bbId = binding.BlackboardKey;
                    var bb = "(none)";
                    if (bbId != 0)
                    {
                        if (!VarIdResolver.TryGetStableKey(bbId, out var stable) || string.IsNullOrEmpty(stable))
                            bb = bbId.ToString();
                        else
                            bb = stable;
                    }
                    var scalar = binding.ScalarKey.Id != 0 ? binding.ScalarKey.Name : "(none)";
                    Debug.Log($"  [{i}] {field.Name} ({field.FieldType.Name}) -> BB: {bb}, Scalar: {scalar}");
                }
                else
                {
                    Debug.Log($"  [{i}] {field.Name} ({field.FieldType.Name}) -> (null or invalid)");
                }
            }
        }
#endif

        // ================================================================
        // Cache Management
        // ================================================================

        /// <summary>
        /// フィールドキャッシュをクリア（主にテスト用）。
        /// </summary>
        public static void ClearFieldCache()
        {
            lock (_cacheLock)
            {
                _fieldCache.Clear();
            }
        }
    }
}
