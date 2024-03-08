using System;
using System.Collections.Generic;
using System.IO;

namespace IRAAS.ImageProcessing
{
    public class StreamAndHeaders : IDisposable
    {
        public Stream Stream { get; private set; }
        public IDictionary<string, string> Headers { get; }

        public StreamAndHeaders(
            Stream stream,
            IDictionary<string, string> headers)
        {
            Headers = headers;
            Stream = stream;
        }

        public void Dispose()
        {
            Stream?.Dispose();
            Stream = null;
        }
    }
}