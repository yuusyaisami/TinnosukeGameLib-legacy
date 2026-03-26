#nullable enable
using System.Collections.Generic;

namespace Game.Channel
{
    static class ListPool<T>
    {
        static readonly Stack<List<T>> Pool = new();

        public static List<T> Get()
        {
            if (Pool.Count > 0)
                return Pool.Pop();
            return new List<T>();
        }

        public static void Release(List<T> list)
        {
            list.Clear();
            Pool.Push(list);
        }
    }
}
