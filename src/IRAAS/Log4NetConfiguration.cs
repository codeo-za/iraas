using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using log4net;
using log4net.Core;
using Microsoft.Extensions.Logging;

namespace IRAAS
{
    public class InvalidLog4NetConfigurationException : InvalidOperationException
    {
        public InvalidLog4NetConfigurationException(
            string message) : base(message)
        {
        }
    }

    public static class Log4NetConfiguration
    {
        /// <summary>
        /// Attempts to configure log4net from a log4net.config
        /// found alongside the IRAAS assembly
        /// </summary>
        public static void Configure(IAppSettings appSettings)
        {
            ConfigureFromXmlFile(
                appSettings,
                Path.Join(
                    MyDir,
                    "log4net.config"
                )
            );
        }

        /// <summary>
        /// Attempts to configure log4net with the provided xml config file
        /// </summary>
        /// <param name="appSettings">Current app settings, used to generate:
        /// - log file name
        /// - log level (if not explicitly set by Octopus vai #{Log4Net.LogLevel})</param>
        /// <param name="path"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public static void ConfigureFromXmlFile(
            IAppSettings appSettings,
            string path)
        {
            if (!File.Exists(path))
            {
                throw new InvalidOperationException(
                    $"Unable to configure log4net: can't find config at {path}"
                );
            }

            var doc = LoadConfig(appSettings, path);
            var repo = LogManager.CreateRepository(
                MainAssembly,
                typeof(log4net.Repository.Hierarchy.Hierarchy)
            );
            log4net.Config.XmlConfigurator.Configure(
                repo,
                doc["log4net"]
            );
        }

        // log4net can be configured with XmlDocument, not XDocument
        private static XmlDocument LoadConfig(
            IAppSettings appSettings,
            string configFile)
        {
            var doc = LoadXml(configFile);
            SetDefaultLogLevelIfRequired(appSettings, doc);
            SetLogOutput(appSettings, doc);
            return doc.ToXmlDocument();
        }

        private static void SetLogOutput(IAppSettings appSettings, XDocument doc)
        {
            if (!CheckPathExistsForElement(doc, APPENDER_FILE_XPATH))
            {
                return;
            }
            ManipulateElement(doc, APPENDER_FILE_XPATH, el =>
            {
                var logDir = string.IsNullOrWhiteSpace(appSettings.LogFolder)
                    ? Path.Combine(MyDir, "logs")
                    : appSettings.LogFolder;
                try
                {
                    EnsureFolderExists(logDir);
                }
                catch (Exception ex)
                {
                    var extra = appSettings.LogFolder is null
                        ? "Try setting the LogFolder setting in appsettings.json"
                        : "Check the setting LogFolder in appsettings.json";
                    throw
                        new InvalidLog4NetConfigurationException(
                            $"Unable to find or create the logs folder at {logDir}:\n{ex.Message}\n{extra}"
                        );
                }

                var configured = el.Attribute(VALUE_ATTRIBUTE)?.Value ?? "iraas-.log";
                el.SetAttributeValue(VALUE_ATTRIBUTE, Path.Combine(logDir, configured));
            });
        }

        private static void EnsureFolderExists(string logDir)
        {
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }
        }

        private const string ROOT_LEVEL_XPATH = "/log4net/root/level";

        private const string APPENDER_FILE_XPATH =
            "/log4net/appender[@name='RollingLogFileAppender']/file";

        private const string VALUE_ATTRIBUTE = "value";

        private static void SetDefaultLogLevelIfRequired(
            IAppSettings appSettings,
            XDocument doc)
        {
            ManipulateElement(doc, ROOT_LEVEL_XPATH, levelEl =>
            {
                var level = levelEl.Attribute(VALUE_ATTRIBUTE);
                if (level is null || IsOctopusVariable(level.Value))
                {
                    levelEl.SetAttributeValue(
                        VALUE_ATTRIBUTE,
                        MapToLog4NetLevel(
                            appSettings.IRAASLogLevel
                        )
                    );
                }
            });
        }

        private static readonly Dictionary<LogLevel, Level> AspNetToLog4NetLevels
            = new()
            {
                [LogLevel.Trace] = Level.Trace,
                [LogLevel.Debug] = Level.Debug,
                [LogLevel.Information] = Level.Info,
                [LogLevel.Warning] = Level.Warn,
                [LogLevel.Error] = Level.Error,
                [LogLevel.Critical] = Level.Critical,
                [LogLevel.None] = Level.Off,
            };

        private static string MapToLog4NetLevel(LogLevel configuredLogLevel)
        {
            return AspNetToLog4NetLevels.TryGetValue(configuredLogLevel, out var result)
                ? result.Name
                : throw new InvalidLog4NetConfigurationException(
                    $"Unable to map LogLevel {configuredLogLevel} to log4net Level"
                );
        }

        private static void ManipulateElement(
            XDocument doc,
            string xpath,
            Action<XElement> manipulator)
        {
            var el = doc.XPathSelectElement(xpath);
            if (el is null)
            {
                throw new InvalidLog4NetConfigurationException(
                    $"Unable to find required node at xpath '{xpath}'"
                );
            }

            manipulator(el);
        }

        private static bool CheckPathExistsForElement(
            XDocument doc,
            string xpath)
        {
            var el = doc.XPathSelectElement(xpath);
            return el is not null;
        }

        private static bool IsOctopusVariable(string levelValue)
        {
            return levelValue.StartsWith("#{") &&
                   levelValue.EndsWith("}");
        }

        // XDocuments are more convenient to work with
        private static XDocument LoadXml(string configFile)
        {
            try
            {
                return XDocument.Load(configFile);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Unable to parse {configFile} as xml: {ex.Message}"
                );
            }
        }

        private static Assembly MainAssembly =>
            _mainAssembly ??= typeof(Program).Assembly;

        private static Assembly _mainAssembly;

        private static string MyDir =>
            _myDir ??= FindContainingFolder();

        private static string _myDir;

        private static string FindContainingFolder()
        {
            var myDir = Path.GetDirectoryName(
                new Uri(
                    MainAssembly.Location
                ).LocalPath
            );
            return myDir ?? throw new InvalidOperationException(
                $"Unable to determine assembly location for {MainAssembly}"
            );
        }
    }

    public static class XDocumentExtensions
    {
        // inspired by https://stackoverflow.com/questions/1508572/converting-xdocument-to-xmldocument-and-vice-versa#1509094
        public static XmlDocument ToXmlDocument(
            this XDocument doc)
        {
            var result = new XmlDocument();
            using var reader = doc.CreateReader();
            result.Load(reader);
            return result;
        }
    }
}