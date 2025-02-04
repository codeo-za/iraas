using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using NSubstitute;

namespace IRAAS.Tests.Fakes;

public class FakeHttpContext : HttpContext
{
    public FakeHttpContext(): this(new Dictionary<string, string>())
    {
    }

    public FakeHttpContext(
        IDictionary<string, string> requestHeaders)
    {
        Features = Substitute.For<IFeatureCollection>();
        Request = new FakeHttpRequest(this, requestHeaders);
        Response = new FakeHttpResponse(this);
    }

    public override void Abort()
    {
    }

    public override IFeatureCollection Features { get; }
    public override HttpRequest Request { get; }
    public override HttpResponse Response { get; }
    public override ConnectionInfo Connection { get; }
    public override WebSocketManager WebSockets { get; }
    public override ClaimsPrincipal User { get; set; }
    public override IDictionary<object, object> Items { get; set; }
    public override IServiceProvider RequestServices { get; set; }
    public override CancellationToken RequestAborted { get; set; }
    public override string TraceIdentifier { get; set; }
    public override ISession Session { get; set; }
}