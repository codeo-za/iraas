using IRAAS.ImageProcessing;
using NUnit.Framework;

namespace IRAAS.Tests;

public class TestBase
{
    [SetUp]
    public void SetupBase()
    {
        UrlImageResizeParameters.ClearDefaults();
        AppSettingsProvider.ClearCachedSettings();
    }

    [TearDown]
    public void TearDownBase()
    {
        UrlImageResizeParameters.ClearDefaults();
        AppSettingsProvider.ClearCachedSettings();
    }
}