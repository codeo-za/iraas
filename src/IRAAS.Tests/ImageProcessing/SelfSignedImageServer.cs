using System;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PeanutButter.Utils;

namespace IRAAS.Tests.ImageProcessing;

public sealed class SelfSignedImageServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly X509Certificate2 _certificate;

    public string BaseUrl { get; }

    private SelfSignedImageServer(
        WebApplication app,
        X509Certificate2 certificate,
        string baseUrl
    )
    {
        _app = app;
        _certificate = certificate;
        BaseUrl = baseUrl;
    }

    public static async Task<SelfSignedImageServer> Start(
        byte[] payload,
        string contentType = "image/png"
    )
    {
        var certificate = SelfSignedCertificate.CreateForLoopback();

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        var port = PortFinder.FindOpenPort();
        builder.WebHost.ConfigureKestrel(
            options =>
            {
                options.Listen(
                    IPAddress.Loopback,
                    port,
                    (ListenOptions listenOptions) => listenOptions.UseHttps(certificate)
                );
            }
        );

        var app = builder.Build();
        app.MapGet("/image", () => Results.Bytes(payload, contentType));

        await app.StartAsync();

        var address = app.Services
            .GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses
            .First();

        return new SelfSignedImageServer(app, certificate, address);
    }

    public string ImageUrl => $"{BaseUrl}/image";

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
        _certificate.Dispose();
    }
}