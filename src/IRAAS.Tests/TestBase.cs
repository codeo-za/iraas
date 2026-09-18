using IRAAS.ImageProcessing;
using NUnit.Framework;

namespace IRAAS.Tests;

public class TestBase
{
    [SetUp]
    public void SetupBase()
    {
        ImageResizeParameters.ClearDefaults();
        AppSettingsProvider.ClearCachedSettings();
    }

    [TearDown]
    public void TearDownBase()
    {
        ImageResizeParameters.ClearDefaults();
        AppSettingsProvider.ClearCachedSettings();
    }
}