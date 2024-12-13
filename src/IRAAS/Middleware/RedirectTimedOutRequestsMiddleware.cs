using System;
using System.Collections.Generic;
using System.Net;
using IRAAS.ImageProcessing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace IRAAS.Middleware;

/// <summary>
/// If a request for a resource takes too long, it's holding up
/// other requests (due to MaxConcurrency). So if we can't get
/// that resource quickly enough, simply tell the caller to go
/// get it themselves.
/// </summary>
public class RedirectTimedOutRequestsMiddleware
    : ExceptionHandlerMiddleware<RequestTimedOutException>
{
    public RedirectTimedOutRequestsMiddleware(
        ILogger<RedirectTimedOutRequestsMiddleware> logger,
        IAppSettings appSettings)
        : base(301, GenerateResponseGenerator(logger), appSettings)
    {
    }
        
    private const string UPSTREAM_HEADER = "X-Upstream";

    private static Func<RequestTimedOutException, HttpContext, string>
        GenerateResponseGenerator(ILogger<RedirectTimedOutRequestsMiddleware> logger)
    {
        return (e, c) =>
        {
            var lines = new List<string>()
            {
                $"Request timed out: {e.Url}"
            };
            var upstreamFound = false;
            // chances are good we get no response, but on the off-chance
            // we do, log info about it
            if (e.Headers.ContainsKey(UPSTREAM_HEADER))
            {
                // try get the X-Upstream header to identify which server
                // timed out
                var header = e.Headers["X-Upstream"];
                if (header is string)
                {
                    lines.Add($"Upstream: {header}");
                    upstreamFound = true;
                }
            }

            if (!upstreamFound)
            {
                lines.Add("(Upstream unknown)");
            }

            logger.LogWarning(
                string.Join(
                    "\n",
                    lines
                )
            );

            c.Response.Headers["Location"] = e.Url;
            return "Moved";
        };
    }
}