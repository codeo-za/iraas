using System;
using System.Collections;
using DryIoc;
using DryIoc.Microsoft.DependencyInjection;
using IRAAS.Logging;
using IRAAS.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IRAAS;

public class Startup
{
    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(
        IApplicationBuilder app,
        IWebHostEnvironment env,
        ILoggerFactory loggerFactory
    )
    {
        var config = AppSettingsProvider.CreateAppSettings();
        SetupLog4netWith(loggerFactory, config);
        app.Use(
            (context, next) =>
            {
                context.Request.EnableBuffering();
                return next();
            }
        );
        if (env.IsDevelopment() ||
            config.UseDeveloperExceptionPage)
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseMiddleware<ProductionFallbackExceptionHandlerMiddleware>();
        }

        if (config.UseHttps)
        {
            app.UseHttpsRedirection();
            app.UseHsts();
        }

        app.UseMiddleware<MaxClientsMiddleware>();
        app.UseMiddleware<ConcurrencyMiddleware>();
        app.UseMiddleware<InvalidProcessingOptionsExceptionMiddleware>();
        app.UseMiddleware<NotImplementedExceptionMiddleware>();
        app.UseMiddleware<ImageSourceNotAllowedExceptionMiddleware>();
        app.UseMiddleware<ImageProviderErrorMiddleware>();
        app.UseMiddleware<RedirectTimedOutRequestsMiddleware>();
        app.UseMiddleware<NotModifiedExceptionMiddleware>();

        app.UseRouting();
        app.UseEndpoints(e => e.MapControllers());
        DumpEnvironmentVariables();
    }

    private void SetupLog4netWith(
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
        services.AddMvc();

        var container = new Container(
            Rules.Default
                .With(FactoryMethod.ConstructorWithResolvableArguments)
                .WithTrackingDisposableTransients()
        );
        container.Register<IServiceScopeFactory, DryIocServiceScopeFactory>(Reuse.Singleton);

        return container.WithDependencyInjectionAdapter(services)
            .ConfigureServiceProvider<CompositionRoot>();
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