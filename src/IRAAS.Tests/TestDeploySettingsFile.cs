using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace IRAAS.Tests;

[TestFixture]
public class TestDeploySettingsFile: TestBase
{
    [TestCase("Logging.LogLevel.Default")]
    [TestCase("Settings.MaxInputImageSize")]
    [TestCase("Settings.MaxOutputImageSize")]
    [TestCase("Settings.UseDeveloperExceptionPage")]
    [TestCase("Settings.UseHttps")]
    [TestCase("Settings.EnableTestPage")]
    [TestCase("Settings.DomainWhitelist")]
    public void ShouldBeOctopusVariable_(string jsonPath)
    {
        // Arrange
        var config = (JObject)JsonConvert.DeserializeObject(
            File.ReadAllText(
                FindDeploymentConfig()
            )
        );
        // Act
        var result = config.Get(jsonPath);
        // Assert
        Expect(result)
            .Not.To.Be.Null(() => $"Missing setting: {jsonPath}");
        Expect(result)
            .To.Match(new Regex("#{.+}"));
    }
        
    private string FindDeploymentConfig()
    {
        return FindFileUpward("appsettings.deploy.json");
    }

    private string FindFileUpward(string fileName)
    {
        var startFolder = Path.GetDirectoryName(new Uri(
                typeof(TestDeploySettingsFile).Assembly.Location
            ).LocalPath
        );
        var current = startFolder;
        while (current != null)
        {
            var test = Path.Combine(current, "IRAAS", fileName);
            if (File.Exists(test))
            {
                return test;
            }

            current = Path.GetDirectoryName(current);
        }

        throw new Exception(
            $"Unable to find '{fileName}' when travelling upward from '{startFolder}'"
        );
    }
}

public static class JObjectExtensions
{
    public static string Get(this JObject obj, string path)
    {
        var parts = new Queue<string>(path.Split('.'));
        var current = obj;
        while (true)
        {
            var prop = parts.Dequeue();
            var propValue = current[prop];
            if (parts.Count == 0 || propValue == null)
            {
                return propValue?.Value<string>();
            }
            current = (JObject)propValue;
        }
    }
}
