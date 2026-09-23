using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using PeanutButter.Utils;

namespace IRAAS.Middleware;

public class ConcurrencyMiddleware : IMiddleware
{
    private readonly ILogger<ConcurrencyMiddleware> _logger;
    private readonly bool _shareConcurrentRequests;
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly LogLevel _logLevel;

    private class CachedResponse
    {
        public string Query { get; }
        public byte[] Body { get; }
        public IHeaderDictionary Headers { get; }

        public CachedResponse(
            string query,
            byte[] body,
            IHeaderDictionary headers)
        {
            Query = query;
            Body = body;
            Headers = Clone(headers);
        }

        private HeaderDictionary Clone(IHeaderDictionary source)
        {
            var dict = source.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            return new HeaderDictionary(dict);
        }
    }

    public ConcurrencyMiddleware(
        IAppSettings appSettings,
        ILogger<ConcurrencyMiddleware> logger)
    {
        _logger = logger;
        _concurrencyLimiter = new SemaphoreSlim(appSettings.MaxConcurrency);
        _shareConcurrentRequests = appSettings.ShareConcurrentRequests;
        _logLevel = appSettings.IRAASLogLevel;
    }

    private static readonly ConcurrentDictionary<string, TaskCompletionSource<CachedResponse>>
        CurrentRequests = new();

    private async Task Throttle(
        HttpContext context,
        RequestDelegate next)
    {
        try
        {
            await _concurrencyLimiter.WaitAsync();
            await next(context);
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    public async Task InvokeAsync(
        HttpContext context,
        RequestDelegate next)
    {
        // sharing of requests is disabled -- only apply throttling
        if (!_shareConcurrentRequests)
        {
            await Throttle(context, next);
            return;
        }

        await ShareConcurrentRequestResult(context, next);
    }

    private async Task ShareConcurrentRequestResult(
        HttpContext context,
        RequestDelegate next)
    {
        var queryString = context.Request.QueryString.ToString();
        var completionSource = new TaskCompletionSource<CachedResponse>();
        var cacheProhibited = HasNoStoreCacheControlHeader(context.Request) ||
                              HasTestPagePath(context.Request) ||
                              context.Request.Method == HttpMethods.Post;
        if (!cacheProhibited)
        {
            // look for an existing current query with the same parameters
            if (!CurrentRequests.TryAdd(queryString, completionSource) &&
                CurrentRequests.TryGetValue(queryString, out var src))
            {
                // a request is currently underway for this query
                // -> subscribe to the completed result
                await ReuseResult(context, src);
                return;
            }
        }

        await _concurrencyLimiter.WaitAsync();
        try
        {
            await PerformFullRequestWith(
                context,
                next,
                queryString,
                completionSource,
                cacheProhibited
            );
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    private bool HasTestPagePath(HttpRequest req)
    {
        return "/test".Equals(req.Path, StringComparison.OrdinalIgnoreCase) ||
               "/size".Equals(req.Path, StringComparison.OrdinalIgnoreCase);
    }

    private bool HasNoStoreCacheControlHeader(
        HttpRequest request
    )
    {
        var cacheControl = request.Headers["Cache-Control"];
        if (cacheControl == StringValues.Empty)
        {
            return false;
        }

        return cacheControl.Any(
            s => s.Split(',').Any(
                part => part.Trim().Equals(
                    "no-store",
                    StringComparison.OrdinalIgnoreCase
                )
            )
        );
    }

    private async Task PerformFullRequestWith(HttpContext context,
        RequestDelegate next,
        string queryString,
        TaskCompletionSource<CachedResponse> completionSource,
        bool cacheProhibited
    )
    {
        var originalBody = context.Response.Body;
        if (!cacheProhibited)
        {
            CurrentRequests.TryAdd(queryString, completionSource);
        }

        await using var memStream = new MemoryStream();
        try
        {
            // temporarily swap out the response body stream
            // with a memory stream which we can rewind and re-read
            context.Response.Body = memStream;

            // allow the rest of the pipeline to happen
            await next(context);

            // read out the result to share with any waiting
            // requests for the same parameters
            memStream.Rewind();
            var buffer = memStream.ReadAllBytes();

            // we have to copy out the result to the original
            // one-way stream
            memStream.Rewind();
            await memStream.CopyToAsync(originalBody);

            LogRequest(queryString);
            // service requests waiting on a duplicate result
            completionSource.SetResult(
                new CachedResponse(
                    queryString,
                    buffer,
                    context.Response.Headers
                )
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                new EventId(42),
                ex,
                $"Error whilst attempting reuse of result for concurrent request ({queryString})"
            );
        }
        finally
        {
            // this request is no longer "current"
            // -> remove from collection
            if (!cacheProhibited)
            {
                CurrentRequests.TryRemove(queryString, out _);
            }

            context.Response.Body = originalBody;
        }
    }

    private void LogRequest(string queryString)
    {
        if (_logLevel > LogLevel.Information)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(queryString))
        {
            return;
        }

        queryString = queryString.Substring(1); // remove leading ?

        var parameters = queryString.Split("&")
            .Select(
                part =>
                {
                    var sub = part.Split('=');
                    var key = sub.First();
                    var value = string.Join("&", sub.Skip(1));
                    return (key, value);
                }
            )
            .ToDictionary(p => p.key, p => p.value);
        if (!parameters.TryGetValue("url", out var url))
        {
            url = "(not set)";
        }

        parameters.Remove("url");
        var longest = parameters.Keys.Aggregate(
            3,
            (acc, cur) => cur.Length > acc
                ? cur.Length
                : acc
        );

        var lines = new[]
        {
            "Serviced request:",
            $"{"url".PadRight(longest)}: {HttpUtility.UrlDecode(url)}"
        }.Concat(
            parameters.Select(
                kvp => $"{kvp.Key.PadRight(longest)}: {HttpUtility.UrlDecode(kvp.Value)}"
            )
        );
        _logger.LogInformation(
            string.Join(
                "\n",
                lines.Select(l => $"  {l}")
            )
        );
    }

    private async Task ReuseResult(
        HttpContext context,
        TaskCompletionSource<CachedResponse> src)
    {
        var response = await src.Task;
        foreach (var header in response.Headers)
        {
            context.Response.Headers[header.Key] = header.Value;
        }

        await context.Response.Body.WriteAsync(response.Body);
        _logger.LogInformation(
            $"Re-used: {response.Query}"
        );
    }
}