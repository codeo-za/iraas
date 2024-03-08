using System;
using System.Collections.Generic;
using System.Net;

namespace IRAAS.ImageProcessing
{
    public class ImageProviderErrorException
        : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public string RequestUri { get; }
        public IDictionary<string, string> RequestHeaders { get; }
        public IDictionary<string, string> ResponseHeaders { get; }

        public ImageProviderErrorException(
            HttpStatusCode statusCode,
            string requestUri,
            IDictionary<string, string> requestHeaders,
            IDictionary<string, string> responseHeaders
            ): base($"Unable to retrieve image at: {requestUri}")
        {
            StatusCode = statusCode;
            RequestUri = requestUri;
            RequestHeaders = requestHeaders;
            ResponseHeaders = responseHeaders;
        }
    }
}