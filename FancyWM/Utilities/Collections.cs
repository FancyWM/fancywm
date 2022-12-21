using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace FancyWM.Utilities
{
    internal static class Collections
    {
        private class SequenceComparer<T> : IEqualityComparer<IEnumerable<T>?>
        {
            public IEqualityComparer<T> Comparer { get; }

            public SequenceComparer(IEqualityComparer<T> comparer)
            {
                Comparer = comparer;
            }

            public bool Equals([AllowNull] IEnumerable<T> x, [AllowNull] IEnumerable<T> y)
            {
                if (x == null)
                    return y == null;
                else if (y == null)
                    return x == null;

                return x.SequenceEqual(y, Comparer);
            }

            public int GetHashCode([DisallowNull] IEnumerable<T> obj)
            {
                int hash = 0;
                foreach (var item in obj)
                {
                    HashCode.Combine(hash, item);
                }
                return hash;
            }
        }

        public static (IEnumerable<T> addList, IEnumerable<T> removeList, IEnumerable<T> persistList) Changes<T>(this IEnumerable<T> enumerable, IEnumerable<T> newEnumerable)
        {
            return enumerable.Changes(newEnumerable, EqualityComparer<T>.Default);
        }

        public static (IEnumerable<T> addList, IEnumerable<T> removeList, IEnumerable<T> persistList) Changes<T>(this IEnumerable<T> enumerable, IEnumerable<T> newEnumerable, IEqualityComparer<T> equalityComparer)
        {
            return (
                addList: newEnumerable.Except(enumerable, equalityComparer),
                removeList: enumerable.Except(newEnumerable, equalityComparer),
                persistList: enumerable.Intersect(newEnumerable, equalityComparer));
        }

        public static IEnumerable<(K Key, V Value)> AsPairs<K, V>(this IEnumerable<KeyValuePair<K, V>> keyValuePairs)
        {
            foreach (var kvp in keyValuePairs)
            {
                yield return (kvp.Key, kvp.Value);
            }
        }

        public static IEqualityComparer<IEnumerable<T>?> ToSequenceComparer<T>(this IEqualityComparer<T> comparer)
        {
            return new SequenceComparer<T>(comparer);
        }

        public static int IndexOf<T>(this IReadOnlyList<T> list, T value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (Equals(value, list[i]))
                {
                    return i;
                }
            }
            return -1;
        }

        public static int IndexOf<T>(this IReadOnlyList<T> list, Predicate<T> pred)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (pred(list[i]))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
