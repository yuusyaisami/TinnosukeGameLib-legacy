// Game.Field
using System.Collections.Generic;

namespace Game.Field
{
    public interface IFieldRegistry
    {
        void Register(FieldMB field);
        void Unregister(FieldMB field);

        IReadOnlyCollection<FieldMB> All { get; }

        IEnumerable<T> Enumerate<T>() where T : FieldMB;
    }

    public sealed class FieldRegistry : IFieldRegistry
    {
        readonly HashSet<FieldMB> _fields = new HashSet<FieldMB>();

        public IReadOnlyCollection<FieldMB> All => _fields;

        public void Register(FieldMB field)
        {
            if (field == null) return;
            _fields.Add(field);
        }

        public void Unregister(FieldMB field)
        {
            if (field == null) return;
            _fields.Remove(field);
        }

        public IEnumerable<T> Enumerate<T>() where T : FieldMB
        {
            foreach (var f in _fields)
            {
                if (f is T t) yield return t;
            }
        }
    }
}
