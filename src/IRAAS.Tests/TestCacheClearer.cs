using System;
using System.Reflection;
using IRAAS.ImageProcessing;

namespace IRAAS.Tests;

internal static class TestCacheClearer
{
    private static readonly BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

    public static void ClearAllStaticFieldCaches()
    {
        ClearDefaultImageParameters();
        ClearCachedAppConfig();
        ClearStartupCache();
    }

    public static void ClearDefaultImageParameters()
    {
        NullifyPrivateStaticField(
            typeof(ImageResizeParameters),
            "_defaultParameters"
        );
    }

    public static void ClearCachedAppConfig()
    {
        NullifyPrivateStaticField(
            typeof(AppSettingsProvider),
            "_cachedConfig"
        );
    }

    public static void ClearStartupCache()
    {
        NullifyPrivateStaticField(
            typeof(Startup),
            "_appSettings"
        );
        NullifyPrivateStaticField(
            typeof(Startup),
            "_appConfig"
        );
    }

    private static void NullifyPrivateStaticField(
        Type onType,
        string fieldName
    )
    {
        var field = onType.GetField(fieldName, PrivateStatic);
        if (field is null)
        {
            throw new InvalidOperationException($"field '{fieldName}' not found on type {onType}");
        }

        field.SetValue(null, null);
    }
}