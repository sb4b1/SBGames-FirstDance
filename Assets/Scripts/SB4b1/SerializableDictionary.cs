using System;
using System.Collections.Generic;
using UnityEngine;

namespace SB4b1.Helpers
{
    [Serializable]
    public class SerializableDictionary<TKey, TValue>
    {
        [Serializable]
        public struct Pair
        {
            public TKey Key;
            public TValue Value;
        }

        [SerializeField] private List<Pair> pairs = new();

        private Dictionary<TKey, TValue> dictionary;

        public Dictionary<TKey, TValue> ToDictionary()
        {
            if (dictionary == null)
            {
                dictionary = new Dictionary<TKey, TValue>();
                foreach (var pair in pairs)
                {
                    if (!dictionary.ContainsKey(pair.Key))
                        dictionary.Add(pair.Key, pair.Value);
                }
            }

            return dictionary;
        }

        public List<Pair> GetPairs() => pairs;

        public void Clear()
        {
            pairs.Clear();
            dictionary?.Clear();
        }
    }
}