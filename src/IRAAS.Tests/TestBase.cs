using IRAAS.ImageProcessing;
using NUnit.Framework;

namespace IRAAS.Tests;

public class TestBase
{
    [SetUp]
    public void SetupBase()
    {
        ImageUrlResizeParameters.ClearDefaults();
        AppSettingsProvider.ClearCachedSettings();
    }

    [TearDown]
    public void TearDownBase()
    {
        ImageUrlResizeParameters.ClearDefaults();
        AppSettingsProvider.ClearCachedSettings();
    }
}