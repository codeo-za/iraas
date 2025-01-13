// define USE_WEBREQUEST to use WebRequest.Create for
// all image fetchers. This allows more protocols (specifically
// ftp), but will cost a little more per request as, under the covers,
// (as discovered by decompilation at time of writing), WebRequest.Create,
// for an http(s) request, will create a new HttpClient, which is
// not optimal according to:
// https://stackoverflow.com/a/35045301/1697008
// #define USE_WEBREQUEST

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PeanutButter.Utils;
#if USE_WEBREQUEST
#else
using System.Net.Http;
using System.Linq;
using System.Threading;

#endif

namespace IRAAS.ImageProcessing;

public interface IUrlFetcher
{
    Task<StreamAndHeaders> Fetch(string url, IDictionary<string, string> headers);
}

public class UrlFetcher : IUrlFetcher
{
    private readonly IAppSettings _appSettings;
    private readonly ILogger<UrlFetcher> _logger;


    public UrlFetcher(
        IAppSettings appSettings,
        ILogger<UrlFetcher> logger
    )
    {
        _appSettings = appSettings;
        _logger = logger;
    }

#if USE_WEBREQUEST
#else
    private static readonly HttpClient HttpClient;
    static UrlFetcher()
    {
        HttpClient = new HttpClient();
    }
#endif

#if USE_WEBREQUEST
        public async Task<StreamAndHeaders> Fetch(
            string url,
            IDictionary<string, string> headers)
        {
            var req = WebRequest.Create(url);
            req.Timeout = _appSettings.MaxImageFetchTimeInMilliseconds;
            SetRequestHeaders(url, headers, req);

            try
            {
                var res = await req.GetResponseAsync();
                return new StreamAndHeaders(
                    new WebResponseStream(res, _appSettings),
                    res.Headers.ToDictionary()
                );
            }
            catch (WebException ex) when (ex.Status == WebExceptionStatus.Timeout)
            {
                throw new RequestTimedOutException(
                    url,
                    ex.Response
                );
            }
            catch (WebException ex)
            {
                if (!(ex.Response is HttpWebResponse httpResponse))
                {
                    throw;
                }

                if (HttpHandlers.TryGetValue(httpResponse.StatusCode, out var handler))
                {
                    return handler(req, httpResponse);
                }

                throw new ImageProviderErrorException(
                    httpResponse.StatusCode,
                    url,
                    req.Headers.ToDictionary().Clone(),
                    ex.Response.Headers.ToDictionary().Clone()
                );
            }
        }

        private Dictionary<HttpStatusCode, Func<WebRequest, HttpWebResponse, StreamAndHeaders>> HttpHandlers =
            new Dictionary<HttpStatusCode, Func<WebRequest, HttpWebResponse, StreamAndHeaders>>()
            {
                [HttpStatusCode.NotModified] = ThrowNotModifiedException,
            };


        private static StreamAndHeaders ThrowNotModifiedException(
            WebRequest request,
            HttpWebResponse response
        )
        {
            throw new NotModifiedException();
        }

        private void SetRequestHeaders(
            string url,
            IDictionary<string, string> headers,
            WebRequest req)
        {
            headers?.ForEach(
                kvp => req.Headers[kvp.Key] = kvp.Value
            );
            OverrideUnreasonableHeaders(url, req);
        }

        private void OverrideUnreasonableHeaders(
            string url,
            WebRequest req)
        {
            var host = new Uri(url).Host;
            req.Headers["Host"] = host;
            req.Headers["Referrer"] = host;
            req.Headers["Origin"] = host;
            req.Headers.Remove(HttpRequestHeader.Connection); // do not honor keep-alive from caller
            if (req is HttpWebRequest httpWebRequest)
            {
                httpWebRequest.KeepAlive =
 _appSettings.EnableConnectionKeepAlive; // ensure we don't add our own keep-alive
            }

            req.Headers["Accept"] = "image/*";
        }

#else
    public async Task<StreamAndHeaders> Fetch(
        string url,
        IDictionary<string, string> headers
    )
    {
        Exception lastException = null;
        var attempts = _appSettings.MaxUrlFetchRetries + 1;
        for (var i = 0; i < attempts; i++)
        {
            try
            {
                return await PerformFetchOperation(url, headers);
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogDebug($"Url fetch failed: {ex}");
                Thread.Sleep(10);
            }
        }

        throw lastException!;
    }

    private async Task<StreamAndHeaders> PerformFetchOperation(
        string url,
        IDictionary<string, string> headers
    )
    {
        var request = new HttpRequestMessage()
        {
            RequestUri = new Uri(url),
        };
        SetRequestHeaders(url, headers, request);
        var cancellationTokenSource = new CancellationTokenSource(
            _appSettings.MaxImageFetchTimeInMilliseconds
        );
        HttpResponseMessage response;
        try
        {
            response = await HttpClient.SendAsync(
                request,
                cancellationTokenSource.Token
            );
        }
        catch (TaskCanceledException)
        {
            throw new RequestTimedOutException(
                url,
                request.Headers.Clone()
            );
        }

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var stream = new WebResponseStream(
                await response.Content.ReadAsStreamAsync(cancellationTokenSource.Token),
                _appSettings
            );
            return new StreamAndHeaders(
                stream,
                response.Headers.ToDictionary(
                    kvp => kvp.Key,
                    kvp => string.Join(",", kvp.Value)
                )
            );
        }

        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            throw new NotModifiedException();
        }

        if (ShouldHaveLocationHeader.Contains(response.StatusCode))
        {
            // getting here with an http response which should have
            //    a Location header means that the upstream boo-booed and
            //    left that header out!
            // -> translate into a 500 to prevent a CDN from caching it
            response.StatusCode = HttpStatusCode.InternalServerError;
        }

        throw new ImageProviderErrorException(
            response.StatusCode,
            url,
            request.Headers.Clone(),
            response?.Headers.Clone()
        );
    }

    private static readonly HashSet<HttpStatusCode> ShouldHaveLocationHeader =
        new HashSet<HttpStatusCode>()
        {
            HttpStatusCode.Moved,
            HttpStatusCode.MovedPermanently,
            HttpStatusCode.Found,
            HttpStatusCode.Redirect
        };

    private void SetRequestHeaders(
        string url,
        IDictionary<string, string> headers,
        HttpRequestMessage req
    )
    {
        var sanitisedHeaders = CloneHeadersWithHostOverride(
            url,
            headers
        );

        sanitisedHeaders.ForEach(
            kvp =>
            {
                try
                {
                    req.Headers.Add(kvp.Key, kvp.Value);
                }
                catch
                {
                    /*
                     intentionally left blank
                     - asp.net core is quite strict about the headers
                        we can add, breaking if a header we would normally
                        expect on a response is added to a request.
                     - However, http requests could contain whatever
                        the client sends. So we just try to add all the
                        headers we can.
                     */
                }
            }
        );
    }

    private IDictionary<string, string> CloneHeadersWithHostOverride(
        string url,
        IDictionary<string, string> headers
    )
    {
        var result = headers.Clone();
        OverrideHostHeaders(url, result);
        OverrideKeepAlive(result);

        OverrideAcceptedContentType(result);
        return result;
    }

    private static void OverrideAcceptedContentType(IDictionary<string, string> result)
    {
// insist on an image response
        result["Accept"] = "image/*";
    }

    private void OverrideKeepAlive(IDictionary<string, string> result)
    {
        if (!_appSettings.EnableConnectionKeepAlive)
        {
            result.Remove("Keep-Alive");
        }

// only honor keep-alive according to app settings
        result["Connection"] = _appSettings.EnableConnectionKeepAlive
            ? "keep-alive"
            : "close";
    }

    private static void OverrideHostHeaders(string url, IDictionary<string, string> result)
    {
        var host = new Uri(url).Host;
        HostHeaders.ForEach(h => result[h] = host);
    }

    private static readonly string[] HostHeaders =
    {
        "Host",
        "Referrer",
        "Origin"
    };
#endif
}