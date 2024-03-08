using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PeanutButter.DuckTyping.Exceptions;
using PeanutButter.DuckTyping.Extensions;
using PeanutButter.Utils;
using PeanutButter.Utils.Dictionaries;

namespace IRAAS
{
    public static class AppSettingsProvider
    {
        public const string BASE_CONFIG = "appsettings.json";
        public const string DEPLOY_CONFIG = "appsettings.deploy.json";

        public static IAppSettings CreateAppSettings()
        {
            return _cached ??= GetSettingsFrom(
                CreateConfig()
            );
        }

        public static void ClearCachedSettings()
        {
            _cached = null;
        }

        private static IAppSettings _cached;

        public static IConfigurationRoot CreateConfig()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(BASE_CONFIG);
            if (CanLoad(DEPLOY_CONFIG))
            {
                builder.AddJsonFile(DEPLOY_CONFIG);
            }

            builder.AddEnvironmentVariables();

            return builder.Build();
        }

        private static bool CanLoad(string deployConfig)
        {
            if (!File.Exists(deployConfig))
            {
                return false;
            }

            var lines = File.ReadAllLines(deployConfig);
            var re = new Regex("#{.+}");
            return lines.All(l => !re.Match(l).Success);
        }

        private static IAppSettings GetSettingsFrom(
            IConfigurationRoot config)
        {
            var defaultConfig = CreateDefaultConfig();
            var providedConfig = config.GetSection("Settings")
                ?.GetChildren()
                .ToDictionary(s => s.Key, s => s.Value) ?? new Dictionary<string, string>();
            DefaultSettingValueProviders.ForEach(kvp =>
                DefaultSettingIfNotSet(
                    providedConfig,
                    kvp.Key,
                    kvp.Value(providedConfig)
                )
            );
            var logLevel = FindLogLevelFor(config, "IRAAS");
            if (!string.IsNullOrWhiteSpace(logLevel))
            {
                providedConfig[nameof(IAppSettings.IRAASLogLevel)] = logLevel;
            }

            var merged = new MergeDictionary<string, string>(providedConfig, defaultConfig);
            try
            {
                var result = merged.FuzzyDuckAs<IAppSettings>(true);
                if (result.MaxUrlFetchRetries < 0)
                {
                    // alter the underlying dictionary of settings
                    // -> the IAppSettings interface is read-only
                    providedConfig[nameof(result.MaxUrlFetchRetries)] = "0";
                }

                return result;
            }
            catch (UnDuckableException ex)
            {
                Console.WriteLine(ex.Errors.JoinWith("\n"));
                throw;
            }
        }

        private static Dictionary<string, string> CreateDefaultConfig()
        {
            return typeof(IAppSettings)
                .GetProperties()
                .Select(pi => (pi.Name, pi.GetCustomAttributes(true)
                                   .OfType<DefaultSettingAttribute>()
                                   .FirstOrDefault()?.Value)
                )
                .ToDictionary(o => o.Name, o => o.Value);
        }

        private static string FindLogLevelFor(
            IConfigurationRoot config,
            string name)
        {
            return config
                .GetSection("Logging")
                ?.GetChildren()
                ?.FirstOrDefault()
                ?.GetChildren()
                ?.FirstOrDefault(c => c.Key == name)
                ?.Value ?? LogLevel.Warning.ToString();
        }

        private static readonly Dictionary<string, Func<Dictionary<string, string>, string>>
            DefaultSettingValueProviders = new Dictionary<string, Func<Dictionary<string, string>, string>>()
            {
                [SETTING_MAX_CONCURRENCY] = ResolveMaxConcurrency,
                [SETTING_MAX_IMAGE_FETCH_TIME_IN_MILLISECONDS] = ResolveMaxImageFetchTimeInMilliseconds,
                [SETTING_LOG_FOLDER] = ResolveDefaultLogFolder
            };

        private static string ResolveDefaultLogFolder(Dictionary<string, string> arg)
        {
            // default is null or empty as the log4net configuration helper will
            // resolve this to be relative to the assembly
            // - we just need to set a value so that duck-typing won't
            //    throw

            return arg.ContainsKey(SETTING_LOG_FOLDER)
                ? arg[SETTING_LOG_FOLDER]
                : "";
        }

        private static string ResolveMaxImageFetchTimeInMilliseconds(Dictionary<string, string> config)
        {
            return ResolveSetting(
                config,
                SETTING_MAX_IMAGE_FETCH_TIME_IN_MILLISECONDS,
                t => t > 0,
                DEFAULT_MAX_IMAGE_FETCH_TIME_IN_MILLISECONDS
            );
        }

        // as the app upgrades, local config may not be replaced
        // with updated configuration values, so we have to fill 
        // in new settings on-the-fly when they are created to avoid
        // getting failure from the Fuzzy-Ducked app settings. The
        // alternative is to force fuzzy ducking, which would get
        // default(T) for any unset property and that's quite likely
        // _not_ what we want
        private static void DefaultSettingIfNotSet(
            Dictionary<string, string> providedConfig,
            string key,
            string defaultValue)
        {
            var matchingKey = providedConfig.Keys.FirstOrDefault(
                k => k.Equals(key, StringComparison.OrdinalIgnoreCase)
            ) ?? key;
            providedConfig[matchingKey] = defaultValue;
        }

        private static string ResolveMaxConcurrency(
            Dictionary<string, string> config)
        {
            return ResolveSetting(
                config,
                SETTING_MAX_CONCURRENCY,
                i => i > 0,
                DEFAULT_MAX_CONCURRENCY
            );
        }

        private static string ResolveSetting<T>(
            Dictionary<string, string> config,
            string key,
            Func<T, bool> validator,
            T defaultValue)
        {
            var configKey = config.Keys.FirstOrDefault(
                k => k.Equals(key, StringComparison.OrdinalIgnoreCase)
            );
            var stringValue = configKey == null
                ? defaultValue.ToString()
                : config[configKey];

            if (TryChangeType<T>(stringValue, out var value) &&
                validator(value))
            {
                return value.ToString();
            }

            return defaultValue.ToString();
        }

        private static bool TryChangeType<T>(string value, out T result)
        {
            result = default(T);
            try
            {
                result = (T) Convert.ChangeType(value, typeof(T));
                return true;
            }
            catch
            {
                return false;
            }
        }

        private const string SETTING_MAX_CONCURRENCY = "MaxConcurrency";
        private const string SETTING_MAX_IMAGE_FETCH_TIME_IN_MILLISECONDS = "MaxImageFetchTimeInMilliseconds";
        private const string SETTING_LOG_FOLDER = "LogFolder";

        private const int DEFAULT_MAX_IMAGE_FETCH_TIME_IN_MILLISECONDS = 1000;
        private static readonly int DEFAULT_MAX_CONCURRENCY = Environment.ProcessorCount;
    }
}