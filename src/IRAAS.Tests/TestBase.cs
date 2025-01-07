using IRAAS.ImageProcessing;
using NUnit.Framework;

namespace IRAAS.Tests;

public class TestBase
{
    [SetUp]
    public void SetupBase()
    {
        ImageResizeOptions.ClearDefaults();
    }

    [TearDown]
    public void TearDownBase()
    {
        ImageResizeOptions.ClearDefaults();
    }
}