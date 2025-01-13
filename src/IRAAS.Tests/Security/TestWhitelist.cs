using IRAAS.Security;
using NSubstitute;
using NUnit.Framework;

namespace IRAAS.Tests.Security;

[TestFixture]
public class TestWhitelist: TestBase
{
    [Test]
    public void ShouldImplement_IWhitelist()
    {
        // Arrange
        // Act
        Expect(typeof(Whitelist))
            .To.Implement<IWhitelist>();
        // Assert
    }

    [TestFixture]
    public class Behavior: TestBase
    {
        [TestCase("")]
        [TestCase(" ")]
        [TestCase(null)]
        public void ShouldAllowAnyHostWhenNotConfigured(string conf)
        {
            // Arrange
            var sut = Create(conf);
            // Act
            var allowed = sut.IsAllowed(GetRandomHttpUrl());
            // Assert
            Expect(allowed)
                .To.Be.True();
        }

        [Test]
        public void ShouldAllowExactMatchForOneDomain()
        {
            // Arrange
            var domain = GetRandomHostname();
            var url = CreateImageUrlFor(domain);
            var sut = Create(domain);
            // Act
            var result = sut.IsAllowed(url);
            // Assert
            Expect(result)
                .To.Be.True();
        }

        [Test]
        public void ShouldNotAllowMismatchOnExactDomain()
        {
            // Arrange
            var notAllowed = GetRandomHostname();
            var allowed = GetAnother(notAllowed, GetRandomHostname);
            var url = CreateImageUrlFor(notAllowed);
            var sut = Create(allowed);
            // Act
            var result = sut.IsAllowed(url);
            // Assert
            Expect(result)
                .To.Be.False();
        }

        [Test]
        public void ShouldAllowDomainGlobbingForSubdomains()
        {
            // Arrange
            var parent = GetRandomHostname();
            var subDomain = $"{GetRandomString(2)}.{parent}";
            var url = CreateImageUrlFor(subDomain);
            var sut = Create($"*.{parent}");
            // Act
            var result = sut.IsAllowed(url);
            // Assert
            Expect(result)
                .To.Be.True();
        }
            
        [Test]
        public void ShouldAllowCaseInsensitiveDomainGlobbing()
        {
            // Arrange
            var parent = GetRandomHostname();
            var subDomain = $"{GetRandomString(2)}.{parent}";
            parent = parent.ToUpper();
            var url = CreateImageUrlFor(subDomain);
            var sut = Create($"*.{parent}");
                
            // Pre-assert
            Expect(subDomain)
                .Not.To.Contain(parent);
            Expect(subDomain.ToLower()).To.Contain(parent.ToLower());
            // Act
            var result = sut.IsAllowed(url);
            // Assert
            Expect(result)
                .To.Be.True();
        }

        [Test]
        public void ShouldAllowDomainGlobbingAtAnyPoint()
        {
            // Arrange
            var subDomain = "a.foo.c";
            var url = CreateImageUrlFor(subDomain);
            var sut = Create("a.*.c");
            // Act
            var result = sut.IsAllowed(url);
            // Assert
            Expect(result)
                .To.Be.True();
        }

        [Test]
        public void ShouldNotAllowDomainNotMatchedByGlob()
        {
            // Arrange
            var parent = "a.b.c";
            var notSub = "foo.b.c";
            var url = CreateImageUrlFor(notSub);
            var sut = Create($"*.{parent}");
            // Act
            var result = sut.IsAllowed(url);
            // Assert
            Expect(result)
                .To.Be.False();
        }

        [Test]
        public void ShouldNotMatchTheParentWhenGlobbingSubDomains()
        {
            // Arrange
            var parent = "a.b.c";
            var url = CreateImageUrlFor(parent);
            var sut = Create($"*.{parent}");
            // Act
            var result = sut.IsAllowed(url);
            // Assert
            Expect(result)
                .To.Be.False();
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void ShouldAllowEmptyOrBlankUrls(string url)
        {
            // because the Image Resize Service supplies a good
            //  failure reason already
            // Arrange
            var allowed = GetRandomHostname();
            var sut = Create(allowed);
            // Act
            var result = sut.IsAllowed(url);
            // Assert
            Expect(result)
                .To.Be.True();
        }

        [TestFixture]
        public class WildBugs: TestBase
        {
            [TestCase("*.moodemo.com, *.moo.com", "static-test.moo.com")]
            public void ShouldMatch_(string regex, string hostname)
            {
                // Arrange
                var sut = Create(regex);
                // Act
                var result = sut.IsAllowed($"http://{hostname}/resources?id=123");
                // Assert
                Expect(result)
                    .To.Be.True();
            }
        }
    }

    private static string CreateImageUrlFor(string domain)
    {
        var ext = GetRandomFrom(new[] { "png", "jpg", "gif", "bmp" });
        return $"http://{domain}/${GetRandomString(2)}/{GetRandomString(2)}.{ext}";
    }

    private static IWhitelist Create(string conf)
    {
        var settings = Substitute.For<IAppSettings>();
        settings.DomainWhitelist.Returns(conf);
        return new Whitelist(settings);
    }
}
