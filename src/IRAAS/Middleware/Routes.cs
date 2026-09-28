using System;
using Microsoft.AspNetCore.Http;

namespace IRAAS.Middleware;

public static class Routes
{
    public const string SIZE = "size";
    public const string TEST = "test";
    public const string HEALTH = "health";
    private static readonly string SizePathBare = $"/{SIZE}";
    private static readonly string TestPathBare = $"/{TEST}";
    private static readonly string HealthPathBare = $"/{HEALTH}";
    private static readonly string SizePathTrailingSlash = $"{SizePathBare}/";
    private static readonly string TestPathTrailingSlash = $"{TestPathBare}/";
    private static readonly string HealthPathTrailingSlash = $"{HealthPathBare}/";

    public static bool HasHealthPagePath(
        HttpRequest request
    )
    {
        return AnyPathMatches(
            request,
            HealthPathBare,
            HealthPathTrailingSlash
        );
    }

    public static bool HasTestPagePath(
        HttpRequest request
    )
    {
        return AnyPathMatches(
            request,
            TestPathBare,
            TestPathTrailingSlash
        );
    }

    public static bool HasSizeEndpointPath(
        HttpRequest request
    )
    {
        return AnyPathMatches(
            request,
            SizePathBare,
            SizePathTrailingSlash
        );
    }

    private static bool AnyPathMatches(
        HttpRequest request,
        params string[] paths
    )
    {
        foreach (var path in paths)
        {
            if (path.Equals(request?.Path, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}