using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace IRAAS.Tests.Middleware
{
    public static class RequestContextDelegateHelperExtensions
    {
        public static RequestDelegate AsRequestDelegate(
            this Func<HttpContext, Task> func
        )
        {
            return new RequestDelegate(func);
        }
    }
}