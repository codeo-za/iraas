using NUnit.Framework;

namespace IRAAS.Tests;

public class TestBase
{
    [SetUp]
    public void SetupBase()
    {
        TestCacheClearer.ClearAllStaticFieldCaches();
    }

    [TearDown]
    public void TearDownBase()
    {
        TestCacheClearer.ClearAllStaticFieldCaches();
    }
}