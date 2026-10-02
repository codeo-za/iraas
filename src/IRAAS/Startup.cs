using System;
using System.Collections;
using System.Text.Json.Serialization;
using DryIoc;
using DryIoc.Microsoft.DependencyInjection;
using IRAAS.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PeanutButter.Utils;

namespace IRAAS;

public class Startup
{
    private static IAppSettings AppSettings =>
#pragma warning disable CS0618 // Type or member is obsolete
        _appSettings ??= AppSettingsProvider.CreateAppSettings();
#pragma warning restore CS0618 // Type or member is obsolete

    [Obsolete("Use the property, Luke")]
    private static IAppSettings _appSettings;

    private static IConfigurationRoot AppConfig =>
#pragma warning disable CS0618 // Type or member is obsolete
        _appConfig ??= AppSettingsProvider.CreateConfig();
#pragma warning restore CS0618 // Type or member is obsolete
    [Obsolete("Use the property, Luke")]
    private static IConfigurationRoot _appConfig;

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(
        IApplicationBuilder app,
        IWebHostEnvironment env,
        ILoggerFactory loggerFactory
    )
    {
        SetupLog4NetWith(loggerFactory, AppSettings);
        app.Use(
            (context, next) =>
            {
                context.Request.EnableBuffering();
                return next();
            }
        );
        if (env.IsDevelopment() ||
            AppSettings.UseDeveloperExceptionPage)
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseMiddleware<ProductionFallbackExceptionHandlerMiddleware>();
        }

        if (AppSettings.UseHttps)
        {
            app.UseHttpsRedirection();
            app.UseHsts();
        }

        app.UseMiddleware<MaxClientsMiddleware>();
        app.UseMiddleware<BadHttpRequestExceptionMiddleware>();
        app.UseMiddleware<ConcurrencyMiddleware>();
        app.UseMiddleware<InvalidProcessingOptionsExceptionMiddleware>();
        app.UseMiddleware<NotImplementedExceptionMiddleware>();
        app.UseMiddleware<ImageSourceNotAllowedExceptionMiddleware>();
        app.UseMiddleware<ImageProviderErrorMiddleware>();
        app.UseMiddleware<RedirectTimedOutRequestsMiddleware>();
        app.UseMiddleware<NotModifiedExceptionMiddleware>();
        app.UseMiddleware<ArgumentExceptionMiddleware>();
        app.UseMiddleware<ArgumentNullExceptionMiddleware>();
        app.UseMiddleware<AuthorizationMiddleware>();

        app.UseRouting();
        app.UseEndpoints(e => e.MapControllers());
        if (Environment.GetEnvironmentVariable("DUMP_ENVIRONMENT").AsBoolean())
        {
            DumpEnvironmentVariables();
        }
    }

    private void SetupLog4NetWith(
        ILoggerFactory loggerFactory,
        IAppSettings config
    )
    {
        Log4NetConfiguration.Configure(config);
        var options = new Log4NetProviderOptions()
        {
            ExternalConfigurationSetup = true
        };
        loggerFactory.AddLog4Net(options);
    }

    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
    public IServiceProvider ConfigureServices(
        IServiceCollection services
    )
    {
        services.AddMvc()
            .AddJsonOptions(
                opts =>
                {
                    opts.JsonSerializerOptions.Converters.Add(
                        new JsonStringEnumConverter()
                    );
                }
            );
        services.Configure<KestrelServerOptions>(
            opts => { opts.Limits.MaxRequestBodySize = ResolveMaxRequestBodySize(); }
        );
        services.AddSingleton(AppSettings);
        
        var container = new Container(
            Rules.Default
                .With(FactoryMethod.ConstructorWithResolvableArguments)
                .WithTrackingDisposableTransients()
        );
        container.Register<IServiceScopeFactory, DryIocServiceScopeFactory>(Reuse.Singleton);

        return container.WithDependencyInjectionAdapter(services)
            .ConfigureServiceProvider<CompositionRoot>();
    }

    private static long ResolveMaxRequestBodySize()
    {
        var configured = AppConfig["Kestrel:Limits:MaxRequestBodySize"];
        if (long.TryParse(configured, out var explicitLimit))
        {
            return explicitLimit;
        }

        // POST requests will expect the image data to come through
        // base64-encoded, so we have to allow at least 33% headroom
        // -> allowing 50% to be sure. So some over-sized requests will
        //    make it into application logic where they will be rejected,
        //    but any massively-oversized request will be blocked at the
        //    kestrel layer
        return (long)Math.Round(AppSettings.MaxInputImageSize * 1.5);
    }

    private void DumpEnvironmentVariables()
    {
        Dump(
            "environment",
            () =>
            {
                var envVars = Environment.GetEnvironmentVariables();
                foreach (DictionaryEntry entry in envVars)
                {
                    Console.WriteLine($"{entry.Key}={entry.Value}");
                }
            }
        );
    }

    private void Dump(string label, Action action)
    {
        Console.WriteLine($"-- {label} --");
        action();
        Console.WriteLine("----");
    }
}