using System;
using System.Text;
using System.Threading.Tasks;
using IRAAS.Middleware;
using NSubstitute;
using NUnit.Framework;
using PeanutButter.TestUtils.AspNetCore.Builders;
using PeanutButter.Utils;

namespace IRAAS.Tests.Middleware;

[TestFixture]
public class TestArgumentNullExceptionMiddleware
{
    [Test]
    public async Task ShouldReturnBadRequest()
    {
        // Arrange
        var sut = Create();
        var context = HttpContextBuilder.BuildDefault();
        var paramName = GetRandomString();

        // Act
        await sut.InvokeAsync(
            context,
            _ => Task.FromException(new ArgumentNullException(paramName))
        );

        // Assert
        Expect(context.Response.StatusCode)
            .To.Equal(400);
        Expect(
            await context.Response.Body.ReadAllTextAsync()
        ).To.Equal(paramName);
    }

    private static ArgumentNullExceptionMiddleware Create()
    {
        return new ArgumentNullExceptionMiddleware(
            Substitute.For<IAppSettings>()
        );
    }
}

[TestFixture]
public class TestArgumentExceptionMiddleware
{
    [Test]
    public async Task ShouldReturnBadRequest()
    {
        // Arrange
        var sut = Create();
        var context = HttpContextBuilder.BuildDefault();
        var paramName = GetRandomString();
        var message = GetRandomWords();

        // Act
        await sut.InvokeAsync(
            context,
            _ => Task.FromException(new ArgumentException(message, paramName))
        );

        // Assert
        Expect(context.Response.StatusCode)
            .To.Equal(400);
        Expect(
            await context.Response.Body.ReadAllTextAsync()
        ).To.Equal($"{message} (Parameter '{paramName}')");
    }

    private static ArgumentExceptionMiddleware Create()
    {
        return new ArgumentExceptionMiddleware(
            Substitute.For<IAppSettings>()
        );
    }
}