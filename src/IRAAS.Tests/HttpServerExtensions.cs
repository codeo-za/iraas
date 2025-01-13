using System;
using System.IO;
using PeanutButter.SimpleHTTPServer;

namespace IRAAS.Tests;

internal static class HttpServerExtensions
{
    internal static void Serve(
        this IHttpServer server,
        string path,
        byte[] data,
        string mimeType,
        Action<HttpProcessor, Stream> onMatchedQuery
    )
    {
        server.AddHandler(
            (processor, stream) =>
            {
                if (processor.Path != path)
                {
                    return HttpServerPipelineResult.NotHandled;
                }

                onMatchedQuery(processor, stream);

                processor.WriteOKStatusHeader();
                processor.WriteMIMETypeHeader(mimeType);
                processor.WriteConnectionClosesAfterCommsHeader();
                processor.WriteContentLengthHeader(
                    data.Length
                );
                processor.WriteEmptyLineToStream();
                processor.WriteDataToStream(
                    data
                );
                return HttpServerPipelineResult.Handled;
            }
        );
    }
}