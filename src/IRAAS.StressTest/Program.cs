using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using PeanutButter.EasyArgs;
using PeanutButter.EasyArgs.Attributes;

namespace IRAAS.StressTest
{
    class Program
    {
        private static readonly HttpClient HttpClient = new(new HttpClientHandler()
        {
            UseProxy = false,
        });

        static void Main(string[] args)
        {
            var opts = args.ParseTo<Options>();
            HttpClient.Timeout = TimeSpan.FromSeconds(opts.Timeout);
            HttpClient.DefaultRequestHeaders.ConnectionClose = true;
            var completed = 0;
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var rateLimiter = new SemaphoreSlim(opts.MaxConcurrency);
            var completedLock = new SemaphoreSlim(1);
            while (true)
            {
                Thread.Sleep(1);
                rateLimiter.Wait();
                Task.Run(async Task() =>
                {
                    try
                    {
                        var current = opts.StartId++;
                        Print($"start: {current}");
                        var finalImageUrl = CreateImageUrlFor(opts, current);
                        var requestUrl = CreateIraasRequestUrlFor(opts, finalImageUrl);

                        if (opts.CheckIsImage && !(await IsAnImage(finalImageUrl)))
                        {
                            return;
                        }

                        var beforeRequest = stopwatch.ElapsedMilliseconds;
                        var res = await HttpClient.GetAsync(requestUrl);
                        var data = await res.Content.ReadAsByteArrayAsync();
                        var afterRequest = stopwatch.ElapsedMilliseconds;
                        await completedLock.WaitAsync();
                        int localCompleted;
                        decimal localSeconds;
                        try
                        {
                            localSeconds = stopwatch.ElapsedMilliseconds / 1000M;
                            localCompleted = ++completed;
                        }
                        finally
                        {
                            completedLock.Release();
                        }

                        var rate = localCompleted / localSeconds;
                        Print(
                            $"complete: {current} ({data.Length} bytes in {afterRequest - beforeRequest}ms) ({rate:F1} req/s)");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"fail: {ex.Message}");
                    }
                    finally
                    {
                        rateLimiter.Release();
                    }
                });
            }
        }

        private static string CreateIraasRequestUrlFor(Options o, string finalImageUrl)
        {
            var uri = new Uri(o.IRAASUrl);
            var builder = new UriBuilder(uri);
            var finalQuery = HttpUtility.ParseQueryString(builder.Query);
            finalQuery["url"] = finalImageUrl;
            builder.Query = finalQuery.ToString() ?? "";
            var requestUrl = builder.ToString();
            return requestUrl;
        }

        private static string CreateImageUrlFor(Options o, int current)
        {
            var imageUrl = new Uri(o.ImageUrlBase);
            var imageUrlBuilder = new UriBuilder(imageUrl);
            var imageQuery = HttpUtility.ParseQueryString(imageUrlBuilder.Query);
            imageQuery["id"] = current.ToString();
            imageQuery["t"] = DateTime.Now.Ticks.ToString();
            imageUrlBuilder.Query = imageQuery.ToString() ?? "";
            var finalImageUrl = imageUrlBuilder.ToString();
            return finalImageUrl;
        }

        private static async Task<bool> IsAnImage(string url)
        {
            var request = new HttpRequestMessage()
            {
                Method = HttpMethod.Head,
                Headers =
                {
                    { "Accept", "*/*" }
                },
                RequestUri = new Uri(url)
            };
            var req = await HttpClient.SendAsync(request);
            var headers = req.Headers.ToArray();
            if (!headers.Any(h => h.Key.Equals("Content-Type")))
            {
                // assume it's valid, I guess
                return true;
            }

            var contentTypeHeader =
                headers.FirstOrDefault(h => h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase));
            var contentType = contentTypeHeader.Value.FirstOrDefault() ?? "";
            if (contentType.StartsWith("image"))
            {
                return true;
            }

            Console.WriteLine($"Ignoring non-image at {url}");
            return false;
        }

        private static readonly object _lock = new object();

        private static void Print(string str)
        {
            lock (_lock)
            {
                Console.WriteLine(str);
            }
        }
    }

    public class Options
    {
        [LongName("iraas-url")]
        [Required]
        [Description("URL where IRAAS is hosted")]
        public string IRAASUrl { get; set; }

        [Required]
        [Description("Base image url to start with, must support tacking on an id url parameter")]
        public string ImageUrlBase { get; set; }

        [Description("id to start with on your image base url")]
        [Default(1)]
        public int StartId { get; set; }

        [Description("max concurrent requests")]
        [Default(10)]
        public int MaxConcurrency { get; set; }

        [Description("max time in seconds to wait for an image request")]
        [Default(120)]
        public int Timeout { get; set; }

        [Description("check that urls are images via content-type headers before attempting to process them")]
        public bool CheckIsImage { get; set; }
    }
}