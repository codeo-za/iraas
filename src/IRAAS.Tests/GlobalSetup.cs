using NUnit.Framework;

namespace IRAAS.Tests;

[SetUpFixture]
public class GlobalSetup
{
    [OneTimeSetUp]
    public void Setup()
    {
        TestEnvironment.Setup();
    }

    [OneTimeTearDown]
    public void Teardown()
    {
        TestEnvironment.Teardown();
    }
}