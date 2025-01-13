using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace IRAAS.Tests.Fakes;

public static  class MockHttpContextAccessorExtensions
{
    public static IHttpContextAccessor For(
        this IHttpContextAccessor accessor,
        HttpContext context
    )
    {
        accessor.HttpContext.Returns(_ => context);
        return accessor;
    }
}