using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Http;

namespace IRAAS.ImageProcessing;

public static class WebHeaderCollectionExtensions
{
    public static IDictionary<string, string> ToDictionary(
        this WebHeaderCollection headers)
    {
        return headers?.AllKeys.Select(key =>
                new
                {
                    key,
                    value = headers[(string) key]
                }).ToDictionary(o => o.key, o => o.value)
            ?? new Dictionary<string, string>();
    }

    public static IDictionary<string, string> ToDictionary(
        this IHeaderDictionary headers)
    {
        return headers?.Keys.Select(key =>
                new
                {
                    key,
                    value = string.Join(",", headers[key])
                }).ToDictionary(o => o.key, o => o.value)
            ?? new Dictionary<string, string>();
    }
}