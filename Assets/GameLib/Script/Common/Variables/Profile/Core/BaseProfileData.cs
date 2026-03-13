// Game.Profile.BaseProfileData.cs
//
// BaseProfileData - inline profile data base class (SerializeReference-friendly).

using System;
using System.Collections.Generic;
using System.Reflection;

namespace Game.Profile
{
    [Serializable]
    public abstract class BaseProfileData : IProfileDefinition
    {
        static readonly Dictionary<Type, FieldInfo[]> _fieldCache = new();
        static readonly object _cacheLock = new object();

        public abstract Type ProfileType { get; }

        public IEnumerable<IProfileValueBinding> EnumerateBindings()
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

        public void CollectBindings(List<IProfileValueBinding> output)
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

        public int GetBindingCount()
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

        FieldInfo[] GetBindingFields()
        {
            var type = GetType();

            lock (_cacheLock)
            {
                if (_fieldCache.TryGetValue(type, out var cached))
                    return cached;
            }

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

            var currentType = type;
            while (currentType != null && currentType != typeof(object))
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

                    if (bindingType.IsAssignableFrom(fieldType))
                    {
                        result.Add(field);
                    }
                }

                currentType = currentType.BaseType;
            }

            return result.ToArray();
        }
    }
}
