using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;

namespace IRAAS.ImageProcessing
{
    public static class DictionaryExtensions
    {
        public static IDictionary<TKey, TValue> Clone<TKey, TValue>(
            this IDictionary<TKey, TValue> src)
        {
            return src?.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value
            ) ?? new Dictionary<TKey, TValue>();
        }

        public static IDictionary<string, string> Clone(
            this HttpHeaders headers)
        {
            return headers?.ToDictionary(
                kvp => kvp.Key,
                kvp => string.Join(",", kvp.Value)
            ) ?? new Dictionary<string, string>();
        }

        public static IDictionary<string, string> Clone(
            this WebHeaderCollection headers)
        {
            return headers?.ToDictionary()
                ?? new Dictionary<string, string>();
        }
    }
}