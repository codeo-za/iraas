using System;
using System.IO;
using IRAAS.ImageProcessing;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using NSubstitute;
using PeanutButter.Utils;

// ReSharper disable AccessToDisposedClosure

namespace IRAAS.Tests.ImageProcessing;

[TestFixture]
[Parallelizable(ParallelScope.None)]
public class TestAppSettingsProvider : TestBase
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
        Expect(result)
            .To.Deep.Equal(expected);
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
        Expect(result)
            .To.Deep.Equal(expected);
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
        Expect(defaultJsonFile)
            .To.Exist();
        Expect(overrideJsonFile)
            .To.Exist();
        // Act
        var result = AppSettingsProvider.CreateAppSettings();
        // Assert
        Expect(result)
            .To.Deep.Equal(
                defaultSettings
            );
    }

    [TestFixture]
    public class WhenMaxUrlRetriesLessThanZero : TestBase
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
        IAppSettings settings,
        IImageResizeParameters defaultParameters = null
    )
    {
        defaultParameters ??= GetRandom<IImageResizeParameters>();
        return
            $$"""
              {
                  "Logging": {
                      "LogLevel": {
                          "Default": "Warning",
                          "IRAAS": "{{settings.IRAASLogLevel}}"
                      }
                  },
                  "Kestrel": {
                      "Endpoints": {
                          "Http": {
                              "Url": "http://0.0.0.0:8008"
                          }
                      }
                  },
                  "Settings": {
                      "MaxInputImageSize": "{{settings.MaxInputImageSize}}",
                      "MaxOutputImageSize": "{{settings.MaxOutputImageSize}}",
                      "UseDeveloperExceptionPage": "{{settings.UseDeveloperExceptionPage}}",
                      "UseHttps": "{{settings.UseHttps}}",
                      "EnableTestPage": "{{settings.EnableTestPage}}",
                      "DomainWhitelist": "{{settings.DomainWhitelist}}",
                      "MaxConcurrency": "{{settings.MaxConcurrency}}",
                      "MaxClients": "{{settings.MaxClients}}",
                      "MaxImageFetchTimeInMilliseconds": "{{settings.MaxImageFetchTimeInMilliseconds}}",
                      "ShareConcurrentRequests": "{{settings.ShareConcurrentRequests.ToString().ToLower()}}",
                      "EnableConnectionKeepAlive": "{{settings.EnableConnectionKeepAlive}}",
                      "LogFolder": "{{settings.LogFolder}}",
                      "SuppressErrorDiagnostics": "{{settings.SuppressErrorDiagnostics}}",
                      "Verbose": "{{settings.Verbose}}"
                  },
                  "DefaultParameters": {
                    "*": {
                        "ReplaceTransparencyWith": null,
                        "Format": null,
                        "Quality": "85",
                        "Width": null,
                        "Height": null,
                        "ResizeMode": null,
                        "JpegColorType": null,
                        "JpegEncodingColor": null,
                        "Gamma": null,
                        "Quantizer": null,
                        "TransparencyThreshold": null,
                        "BitDepth": null,
                        "PngColorType": null,
                        "CompressionLevel": null,
                        "PngFilterMethod": null,
                        "Sampler": null,
                        "GifColorTableMode": null,
                        "MaxColors": null,
                        "Dither": null,
                        "DevicePixelRatio": 1
                    },
                    "png": {
                        "Sampler": "Bicubic"
                    }
                  },
                  "AllowedHosts": "*"
              }
              """;
    }

    private string MakeSettingsWithoutConcurrency(
        IAppSettings settings
    )
    {
        return
            $$$"""
               {
                   "LogLevel": {
                       "Default": "Warning",
                       "IRAAS": "{{{settings.IRAASLogLevel}}}"
                   },
                   "Kestrel": {
                       "Endpoints": {
                           "Http": {
                               "Url": "http://0.0.0.0:8080"
                           }
                       }
                   },
                   "Settings": {
                       "MaxInputImageSize": "{{settings.MaxInputImageSize}}",
                       "MaxOutputImageSize": "{{settings.MaxOutputImageSize}}",
                       "UseDeveloperExceptionPage": "{{settings.UseDeveloperExceptionPage}}",
                       "UseHttps": "{{settings.UseHttps}}",
                       "EnableTestPage": "{{settings.EnableTestPage}}",
                       "DomainWhitelist": "{{settings.DomainWhitelist}}",
                       "ShareConcurrentRequests": "{{settings.ShareConcurrentRequests}}",
                       "EnableConnectionKeepAlive": "{{settings.EnableConnectionKeepAlive}}",
                       "LogFolder": "{{settings.LogFolder}}"
                   },
                   "DefaultParameters": {
                       "*": {
                           "ReplaceTransparencyWith": null,
                           "Format": null,
                           "Quality": "85",
                           "Width": null,
                           "Height": null,
                           "ResizeMode": null,
                           "JpegColorType": null,
                           "JpegEncodingColor": null,
                           "Gamma": null,
                           "Quantizer": null,
                           "TransparencyThreshold": null,
                           "BitDepth": null,
                           "PngColorType": null,
                           "CompressionLevel": null,
                           "PngFilterMethod": null,
                           "Sampler": null,
                           "GifColorTableMode": null,
                           "MaxColors": null,
                           "Dither": null,
                           "DevicePixelRatio": 1
                       },
                       "png": {
                           "Sampler": "Bicubic"
                       }
                   },
                   "AllowedHosts": "*"
               }
               """;
    }

    private string MakeSettingsWithoutLogFolder(
        IAppSettings settings
    )
    {
        return
            $$"""
              {
                  "Logging": {
                      "LogLevel": {
                          "Default": "Warning",
                          "IRAAS": "{{settings.IRAASLogLevel}}"
                      }
                  },
                  "Kestrel": {
                      "Endpoints": {
                          "Http": {
                              "Url": "http://0.0.0.0:8008"
                          }
                      }
                  },
                  "Settings": {
                      "MaxInputImageSize": "{{settings.MaxInputImageSize}}",
                      "MaxOutputImageSize": "{{settings.MaxOutputImageSize}}",
                      "UseDeveloperExceptionPage": "{{settings.UseDeveloperExceptionPage}}",
                      "UseHttps": "{{settings.UseHttps}}",
                      "EnableTestPage": "{{settings.EnableTestPage}}",
                      "DomainWhitelist": "{{settings.DomainWhitelist}}",
                      "ShareConcurrentRequests": "{{settings.ShareConcurrentRequests}}",
                      "EnableConnectionKeepAlive": "{{settings.EnableConnectionKeepAlive}}"
                  },
                  "DefaultParameters": {
                      "*": {
                          "ReplaceTransparencyWith": null,
                          "Format": null,
                          "Quality": "85",
                          "Width": null,
                          "Height": null,
                          "ResizeMode": null,
                          "JpegColorType": null,
                          "JpegEncodingColor": null,
                          "Gamma": null,
                          "Quantizer": null,
                          "TransparencyThreshold": null,
                          "BitDepth": null,
                          "PngColorType": null,
                          "CompressionLevel": null,
                          "PngFilterMethod": null,
                          "Sampler": null,
                          "GifColorTableMode": null,
                          "MaxColors": null,
                          "Dither": null,
                          "DevicePixelRatio": 1
                      },
                      "png": {
                          "Sampler": "Bicubic"
                      }
                  }
              }
              """;
    }

    private static string ChDir(string target)
    {
        var current = Environment.CurrentDirectory;
        Directory.SetCurrentDirectory(target);
        return current;
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
    public bool Verbose { get; set; }
}