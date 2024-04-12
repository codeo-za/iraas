using System.Linq;
using DryIoc;
using IRAAS.ImageProcessing;
using IRAAS.Logging;
using IRAAS.Middleware;
using IRAAS.Security;
using Microsoft.AspNetCore.Http;
using PeanutButter.Utils;

namespace IRAAS
{
    public class Bootstrapper
    {
        public void Bootstrap(IContainer container)
        {
            container
                .RegisterSingleton<IImageResizer, ImageResizer>()
                .RegisterSingleton<IImageMimeTypeProvider, ImageMimeTypeProvider>()
                .RegisterSingleton<IUrlFetcher, UrlFetcher>()
                .RegisterSingleton<IWhitelist, Whitelist>()
                .RegisterSingleton<ILogMessageGenerator, LogMessageGenerator>();
            // ReSharper disable once RedundantTypeArgumentsOfMethod
            container.RegisterInstance<IAppSettings>(AppSettingsProvider.CreateAppSettings());

            container.RegisterAllMiddlewareSingleton();
            container.Register<IHttpContextAccessor, HttpContextAccessor>();
        }
    }

    internal static class ContainerExtensions
    {
        internal static IContainer RegisterAllMiddlewareSingleton(
            this IContainer container)
        {
            typeof(Bootstrapper).Assembly.GetTypes()
                .Where(t => t.ImplementsServiceType<IMiddleware>())
                .ForEach(t => container.Register(t, Reuse.Singleton));
            return container;
        }

        internal static IContainer RegisterSingleton<TService, TImplementation>(
            this IContainer container
        ) where TImplementation : TService
        {
            container.Register<TService, TImplementation>(Reuse.Singleton);
            return container;
        }
    }
}