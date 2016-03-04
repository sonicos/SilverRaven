using System.Collections.Generic;
using System.Linq;

namespace SilverRaven.Utilities
{
    internal static class DictionaryExtensions
    {
        public static T MergeLeft<T, TKey, TValue>(this T self, params IDictionary<TKey, TValue>[] others)
            where T : IDictionary<TKey, TValue>, new()
        {
            var outMap = new T();
            foreach (var pair in new List<IDictionary<TKey, TValue>> {self}.Concat(others).SelectMany(src => src))
            {
                outMap[pair.Key] = pair.Value;
            }
            return outMap;
        }
    }
}