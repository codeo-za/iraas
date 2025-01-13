using System;

namespace IRAAS.ImageProcessing;

public static class UriExtensions
{
    public static bool HasPath(this Uri uri)
    {
        return !string.IsNullOrWhiteSpace(uri.AbsolutePath) &&
            uri.AbsolutePath != "/";
    }

    public static bool HasParameters(this Uri uri)
    {
        return !string.IsNullOrWhiteSpace(uri.Query);
    }
}