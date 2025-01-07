using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IRAAS.Tests.ImageProcessing;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Repository.Hierarchy;
using log4net.Util;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using NSubstitute;
using PeanutButter.Utils;

namespace IRAAS.Tests;

[TestFixture]
[Parallelizable(ParallelScope.None)]
public class TestLog4NetConfiguration: TestBase
{
    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public class WhenLogLevelIsOctopusVariable: TestBase
    {
        public static IEnumerable<(LogLevel aspNetLevel, Level log4NetLevel)> TestCases()
        {
            yield return (LogLevel.Trace, Level.Trace);
            yield return (LogLevel.Debug, Level.Debug);
            yield return (LogLevel.Information, Level.Info);
            yield return (LogLevel.Warning, Level.Warn);
            yield return (LogLevel.Error, Level.Error);
            yield return (LogLevel.Critical, Level.Critical);
            yield return (LogLevel.None, Level.Off);
        }

        [Test]
        public void LogLevelTranslationTestCasesShouldEncompassAllEnumValues()
        {
            // provides an early warning if `LogLevel` is expanded and not catered for
            // Arrange
            var allValues = Enum.GetValues(typeof(LogLevel))
                .AsEnumerable<LogLevel>()
                .ToArray();
            var allCases = TestCases()
                .Select(o => o.aspNetLevel)
                .ToArray();
            Expect(allValues)
                .Not.To.Be.Empty();
            Expect(allCases)
                .Not.To.Be.Empty();
            // Act

            // Assert
            Expect(allValues)
                .To.Be.Equivalent.To(allCases);
        }

        [TestCaseSource(nameof(TestCases))]
        public void ShouldConfigureLogLevelFromAppSettings(
            (LogLevel, Level) testCase
        )
        {
            // Arrange
            var (configured, expected) = testCase;
            var appSettings = GetRandom<AppSettings>();
            appSettings.IRAASLogLevel = configured;
            using var config = new AutoTempFile
            {
                StringData = log4netConfig.Trim()
            };
            // Act
            Log4NetConfiguration.ConfigureFromXmlFile(
                appSettings,
                config.Path
            );

            // Assert
            var repository = FindMainLog4NetRepository();

            Expect(repository.Configured)
                .To.Be.True("Should be properly configured");
            var configLogs = repository
                .ConfigurationMessages
                .AsEnumerable<LogLog>()
                .ToArray();
            Expect(configLogs)
                .To.Be.Empty("Should have no configuration errors");
            var root = repository.Root;
            Expect(root.Level)
                .To.Equal(expected);
        }
    }

    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public class WhenLogLevelHasBeenSet: TestBase
    {
        [Test]
        public void ShouldConfigureThatLevel()
        {
            // Arrange
            var level = GetRandomFrom(
                new[]
                {
                    Level.Info,
                    Level.Warn,
                    Level.Fatal
                }
            );
            var xml = log4netConfig.Replace(
                "#{Log4Net.LogLevel}",
                level.Name
            );
            using var config = new AutoTempFile()
            {
                StringData = xml.Trim()
            };
            var appSettings = GetRandom<IAppSettings>();

            // Act
            Log4NetConfiguration.ConfigureFromXmlFile(
                appSettings,
                config.Path
            );
            // Assert
            var repository = FindMainLog4NetRepository();
            Expect(repository.Configured)
                .To.Be.True("Should be properly configured");
            var configLogs = repository
                .ConfigurationMessages
                .AsEnumerable<LogLog>()
                .ToArray();
            Expect(configLogs)
                .To.Be.Empty("Should have no configuration errors");
            var root = repository.Root;
            Expect(root.Level)
                .To.Equal(level);
        }

        [TestFixture]
        [Parallelizable(ParallelScope.None)]
        public class WhenLogDirNotSpecified: TestBase
        {
            [Test]
            public void ShouldConfigureLogFileToBeInLogsFolderOffAssemblyDir()
            {
                // Arrange
                using var config = new AutoTempFile()
                {
                    StringData = log4netConfig.Trim()
                };
                var appSettings = Substitute.For<IAppSettings>();
                Expect(appSettings.LogFolder)
                    .To.Be.Null.Or.Empty();
                var expected = Path.Combine(
                    // ReSharper disable once AssignNullToNotNullAttribute
                    Path.GetDirectoryName(
                        new Uri(
                            typeof(Program)
                                .Assembly
                                .Location
                        ).LocalPath
                    ),
                    "logs"
                );
                // Act
                Log4NetConfiguration.ConfigureFromXmlFile(
                    appSettings,
                    config.Path
                );
                // Assert
                var repo = FindMainLog4NetRepository();
                var appender = repo.GetAppenders()
                    .FirstOrDefault(a => a.Name == "RollingLogFileAppender") as RollingFileAppender;
                Expect(appender)
                    .Not.To.Be.Null("No rolling file appender found!");
                Expect(Path.GetDirectoryName(appender.File))
                    .To.Equal(expected);
            }
        }

        [TestFixture]
        [Parallelizable(ParallelScope.None)]
        public class WhenLogDirSpecified: TestBase
        {
            [Test]
            public void ShouldUseThatDir()
            {
                // Arrange
                using var config = new AutoTempFile(log4netConfig.Trim());
                var appSettings = Substitute.For<IAppSettings>();
                // log4net is well clever: if the path looks
                // relative, it will be made relative to the process' cwd
                // whereas an absolute path is treated as such. For this test,
                // using an absolute path provides predictable results
                using var tempDir = new AutoTempFolder();
                var expected = Path.Combine(
                    tempDir.Path,
                    GetRandomString(),
                    GetRandomString()
                );
                appSettings.LogFolder.Returns(expected);
                // Act
                Log4NetConfiguration.ConfigureFromXmlFile(
                    appSettings,
                    config.Path
                );
                // Assert
                var repo = FindMainLog4NetRepository();
                var appender = repo.GetAppenders()
                    .FirstOrDefault(a => a.Name == "RollingLogFileAppender") as RollingFileAppender;
                Expect(appender)
                    .Not.To.Be.Null("No rolling file appender found!");
                Expect(Path.GetDirectoryName(appender.File))
                    .To.Equal(expected);
            }
        }
    }

    private static Hierarchy FindMainLog4NetRepository()
    {
        if (!(LogManager.GetRepository(
                typeof(Program).Assembly
            ) is Hierarchy repository))
        {
            throw new InvalidOperationException(
                "Root log repository cannot be used as an Hierarchy"
            );
        }

        return repository;
    }


    private const string log4netConfig = @"
<?xml version=""1.0"" encoding=""utf-8""?>

        <log4net>
        <appender name=""RollingLogFileAppender"" type=""log4net.Appender.RollingFileAppender"">
        <lockingModel type=""log4net.Appender.FileAppender+MinimalLock"" />
            <file value=""iraas-.log"" />
            <preserveLogFileNameExtension value=""true"" />
            <staticLogFileName value=""false"" />
            <appendToFile value=""true"" />
            <rollingStyle value=""Date"" />
            <datePattern value=""yyyy.MM.dd"" />
            <maxSizeRollBackups value=""7"" />
            <layout type=""log4net.Layout.PatternLayout"">
            <conversionPattern
            value=""%date thread::%thread level::%-5level logger::%logger - message::%message meta::%property{Meta} exception::%exception%newline"" />
                </layout>
        </appender>
        <root>
        <level value=""#{Log4Net.LogLevel}"" />
            <appender-ref ref=""RollingLogFileAppender"" />
            </root>
        </log4net>
";
}