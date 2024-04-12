using System;
using System.IO;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using static PeanutButter.RandomGenerators.RandomValueGen;
using NExpect;
using NExpect.Implementations;
using NExpect.Interfaces;
using NExpect.MatcherLogic;
using NSubstitute;
using PeanutButter.Utils;
using static NExpect.Expectations;

// ReSharper disable AccessToDisposedClosure

namespace IRAAS.Tests.ImageProcessing;

[TestFixture]
[Parallelizable(ParallelScope.None)]
public class TestAppSettingsProvider
{
    [SetUp]
    public void Setup()
    {
        // don't let tests interact
        AppSettingsProvider.ClearCachedSettings();
    }

    [OneTimeTearDown]
    public void Teardown()
    {
        // don't poison app settings for anyone else
        AppSettingsProvider.ClearCachedSettings();
    }

    [TestCase("appsettings.json")]
    public void ShouldLoadFromCWD_(string filename)
    {
        var expected = GetRandom<IAppSettings>();
        using var tempFolder = new AutoTempFolder();
        using var _ = new AutoResetter<string>(
            () => ChDir(tempFolder.Path),
            prior => ChDir(prior)
        );
        // Arrange
        var json = MakeSettings(expected);
        File.WriteAllText(
            filename,
            json
        );
        // Act
        var result = AppSettingsProvider.CreateAppSettings();
        // Assert
        Expect(result).To.Deep.Equal(expected);
    }

    [TestCase("appsettings.json")]
    public void ShouldDefaultMaxConcurrencyToProcessorCount_(string filename)
    {
        var expected = GetRandom<IAppSettings>();
        using var tempFolder = new AutoTempFolder();
        using var _ = new AutoResetter<string>(
            () => ChDir(tempFolder.Path),
            prior => ChDir(prior)
        );
        // Arrange
        var json = MakeSettingsWithoutConcurrency(expected);
        File.WriteAllText(
            filename,
            json
        );
        // Act
        var result = AppSettingsProvider.CreateAppSettings();
        // Assert
        Expect(result.MaxConcurrency)
            .To.Equal(Environment.ProcessorCount);
    }

    [TestCase("appsettings.json")]
    public void ShouldDefaultLogFolderToEmptyString_(string filename)
    {
        var expected = GetRandom<IAppSettings>();
        using var tempFolder = new AutoTempFolder();
        using var _ = new AutoResetter<string>(
            () => ChDir(tempFolder.Path),
            prior => ChDir(prior)
        );
        // Arrange
        var json = MakeSettingsWithoutLogFolder(expected);
        File.WriteAllText(
            filename,
            json
        );
        // Act
        var result = AppSettingsProvider.CreateAppSettings();
        // Assert
        Expect(result.LogFolder)
            .To.Equal("");
    }

    [TestCase("appsettings.json")]
    public void ShouldDefaultMaxConcurrencyToProcessorCountWhenLessThan1(string filename)
    {
        var expected = GetRandom<AppSettings>();
        if (expected.MaxConcurrency < 1)
        {
            // when MaxConcurrency is zero, the processor count is used
            // -> check here that this setting wasn't randomly set to zero
            //    as it would cause the test to flap
            expected.MaxConcurrency = GetRandomInt(1);
        }

        var badConfig = GetRandomInt(-10, 0);
        expected.MaxConcurrency = badConfig;
        using var tempFolder = new AutoTempFolder();
        using var _ = new AutoResetter<string>(
            () => ChDir(tempFolder.Path),
            prior => ChDir(prior)
        );
        // Arrange
        var json = MakeSettings(expected);
        File.WriteAllText(
            filename,
            json
        );
        // Act
        var result = AppSettingsProvider.CreateAppSettings();
        // Assert
        Expect(result.MaxConcurrency)
            .To.Equal(Environment.ProcessorCount);
    }

    [TestCase("appsettings.deploy.json")]
    public void ShouldOptionallyOverrideFromCWD_(string filename)
    {
        var defaultSettings = GetRandom<IAppSettings>();
        var expected = GetRandom<IAppSettings>();
        using var tempFolder = new AutoTempFolder();
        using var _ = new AutoResetter<string>(
            () => ChDir(tempFolder.Path),
            prior => ChDir(prior)
        );
        // Arrange
        var defaultJson = MakeSettings(defaultSettings);
        File.WriteAllText(
            Path.Combine(tempFolder.Path, "appsettings.json"),
            defaultJson
        );
        var overrideJson = MakeSettings(expected);
        File.WriteAllText(
            Path.Combine(tempFolder.Path, filename),
            overrideJson
        );
        // Act
        var result = AppSettingsProvider.CreateAppSettings();
        // Assert
        Expect(result).To.Deep.Equal(expected);
    }

    [Test]
    public void ShouldNotIncludeDeploySettingsWhenNugetVariablesNotSubstituted()
    {
        var defaultSettings = GetRandom<AppSettings>();
        // fuzzy-ducking will allow the unknown string #{UseHttps} as a falsey value
        defaultSettings.UseHttps = true;
        defaultSettings.MaxUrlFetchRetries = 0;

        using var tempFolder = new AutoTempFolder();
        using var _ = new AutoResetter<string>(
            () => ChDir(tempFolder.Path),
            prior => ChDir(prior)
        );
        // Arrange
        var defaultJson = MakeSettings(defaultSettings);
        var defaultJsonFile = Path.Combine(tempFolder.Path, "appsettings.json");
        File.WriteAllText(
            defaultJsonFile,
            defaultJson
        );
        var overrideJson = UNSUBSTITUTED_JSON;
        var overrideJsonFile = Path.Combine(tempFolder.Path, "appsettings.deploy.json");
        File.WriteAllText(
            overrideJsonFile,
            overrideJson
        );
        Expect(defaultJsonFile).To.Exist();
        Expect(overrideJsonFile).To.Exist();
        // Act
        var result = AppSettingsProvider.CreateAppSettings();
        // Assert
        Expect(result)
            .To.Deep.Equal(
                defaultSettings
            );
    }

    [TestFixture]
    public class WhenMaxUrlRetriesLessThanZero
    {
        [Test]
        public void ShouldSetToZero()
        {
            // Arrange
            var sourceSettings = GetRandom<IAppSettings>()
                .With(o => o.MaxUrlFetchRetries.Returns(-1));
            using var tempFolder = new AutoTempFolder();
            using var _ = AutoResetter.Create(
                () => ChDir(tempFolder.Path),
                prior => ChDir(prior)
            );
            var json = MakeSettings(sourceSettings);
            File.WriteAllText(
                Path.Combine(tempFolder.Path, "appsettings.json"),
                json
            );
            // Act
            var result = AppSettingsProvider.CreateAppSettings();
            // Assert
            Expect(result.MaxUrlFetchRetries)
                .To.Equal(0);
        }
    }

    private const string UNSUBSTITUTED_JSON = @"{
            ""Settings"": {
                ""UseHttps"": ""#{UseHttps}""
                }
            }";

    private static string MakeSettings(
        IAppSettings settings
    )
    {
        return $@"{{
  ""Logging"": {{
        ""LogLevel"": {{
            ""Default"": ""Warning"",
            ""IRAAS"": ""{settings.IRAASLogLevel}"",
        }}
    }},
    ""Kestrel"": {{
    ""Endpoints"": {{
        ""Http"": {{
            ""Url"": ""http://0.0.0.0:8008""
        }}
    }}
    }},
    ""Settings"": {{
    ""MaxInputImageSize"": ""{settings.MaxInputImageSize}"",
    ""MaxOutputImageSize"": ""{settings.MaxOutputImageSize}"",
    ""UseDeveloperExceptionPage"": ""{settings.UseDeveloperExceptionPage}"",
    ""UseHttps"": ""{settings.UseHttps}"",
    ""EnableTestPage"": ""{settings.EnableTestPage}"",
    ""DomainWhitelist"": ""{settings.DomainWhitelist}"",
    ""MaxConcurrency"": ""{settings.MaxConcurrency}"",
    ""MaxClients"": ""{settings.MaxClients}"",
    ""MaxImageFetchTimeInMilliseconds"": ""{settings.MaxImageFetchTimeInMilliseconds}"",
    ""ShareConcurrentRequests"": ""{settings.ShareConcurrentRequests.ToString().ToLower()}"",
    ""EnableConnectionKeepAlive"": ""{settings.EnableConnectionKeepAlive}"",
    ""LogFolder"": ""{settings.LogFolder}"",
    ""SuppressErrorDiagnostics"": ""{settings.SuppressErrorDiagnostics}"",
    }},
    ""AllowedHosts"": ""*""
}}";
    }

    private string MakeSettingsWithoutConcurrency(
        IAppSettings settings
    )
    {
        return $@"{{
  ""Logging"": {{
        ""LogLevel"": {{
            ""Default"": ""Warning"",
            ""IRAAS"": ""{settings.IRAASLogLevel}""
        }}
    }},
    ""Kestrel"": {{
    ""Endpoints"": {{
        ""Http"": {{
            ""Url"": ""http://0.0.0.0:8008""
        }}
    }}
    }},
    ""Settings"": {{
    ""MaxInputImageSize"": ""{settings.MaxInputImageSize}"",
    ""MaxOutputImageSize"": ""{settings.MaxOutputImageSize}"",
    ""UseDeveloperExceptionPage"": ""{settings.UseDeveloperExceptionPage}"",
    ""UseHttps"": ""{settings.UseHttps}"",
    ""EnableTestPage"": ""{settings.EnableTestPage}"",
    ""DomainWhitelist"": ""{settings.DomainWhitelist}"",
    ""ShareConcurrentRequests"": ""{settings.ShareConcurrentRequests}"",
    ""EnableConnectionKeepAlive"": ""{settings.EnableConnectionKeepAlive}"",
    ""LogFolder"": ""{settings.LogFolder}""
    }},
    ""AllowedHosts"": ""*""
}}";
    }

    private string MakeSettingsWithoutLogFolder(
        IAppSettings settings
    )
    {
        return $@"{{
  ""Logging"": {{
        ""LogLevel"": {{
            ""Default"": ""Warning"",
            ""IRAAS"": ""{settings.IRAASLogLevel}""
        }}
    }},
    ""Kestrel"": {{
    ""Endpoints"": {{
        ""Http"": {{
            ""Url"": ""http://0.0.0.0:8008""
        }}
    }}
    }},
    ""Settings"": {{
    ""MaxInputImageSize"": ""{settings.MaxInputImageSize}"",
    ""MaxOutputImageSize"": ""{settings.MaxOutputImageSize}"",
    ""UseDeveloperExceptionPage"": ""{settings.UseDeveloperExceptionPage}"",
    ""UseHttps"": ""{settings.UseHttps}"",
    ""EnableTestPage"": ""{settings.EnableTestPage}"",
    ""DomainWhitelist"": ""{settings.DomainWhitelist}"",
    ""ShareConcurrentRequests"": ""{settings.ShareConcurrentRequests}"",
    ""EnableConnectionKeepAlive"": ""{settings.EnableConnectionKeepAlive}""
    }},
    ""AllowedHosts"": ""*""
}}";
    }

    private static string ChDir(string target)
    {
        var current = Environment.CurrentDirectory;
        Directory.SetCurrentDirectory(target);
        return current;
    }
}

public static class PathMatchers
{
    public static void Exist(this ITo<string> to)
    {
        to.AddMatcher(
            actual =>
            {
                var passed = Directory.Exists(actual) || File.Exists(actual);
                return new MatcherResult(
                    passed,
                    () => $"Expected {actual} {passed.AsNot()}to exist"
                );
            }
        );
    }
}

public class AppSettings : IAppSettings
{
    public int MaxInputImageSize { get; set; }
    public int MaxOutputImageSize { get; set; }
    public bool UseDeveloperExceptionPage { get; set; }
    public bool UseHttps { get; set; }
    public bool EnableTestPage { get; set; }
    public string DomainWhitelist { get; set; }
    public int MaxConcurrency { get; set; }
    public int MaxImageFetchTimeInMilliseconds { get; set; }
    public int MaxClients { get; set; }
    public bool ShareConcurrentRequests { get; set; }
    public bool EnableConnectionKeepAlive { get; set; }
    public string LogFolder { get; set; }
    public bool SuppressErrorDiagnostics { get; set; }
    public LogLevel IRAASLogLevel { get; set; }
    public int MaxUrlFetchRetries { get; set; }
}