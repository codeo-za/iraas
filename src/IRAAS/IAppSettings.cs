using System;
using Microsoft.Extensions.Logging;
using static IRAAS.DefaultSettingValues;

namespace IRAAS
{
    public class DefaultSettingAttribute : Attribute
    {
        public string Value { get; }

        public DefaultSettingAttribute(string value)
        {
            Value = value;
        }
    }

    public static class DefaultSettingValues
    {
        public const string FORTY_MEGABYTES = "${40 * 1024 * 1024}";
        public const string FALSE = "false";
        public const string TRUE = "true";
        public const string HOST_MACHINE_CPU_COUNT = "0";
        public const string ONE_SECOND = "1000";
        public const string UNLIMITED_CLIENTS = "0";
        public const string LOG_LEVEL_WARNING = "Warning";
    }

    public interface IAppSettings
    {
        // read from Settings section
        [DefaultSetting(FORTY_MEGABYTES)]
        int MaxInputImageSize { get; }

        [DefaultSetting(FORTY_MEGABYTES)]
        int MaxOutputImageSize { get; }

        [DefaultSetting(FALSE)]
        bool UseDeveloperExceptionPage { get; }

        [DefaultSetting(FALSE)]
        bool UseHttps { get; }

        [DefaultSetting(FALSE)]
        bool EnableTestPage { get; }

        [DefaultSetting("*")]
        string DomainWhitelist { get; }

        [DefaultSetting(HOST_MACHINE_CPU_COUNT)]
        int MaxConcurrency { get; }

        [DefaultSetting(ONE_SECOND)]
        int MaxImageFetchTimeInMilliseconds { get; }

        [DefaultSetting(UNLIMITED_CLIENTS)]
        int MaxClients { get; }

        [DefaultSetting(TRUE)]
        bool ShareConcurrentRequests { get; }

        [DefaultSetting(FALSE)]
        bool EnableConnectionKeepAlive { get; }

        [DefaultSetting(null)]
        string LogFolder { get; }

        [DefaultSetting(FALSE)]
        bool SuppressErrorDiagnostics { get; }

        // read from Logging section - only made available here to reduce
        // logging overhead within IRAAS when request logging is not enabled
        // by log level > info
        [DefaultSetting(LOG_LEVEL_WARNING)]
        LogLevel IRAASLogLevel { get; }

        [DefaultSetting("0")]
        int MaxUrlFetchRetries { get; }
    }
}