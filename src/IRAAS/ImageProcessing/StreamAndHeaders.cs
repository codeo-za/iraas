using System;
using System.Collections.Generic;
using System.IO;

namespace IRAAS.ImageProcessing;

public class StreamAndHeaders : IDisposable
{
    public Stream Stream { get; private set; }
    public IDictionary<string, string> Headers { get; }

    public StreamAndHeaders(
        Stream stream,
        IDictionary<string, string> headers
    )
    {
        Headers = headers;
        Stream = stream;
    }

    public StreamAndHeaders(
        byte[] data
    ) : this(data, new Dictionary<string, string>())
    {
    }

    public StreamAndHeaders(
        byte[] data,
        IDictionary<string, string> headers
    )
    {
        Headers = headers;
        Stream = new MemoryStream(data);
    }

    public void Dispose()
    {
        Stream?.Dispose();
        Stream = null;
    }
}