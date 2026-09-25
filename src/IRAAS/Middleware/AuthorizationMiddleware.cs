using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IRAAS.Exceptions;
using IRAAS.Security;
using Microsoft.AspNetCore.Http;
using PeanutButter.Utils;

namespace IRAAS.Middleware;

public class AuthorizationMiddleware : IMiddleware
{
    private readonly IAppSettings _appSettings;
    private readonly IWhitelist _whitelist;
    private readonly HashSet<string> _knownTokens;
    private readonly bool _openPostAllowed;

    public AuthorizationMiddleware(
        IAppSettings appSettings,
        IWhitelist whitelist
    )
    {
        _appSettings = appSettings;
        _whitelist = whitelist;
        _knownTokens = Split(appSettings.PostAuthTokens ?? "", ",")
            .AsHashSet();
        _openPostAllowed = (appSettings.PostAuthTokens ?? "").Trim() == "*";
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

        VerifyHaveValidUrlParameter(context.Request);
    }

    private void VerifyHaveValidUrlParameter(
        HttpRequest req
    )
    {
        if (
            !req.Query.TryGetValue("url", out var requestedImageUrl)
            || requestedImageUrl.IsEmpty()
        )
        {
            throw new NotImplementedException();
        }

        if (IsInvalidUrl(requestedImageUrl))
        {
            throw new InvalidProcessingOptionsException(
                $"Invalid url provided: '{requestedImageUrl}'"
            );
        }

        if (!_whitelist.IsAllowed(requestedImageUrl))
        {
            throw new ImageSourceNotAllowedException(requestedImageUrl);
        }
    }

    private static bool IsInvalidUrl(
        string url
    )
    {
        return !Uri.TryCreate(
            url,
            UriKind.Absolute,
            out _
        );
    }

    private bool IsAllowedTestPageRequest(
        HttpRequest req
    )
    {
        var isSizePath = Routes.HasSizeEndpointPath(req);
        var isTestPath = Routes.HasTestPagePath(req);

        if (!isSizePath && !isTestPath)
        {
            return false;
        }

        if (!_appSettings.EnableTestPage)
        {
            throw new NotImplementedException();
        }

        if (isSizePath)
        {
            VerifyHaveValidUrlParameter(req);
        }

        return true;
    }

    private void VerifyPostRequestAllowed(
        HttpContext context
    )
    {
        if (!_appSettings.AllowPostRequests)
        {
            throw new NotImplementedException();
        }

        if (_openPostAllowed)
        {
            return;
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