using System.Collections.Generic;
using System.Linq;
using System.Net;
using IRAAS.ImageProcessing;
using Microsoft.AspNetCore.Http;

namespace IRAAS.Middleware
{
    public class ImageProviderErrorMiddleware
        : ExceptionHandlerMiddleware<ImageProviderErrorException>
    {
        public ImageProviderErrorMiddleware(IAppSettings appSettings)
            : base(e => e.StatusCode, GenerateMessage, appSettings)
        {
        }

        private static string GenerateMessage(
            ImageProviderErrorException ex,
            HttpContext context)
        {
            try
            {
                return string.Join(
                    "\n",
                    "Unable to retrieve image at:",
                    ex.RequestUri,
                    "request headers:",
                    DumpHeaders(ex.RequestHeaders),
                    $"response status: {(int) ex.StatusCode}",
                    "response headers:",
                    DumpHeaders(ex.ResponseHeaders)
                );
            }
            catch
            {
                // should never fail
                return "Unable to retrieve image";
            }
        }

        private static string DumpHeaders(IDictionary<string, string> headers)
        {
            return string.Join(
                "\n",
                headers.Select(kvp => $"{kvp.Key}: {kvp.Value}")
            );
        }

        private static object DumpHeaders(HttpWebResponse src)
        {
            return string.Join(
                "\n",
                src
                    ?.Headers
                    .AllKeys
                    .Select(k => $"{k}: {src.Headers[k]}") ?? new string[0]
            );
        }

        private static string DumpHeaders(WebRequest src)
        {
            return string.Join(
                "\n",
                src
                    .Headers
                    .AllKeys
                    .Select(k => $"{k}: {src.Headers[k]}")
            );
        }
    }
}