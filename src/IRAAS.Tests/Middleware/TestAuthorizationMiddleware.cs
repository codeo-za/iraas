using System;
using System.Collections.Generic;
using IRAAS.Middleware;
using IRAAS.Security;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using NUnit.Framework;
using PeanutButter.TestUtils.AspNetCore.Builders;
using PeanutButter.TestUtils.AspNetCore.Utils;
using PeanutButter.Utils;

namespace IRAAS.Tests.Middleware;

[TestFixture]
public class TestAuthorizationMiddleware
{
    [TestFixture]
    public class WhenRequestIsGetRequest
    {
        [TestFixture]
        public class AndIsRequestToTestView
        {
            [TestFixture]
            public class WhenTestViewDisabled
            {
                [TestCase("/test")]
                [TestCase("/size")]
                public void ShouldThrowNotImplemented(
                    string requestPath
                )
                {
                    // Arrange
                    HttpContext captured = null;
                    var (ctx, next) = RequestDelegateTestArenaBuilder.Create()
                        .WithContext(
                            HttpContextBuilder.Create()
                                .WithRequestMethod(HttpMethods.Get)
                                .WithRequestMutator(req => { req.Path = requestPath; })
                                .Build()
                        ).WithDelegateLogic(c => captured = c)
                        .Build();
                    var whitelist = CreateAllowingWhitelist();
                    var appSettings = Substitute.For<IAppSettings>()
                        .With(o => o.EnableTestPage.Returns(false));
                    var sut = Create(appSettings, whitelist);

                    // Act
                    Expect(async () => await sut.InvokeAsync(ctx, next))
                        .To.Throw<NotImplementedException>();

                    // Assert
                    Expect(captured)
                        .To.Be(null);
                }
            }

            [TestFixture]
            public class WhenTestViewEnabled
            {
                [TestCase("/test")]
                [TestCase("/size")]
                public void ShouldContinue(
                    string requestPath
                )
                {
                    // Arrange
                    HttpContext captured = null;
                    var (ctx, next) = RequestDelegateTestArenaBuilder.Create()
                        .WithContext(
                            HttpContextBuilder.Create()
                                .WithRequestMethod(HttpMethods.Get)
                                .WithRequestMutator(req => { req.Path = requestPath; })
                                .Build()
                        ).WithDelegateLogic(c => captured = c)
                        .Build();
                    var whitelist = CreateAllowingWhitelist();
                    var appSettings = Substitute.For<IAppSettings>()
                        .With(o => o.EnableTestPage.Returns(true));
                    var sut = Create(appSettings, whitelist);

                    // Act
                    Expect(async () => await sut.InvokeAsync(ctx, next))
                        .Not.To.Throw();

                    // Assert
                    Expect(captured)
                        .To.Be(ctx);
                }
            }
        }

        [TestFixture]
        public class AndRequestedUrlIsWhitelisted
        {
            [Test]
            public void ShouldContinue()
            {
                // Arrange
                HttpContext captured = null;
                var requestUrlParameter = GetRandomHttpsUrlWithPath();
                var (ctx, next) = RequestDelegateTestArenaBuilder.Create()
                    .WithContext(
                        HttpContextBuilder.Create()
                            .WithRequestMethod(HttpMethods.Get)
                            .WithRequestQueryParameter(
                                "url",
                                requestUrlParameter
                            )
                            .Build()
                    ).WithDelegateLogic(c => captured = c)
                    .Build();
                var whitelist = CreateAllowingWhitelist();
                var sut = Create(whitelist: whitelist);

                // Act
                sut.InvokeAsync(ctx, next);

                // Assert
                Expect(captured)
                    .To.Be(ctx);
                Expect(whitelist)
                    .To.Have.Received(1)
                    .IsAllowed(requestUrlParameter);
            }
        }

        [TestFixture]
        public class AndRequestedUrlNotIsWhitelisted
        {
            [Test]
            public void ShouldContinue()
            {
                // Arrange
                HttpContext captured = null;
                var requestUrlParameter = GetRandomHttpsUrlWithPath();
                var (ctx, next) = RequestDelegateTestArenaBuilder.Create()
                    .WithContext(
                        HttpContextBuilder.Create()
                            .WithRequestMethod(HttpMethods.Get)
                            .WithRequestQueryParameter(
                                "url",
                                requestUrlParameter
                            )
                            .Build()
                    ).WithDelegateLogic(c => captured = c)
                    .Build();
                var whitelist = CreateDisallowingWhitelist();
                var sut = Create(whitelist: whitelist);

                // Act
                Expect(async () => await sut.InvokeAsync(ctx, next))
                    .To.Throw<NotImplementedException>();

                // Assert
                Expect(captured)
                    .To.Be(null);
                Expect(whitelist)
                    .To.Have.Received(1)
                    .IsAllowed(requestUrlParameter);
            }
        }
    }

    [TestFixture]
    public class WhenRequestIsNotGetOrPost
    {
        public static IEnumerable<string> TestCaseGenerator()
        {
            yield return HttpMethods.Put;
            yield return HttpMethods.Patch;
            yield return HttpMethods.Connect;
            yield return HttpMethods.Delete;
            yield return HttpMethods.Head;
            yield return HttpMethods.Trace;
        }

        [TestCaseSource(nameof(TestCaseGenerator))]
        [Test]
        public void ShouldThrowNotImplemented(
            string method
        )
        {
            // Arrange
            HttpContext captured = null;
            var (ctx, next) = RequestDelegateTestArenaBuilder.Create()
                .WithContext(
                    HttpContextBuilder.Create()
                        .WithRequestMethod(method)
                        .Build()
                ).WithDelegateLogic(c => captured = c)
                .Build();
            var sut = Create();

            // Act
            Expect(async () => await sut.InvokeAsync(ctx, next))
                .To.Throw<NotImplementedException>();

            // Assert
            Expect(captured)
                .To.Be.Null();
        }
    }

    [TestFixture]
    public class WhenRequestIsPost
    {
        [TestFixture]
        public class WhenPostIsNotAllowed
        {
            [Test]
            public void ShouldThrowNotImplemented()
            {
                // Arrange
                var appSettings = Substitute.For<IAppSettings>();
                Expect(appSettings.AllowPostRequests)
                    .To.Be.False();
                HttpContext captured = null;
                var (ctx, next) = RequestDelegateTestArenaBuilder.Create()
                    .WithContext(
                        HttpContextBuilder.Create()
                            .WithRequestMethod(HttpMethods.Post)
                            .Build()
                    ).WithDelegateLogic(c => captured = c)
                    .Build();
                var sut = Create(appSettings);

                // Act
                Expect(async () => await sut.InvokeAsync(ctx, next))
                    .To.Throw<NotImplementedException>();

                // Assert
                Expect(captured)
                    .To.Be.Null();
            }
        }

        [TestFixture]
        public class WhenPostIsAllowed
        {
            [TestFixture]
            public class ButAuthTokensIsEmpty
            {
                [TestCase("")]
                [TestCase(" ")]
                [TestCase(null)]
                public void ShouldThrowNotImplemented_(
                    string postTokens
                )
                {
                    // Arrange
                    var appSettings = Substitute.For<IAppSettings>()
                        .With(o => o.AllowPostRequests.Returns(true))
                        .With(o => o.PostAuthTokens.Returns(postTokens));
                    HttpContext captured = null;
                    var (ctx, next) = RequestDelegateTestArenaBuilder.Create()
                        .WithContext(
                            HttpContextBuilder.Create()
                                .WithRequestMethod(HttpMethods.Post)
                                .Build()
                        ).WithDelegateLogic(c => captured = c)
                        .Build();
                    var sut = Create(appSettings);

                    // Act
                    Expect(async () => await sut.InvokeAsync(ctx, next))
                        .To.Throw<NotImplementedException>();

                    // Assert
                    Expect(captured)
                        .To.Be.Null();
                }
            }

            [TestFixture]
            public class AndAuthTokenProvided
            {
                [TestFixture]
                public class ButTokenIsUnknown
                {
                    [Test]
                    public void ShouldThrowNotImplemented()
                    {
                        // Arrange
                        var invalidToken = $"{Guid.NewGuid()}";
                        var appSettings = Substitute.For<IAppSettings>()
                            .With(o => o.AllowPostRequests.Returns(true))
                            .With(o => o.PostAuthTokens.Returns($"{Guid.NewGuid()},{Guid.NewGuid()}"));
                        HttpContext captured = null;
                        var (ctx, next) = RequestDelegateTestArenaBuilder.Create()
                            .WithContext(
                                HttpContextBuilder.Create()
                                    .WithRequestMethod(HttpMethods.Post)
                                    .WithRequestHeader(
                                        "Authorization",
                                        $"Bearer {invalidToken}"
                                    )
                                    .Build()
                            ).WithDelegateLogic(c => captured = c)
                            .Build();
                        var sut = Create(appSettings);

                        // Act
                        Expect(async () => await sut.InvokeAsync(ctx, next))
                            .To.Throw<NotImplementedException>();

                        // Assert
                        Expect(captured)
                            .To.Be.Null();
                    }
                }

                [TestFixture]
                public class AndAuthTokenIsKnown
                {
                    [Test]
                    public void ShouldContinue()
                    {
                        // Arrange
                        var validToken = $"{Guid.NewGuid()}";
                        var appSettings = Substitute.For<IAppSettings>()
                            .With(o => o.AllowPostRequests.Returns(true))
                            .With(o => o.PostAuthTokens.Returns($"{validToken},{Guid.NewGuid()}"));
                        HttpContext captured = null;
                        var (ctx, next) = RequestDelegateTestArenaBuilder.Create()
                            .WithContext(
                                HttpContextBuilder.Create()
                                    .WithRequestMethod(HttpMethods.Post)
                                    .WithRequestHeader(
                                        "Authorization",
                                        $"Bearer {validToken}"
                                    )
                                    .Build()
                            ).WithDelegateLogic(c => captured = c)
                            .Build();
                        var sut = Create(appSettings);

                        // Act
                        Expect(async () => await sut.InvokeAsync(ctx, next))
                            .Not.To.Throw();

                        // Assert
                        Expect(captured)
                            .To.Be(ctx);
                    }
                }
            }
        }
    }

    private static AuthorizationMiddleware Create(
        IAppSettings appSettings = null,
        IWhitelist whitelist = null
    )
    {
        return new(
            appSettings ?? Substitute.For<IAppSettings>(),
            whitelist ?? CreateAllowingWhitelist()
        );
    }

    private static IWhitelist CreateAllowingWhitelist()
    {
        return Substitute.For<IWhitelist>()
            .With(
                o => o.IsAllowed(Arg.Any<string>())
                    .Returns(_ => true)
            );
    }

    private static IWhitelist CreateDisallowingWhitelist()
    {
        return Substitute.For<IWhitelist>()
            .With(
                o => o.IsAllowed(Arg.Any<string>())
                    .Returns(_ => false)
            );
    }
}