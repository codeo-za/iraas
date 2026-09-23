using NSubstitute;
using PeanutButter.Utils;

namespace IRAAS.Tests.ImageProcessing;

public static class AppSettingsExtensions
{
    public static IAppSettings WithDefaultSettings(
        this IAppSettings appSettings
    )
    {
        var _40mb = 40 * 1024 * 1024;
        return appSettings
            .WithMaxInputImageSize(_40mb)
            .WithMaxOutputImageSize(_40mb)
            // allow a long fetch time so tests don't time out
            .WithMaxImageFetchTimeInMilliseconds(10000)
            .WithMaxUrlFetchRetries(0)
            // don't block anything by default
            .WithDomainWhitelist("*")
            // enable the test page for testing
            .WithTestPageEnabled()
            // don't enable keep-alive: let test connections close
            .WithConnectionKeepAliveDisabled()
            // don't allow invalid ssl certs by default
            // -> mirrors most likely deployed config
            .WithInvalidSslCertificatesForbidden()
            .WithPostRequestsForbidden()
            .WithPostAuthTokens("");
    }

    public static IAppSettings WithPostRequestsAllowed(
        this IAppSettings appSettings
    )
    {
        return appSettings.With(
            o => o.AllowPostRequests.Returns(true)
        );
    }

    public static IAppSettings WithPostRequestsForbidden(
        this IAppSettings appSettings
    )
    {
        return appSettings.With(
            o => o.AllowPostRequests.Returns(false)
        );
    }

    public static IAppSettings WithPostAuthTokens(
        this IAppSettings appSettings,
        string tokens
    )
    {
        return appSettings.With(
            o => o.PostAuthTokens.Returns(tokens)
        );
    }

    public static IAppSettings WithInvalidSslCertificatesAllowed(
        this IAppSettings appSettings
    )
    {
        return appSettings.With(
            o => o.AllowInvalidSslCertificates.Returns(_ => true)
        );
    }

    public static IAppSettings WithInvalidSslCertificatesForbidden(
        this IAppSettings appSettings
    )
    {
        return appSettings.With(
            o => o.AllowInvalidSslCertificates.Returns(_ => false)
        );
    }

    public static IAppSettings WithConnectionKeepAliveEnabled(
        this IAppSettings appSettings
    )
    {
        return appSettings.With(
            o => o.EnableConnectionKeepAlive.Returns(_ => true)
        );
    }

    public static IAppSettings WithConnectionKeepAliveDisabled(
        this IAppSettings appSettings
    )
    {
        return appSettings.With(
            o => o.EnableConnectionKeepAlive.Returns(_ => false)
        );
    }

    public static IAppSettings WithMaxInputImageSize(
        this IAppSettings appSettings,
        int maxBytes
    )
    {
        return appSettings.With(
            o => o.MaxInputImageSize.Returns(_ => maxBytes)
        );
    }

    public static IAppSettings WithMaxOutputImageSize(
        this IAppSettings appSettings,
        int maxBytes
    )
    {
        return appSettings.With(
            o => o.MaxOutputImageSize.Returns(_ => maxBytes)
        );
    }

    public static IAppSettings WithMaxImageFetchTimeInMilliseconds(
        this IAppSettings appSettings,
        int milliseconds
    )
    {
        return appSettings.With(
            o => o.MaxImageFetchTimeInMilliseconds.Returns(_ => milliseconds)
        );
    }

    public static IAppSettings WithMaxUrlFetchRetries(
        this IAppSettings appSettings,
        int maxRetries
    )
    {
        return appSettings.With(
            o => o.MaxUrlFetchRetries.Returns(_ => maxRetries)
        );
    }

    public static IAppSettings WithDomainWhitelist(
        this IAppSettings appSettings,
        string whitelist
    )
    {
        return appSettings.With(
            o => o.DomainWhitelist.Returns(_ => whitelist)
        );
    }

    public static IAppSettings WithTestPageEnabled(
        this IAppSettings appSettings
    )
    {
        return appSettings.With(
            o => o.EnableTestPage.Returns(_ => true)
        );
    }

    public static IAppSettings WithTestPageDisabled(
        this IAppSettings appSettings
    )
    {
        return appSettings.With(
            o => o.EnableTestPage.Returns(_ => false)
        );
    }
}