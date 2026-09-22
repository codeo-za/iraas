using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IRAAS.Security;
using Microsoft.AspNetCore.Http;
using PeanutButter.Utils;

namespace IRAAS.Middleware;

public class AuthorizationMiddleware : IMiddleware
{
    private readonly IAppSettings _appSettings;
    private readonly IWhitelist _whitelist;
    private readonly HashSet<string> _knownTokens;

    public AuthorizationMiddleware(
        IAppSettings appSettings,
        IWhitelist whitelist
    )
    {
        _appSettings = appSettings;
        _whitelist = whitelist;
        _knownTokens = Split(appSettings.PostAuthTokens ?? "", ",")
            .AsHashSet();
    }

    public Task InvokeAsync(
        HttpContext context,
        RequestDelegate next
    )
    {
        if (context.Request.Method == HttpMethods.Get)
        {
            VerifyGetRequestAllowed(context);
            return next(context);
        }

        if (context.Request.Method != HttpMethods.Post)
        {
            throw new NotImplementedException();
        }

        VerifyPostRequestAllowed(context);

        // fixme: should verify:
        // 1. can post
        // 2. post allowed by token
        return next(context);
    }

    private void VerifyGetRequestAllowed(
        HttpContext context
    )
    {
        if (IsAllowedTestPageRequest(context.Request))
        {
            return;
        }

        if (
            !context.Request.Query.TryGetValue("url", out var requestedImageUrl)
            || requestedImageUrl.IsEmpty()
        )
        {
            throw new NotImplementedException();
        }

        if (!_whitelist.IsAllowed(requestedImageUrl))
        {
            throw new NotImplementedException();
        }
    }

    private bool IsAllowedTestPageRequest(
        HttpRequest contextRequest
    )
    {
        if (!_appSettings.EnableTestPage)
        {
            return false;
        }

        if (IsTestPath(contextRequest.Path))
        {
            return true;
        }

        return false;
    }

    private bool IsTestPath(string path)
    {
        return "/test".Equals(path, StringComparison.OrdinalIgnoreCase) ||
               "/size".Equals(path, StringComparison.OrdinalIgnoreCase);
    }

    private void VerifyPostRequestAllowed(
        HttpContext context
    )
    {
        if (!_appSettings.AllowPostRequests)
        {
            throw new NotImplementedException();
        }

        var authHeader = context.Request.Headers.Authorization.FirstOrDefault()
                         ?? throw new NotImplementedException();
        var parts = Split(authHeader, " ");
        var authScheme = parts.FirstOrDefault() ?? throw new NotImplementedException();
        var token = parts.Skip(1).FirstOrDefault() ?? throw new NotImplementedException();
        if (!authScheme.Equals("bearer", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotImplementedException();
        }

        if (!_knownTokens.Contains(token))
        {
            throw new NotImplementedException();
        }
    }

    private string[] Split(string str, string delimiter)
    {
        return (str ?? "")
            .Split(
                delimiter,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            );
    }
}