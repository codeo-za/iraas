using System;

namespace IRAAS.Exceptions
{
    public class ImageSourceNotAllowedException : NotSupportedException
    {
        public string Url { get; }

        public ImageSourceNotAllowedException(string url)
            : base("Image source is not allowed")
        {
            Url = url;
        }
    }
}
