using System;
using System.Collections.Generic;
using System.Net;

namespace IRAAS.ImageProcessing
{
    public class RequestTimedOutException : Exception
    {
        public string Url { get; }
        public IDictionary<string, string> Headers { get; }

        public RequestTimedOutException(
            string url, 
            WebResponse response
            ): this(url, response?.Headers.Clone())
        {
        }

        public RequestTimedOutException(
            string url,
            IDictionary<string, string> headers)
        {
            Url = url;
            Headers = headers;
        }
    }
}