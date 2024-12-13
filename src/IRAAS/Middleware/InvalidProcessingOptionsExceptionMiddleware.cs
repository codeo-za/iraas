using System.Collections.Generic;
using System.Linq;
using IRAAS.Exceptions;
using log4net.Repository.Hierarchy;
using Microsoft.AspNetCore.Http;
using PeanutButter.Utils;

namespace IRAAS.Middleware;

public class InvalidProcessingOptionsExceptionMiddleware
    : ExceptionHandlerMiddleware<InvalidProcessingOptionsException>
{
    public InvalidProcessingOptionsExceptionMiddleware(
        IAppSettings appSettings)
        : base(400, GenerateMessage, appSettings)
    {
    }

    private static string GenerateMessage(
        InvalidProcessingOptionsException ex,
        HttpContext context)
    {
        var lines = new List<string>
        {
            ex.Message,
            "Query parameters:"
        };
        if (context.Request.Query?.Any() ?? false)
        {
            context.Request.Query.ForEach(
                p => lines.Add($"{p.Key}: {p.Value}")
            );
        }
        else
        {
            lines.Add("(none supplied)");
        }

        return lines.JoinWith("\n");
    }
}